using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using System.Text;
using System.Text.Json;
using VanAn.CoreHub.Hubs;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP.Infrastructure;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Subscribes to NATS "vanan.shoperp.order.created" events published by Gateway's NatsSyncWorker.
    /// Syncs order data from PostgreSQL (Gateway) → SQLite (ShopERP) so Owner can see orders in the UI.
    ///
    /// Flow: Gateway creates order → PostgreSQL → Outbox → NatsSyncWorker → NATS → this subscriber → SQLite
    /// </summary>
    public class OrderSyncSubscriber : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OrderSyncSubscriber> _logger;
        private readonly IHubContext<OrderHub> _hubContext;
        private IConnection? _subscriptionConnection;

        public OrderSyncSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<OrderSyncSubscriber> logger,
            IHubContext<OrderHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Phase 4: Validate SHOP_INSTANCE_ID BEFORE attempting NATS connection.
            // Without it, we cannot route subscriptions to this ShopERP's ShopInstance.
            // Failing fast prevents cross-VPS data leaks (all ShopERPs receiving all orders).
            Guid shopInstanceId = ResolveShopInstanceId();
            string createdSubject = $"vanan.cloud.order.created.{shopInstanceId}";
            string statusSubject = $"vanan.cloud.order.status.changed.{shopInstanceId}";

            string url = _configuration.GetValue<string>("Nats:Url")
                ?? _configuration.GetValue<string>("NATS:Url")
                ?? _configuration.GetValue<string>("NATS__Url")
                ?? _configuration.GetValue<string>("ConnectionStrings:Nats")
                ?? "nats://localhost:4222";

            // Retry loop: NATS may be temporarily unavailable at startup (container starting,
            // network not ready, VPC firewall rule not applied yet). Previous implementation
            // gave up after 1 attempt → subscriber never started → orders never synced.
            // Now retries with exponential backoff until connected or cancelled.
            int retryDelay = 2000; // start at 2s
            const int maxRetryDelay = 30000; // cap at 30s

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _subscriptionConnection = CreateSubscriptionConnection(url);

                    // Phase 4: Subscribe ONLY to routed subjects (vanan.cloud.order.created.{shopInstanceId}).
                    // Previous wildcard subscription (vanan.cloud.order.created.>) removed — would cause
                    // cross-VPS data leak in multi-VPS deployment.
                    _ = _subscriptionConnection.SubscribeAsync(createdSubject, async (sender, args) =>
                    {
                        await SyncOrderCreatedAsync(args.Message.Data, stoppingToken);
                    });
                    RecordSubscription(createdSubject);

                    _ = _subscriptionConnection.SubscribeAsync(statusSubject, async (sender, args) =>
                    {
                        await SyncOrderStatusChangedAsync(args.Message.Data, stoppingToken);
                    });
                    RecordSubscription(statusSubject);

                    _logger.LogInformation(
                        "OrderSyncSubscriber connected to NATS {Url}, subscribed to {CreatedSubject} + {StatusSubject} (ShopInstanceId={ShopInstanceId})",
                        url, createdSubject, statusSubject, shopInstanceId);

                    // Connected successfully — wait indefinitely until cancelled
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "OrderSyncSubscriber: NATS unavailable at {Url}. Retrying in {RetryDelay}ms. Subjects: {CreatedSubject}, {StatusSubject}",
                        url, retryDelay, createdSubject, statusSubject);

                    try
                    {
                        await Task.Delay(retryDelay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    // Exponential backoff: 2s → 4s → 8s → 16s → 30s (cap)
                    retryDelay = Math.Min(retryDelay * 2, maxRetryDelay);
                }
            }

            _logger.LogInformation("OrderSyncSubscriber stopped.");
        }

        /// <summary>
        /// Resolves this ShopERP's ShopInstanceId from config (env var SHOP_INSTANCE_ID
        /// via configuration provider, or ShopInstance:Id config key). Throws if missing/invalid.
        /// </summary>
        private Guid ResolveShopInstanceId()
        {
            string? shopInstanceIdStr = _configuration.GetValue<string>("ShopInstance:Id")
                ?? Environment.GetEnvironmentVariable("SHOP_INSTANCE_ID");

            if (!Guid.TryParse(shopInstanceIdStr, out Guid shopInstanceId) || shopInstanceId == Guid.Empty)
            {
                _logger.LogError(
                    "OrderSyncSubscriber: SHOP_INSTANCE_ID not configured. Set env var SHOP_INSTANCE_ID or config ShopInstance:Id. Aborting subscriber.");
                throw new InvalidOperationException(
                    "SHOP_INSTANCE_ID not configured — cannot route NATS subscription. " +
                    "Set env var SHOP_INSTANCE_ID or config ShopInstance:Id to this ShopERP's ShopInstance Guid.");
            }

            return shopInstanceId;
        }

        /// <summary>
        /// Creates the NATS subscription connection. Extracted as protected virtual
        /// to enable testing without a real NATS server (test subclass overrides to
        /// return a mock IConnection).
        /// </summary>
        protected virtual IConnection CreateSubscriptionConnection(string url)
        {
            var opts = ConnectionFactory.GetDefaultOptions();
            opts.Url = url;
            opts.MaxReconnect = 5;
            opts.ReconnectWait = 2000;
            opts.Name = "vanan-shoperp-order-sync-subscriber";
            return new ConnectionFactory().CreateConnection(opts);
        }

        /// <summary>
        /// Records a subscribed subject string. Test subclasses override to capture
        /// the routed subject for assertion. Production implementation is a no-op.
        /// </summary>
        protected virtual void RecordSubscription(string subject) { }

        /// <summary>
        /// Sync OrderCreated event from Gateway → SQLite.
        /// Payload shape (from OrderService.CreateOrderFromCommandAsync):
        ///   { eventId, orderId, tenantId, status, totalAmount, subTotal, totalVatAmount, 
        ///     paymentStatus, orderType, orderDate, createdAt, trackingCode, customerInfo, items[] }
        /// </summary>
        private async Task SyncOrderCreatedAsync(byte[] data, CancellationToken cancellationToken)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                Guid orderId = root.GetProperty("OrderId").GetGuid();
                Guid tenantId = root.GetProperty("TenantId").GetGuid();

                using IServiceScope scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();

                // Check if order already exists in SQLite (idempotent)
                bool exists = await dbContext.Orders.AnyAsync(o => o.Id == orderId, cancellationToken);
                if (exists)
                {
                    _logger.LogDebug("OrderSyncSubscriber: order {OrderId} already exists in SQLite", orderId);
                    return;
                }

                // Create order in SQLite using DDD factory method
                TenantId tenantIdObj = new(tenantId);
                var items = new List<OrderItem>();

                if (root.TryGetProperty("Items", out var itemsProp))
                {
                    // Pre-check: ensure all ProductIds exist in SQLite before inserting order.
                    // If a product is missing, create a stub from the event payload to prevent FK violation.
                    // Phase 4: stub uses UnitPrice + VatRate from the order payload (client snapshot from QR)
                    // instead of 0m — avoids price-validation failures when a customer scans a legacy QR
                    // and the ShopERP product does not yet exist.
                    var productIds = new List<(Guid ProductId, string ProductName, decimal UnitPrice, decimal VatRate)>();
                    foreach (var item in itemsProp.EnumerateArray())
                    {
                        Guid productId = item.GetProperty("ProductId").GetGuid();
                        string productName = item.TryGetProperty("ProductName", out var pnProp) ? pnProp.GetString() ?? "" : "";
                        decimal unitPrice = item.TryGetProperty("UnitPrice", out var upProp) ? upProp.GetDecimal() : 0m;
                        decimal vatRate = item.TryGetProperty("VatRate", out var vrProp) ? vrProp.GetDecimal() : 0.10m;
                        productIds.Add((productId, productName, unitPrice, vatRate));
                    }

                    // Auto-create missing products as stubs (idempotent — skip if already exists)
                    foreach (var (productId, productName, unitPrice, vatRate) in productIds)
                    {
                        bool productExists = await dbContext.Products
                            .IgnoreQueryFilters()
                            .AnyAsync(p => p.Id == productId, cancellationToken);
                        if (!productExists)
                        {
                            var stub = new Product(tenantIdObj, productName, "Synced from Gateway", unitPrice, "Synced", true, null, vatRate, 0m);
                            typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(stub, productId);
                            typeof(Product).GetProperty("ProductId")!.SetValue(stub, new ProductId(productId));
                            _ = dbContext.Products.Add(stub);
                            _logger.LogInformation("OrderSyncSubscriber: auto-created product stub {ProductId} ({Name}) UnitPrice={UnitPrice} VatRate={VatRate}",
                                productId, productName, unitPrice, vatRate);
                        }
                    }

                    // Save stubs before creating order (FK constraint requires products to exist first)
                    if (dbContext.ChangeTracker.HasChanges())
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }

                    // Now build OrderItems (products are guaranteed to exist)
                    foreach (var item in itemsProp.EnumerateArray())
                    {
                        Guid itemId = item.TryGetProperty("ItemId", out var idProp) ? idProp.GetGuid() : Guid.NewGuid();
                        Guid productId = item.GetProperty("ProductId").GetGuid();
                        int quantity = item.GetProperty("Quantity").GetInt32();
                        decimal unitPrice = item.GetProperty("UnitPrice").GetDecimal();
                        // RC-3 fix: parse ProductName + VatRate from payload (previously dropped).
                        string productName = item.TryGetProperty("ProductName", out var pnProp) ? pnProp.GetString() ?? "" : "";
                        decimal vatRate = item.TryGetProperty("VatRate", out var vrProp) ? vrProp.GetDecimal() : 0.10m;

                        var orderItem = OrderItem.Create(itemId, tenantIdObj, orderId, productId, quantity, unitPrice, productName, vatRate);
                        items.Add(orderItem);
                    }
                }

                // Bug 4 fix: parse CustomerId from payload and link order to Customer entity.
                // Previously Order.Create was called with null customerId → order.CustomerId = null in SQLite
                // → OrderWorkflowService.ProcessLoyaltyPointsAsync could not find customer → no points awarded.
                // Now: if payload has CustomerId, create a Customer stub in SQLite (if missing) and link the order.
                Guid? customerId = null;
                if (root.TryGetProperty("CustomerId", out var cidProp) && cidProp.ValueKind == JsonValueKind.String)
                {
                    string? cidStr = cidProp.GetString();
                    if (Guid.TryParse(cidStr, out Guid cid) && cid != Guid.Empty)
                    {
                        // Ensure Customer row exists in SQLite (FK constraint + loyalty lookup).
                        // Auto-create stub from CustomerInfo if missing — mirrors product stub pattern above.
                        bool customerExists = await dbContext.Customers
                            .IgnoreQueryFilters()
                            .AnyAsync(c => c.Id == cid, cancellationToken);
                        if (!customerExists)
                        {
                            string cName = "";
                            string cPhone = "";
                            string? cEmail = null;
                            if (root.TryGetProperty("CustomerInfo", out var ciProp))
                            {
                                cName = ciProp.TryGetProperty("FullName", out var n) ? n.GetString() ?? "" : "";
                                cPhone = ciProp.TryGetProperty("PhoneNumber", out var p) ? p.GetString() ?? "" : "";
                                cEmail = ciProp.TryGetProperty("Email", out var e) ? e.GetString() : null;
                            }
                            if (string.IsNullOrWhiteSpace(cName)) cName = "Khách hàng";
                            if (string.IsNullOrWhiteSpace(cPhone)) cPhone = "N/A";

                            var customerStub = new Customer(tenantIdObj, cName, cPhone, cEmail);
                            // Single-identity: align BaseEntity.Id (PK) with CustomerId (business key).
                            typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(customerStub, cid);
                            typeof(Customer).GetProperty("CustomerId")!.SetValue(customerStub, new CustomerId(cid));
                            // FK-safety shell — not a verified identity. Guest marking keeps it
                            // mergeable by CustomerMergeService and lets a later login upgrade it.
                            customerStub.MarkAsGuestStub();
                            _ = dbContext.Customers.Add(customerStub);
                            _logger.LogInformation("OrderSyncSubscriber: auto-created customer stub {CustomerId} ({Name})",
                                cid, cName);
                        }
                        customerId = cid;
                    }
                }

                // Bug 4: save Customer stub before creating order (FK_Orders_Customers_CustomerId).
                // Mirrors the product stub save pattern above.
                if (dbContext.ChangeTracker.HasChanges())
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                Order order = Order.Create(orderId, tenantIdObj, customerId, items);

                // Set customer info if provided
                if (root.TryGetProperty("CustomerInfo", out var infoProp))
                {
                    string name = infoProp.TryGetProperty("FullName", out var n) ? n.GetString() ?? "" : "";
                    string phone = infoProp.TryGetProperty("PhoneNumber", out var p) ? p.GetString() ?? "" : "";
                    string email = infoProp.TryGetProperty("Email", out var e) ? e.GetString() ?? "" : "";
                    string address = infoProp.TryGetProperty("Address", out var a) ? a.GetString() ?? "" : "";

                    if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(phone))
                    {
                        order.SetCustomerInfo(new CustomerInfo(name, phone, email, address));
                    }
                }

                // CC-S2 fix (Issue #2): sync OrderType from payload. Previously Order.Create()
                // defaulted to DINEIN and the subscriber never read OrderType from the event →
                // DELIVERY orders from PG were stored as DINEIN in SQLite → shipper filter
                // OrderType=DELIVERY returned 0 → owner confirmed but shipper never saw the order.
                // Now: read OrderType + delivery fields from payload and call SetOrderType.
                if (root.TryGetProperty("OrderType", out var otProp))
                {
                    string? payloadOrderType = otProp.GetString();
                    if (!string.IsNullOrWhiteSpace(payloadOrderType) && payloadOrderType != "DINEIN")
                    {
                        string? deliveryAddress = root.TryGetProperty("DeliveryAddress", out var daProp) ? daProp.GetString() : null;
                        double? deliveryLat = root.TryGetProperty("DeliveryLat", out var latProp) && latProp.ValueKind == JsonValueKind.Number ? latProp.GetDouble() : null;
                        double? deliveryLng = root.TryGetProperty("DeliveryLng", out var lngProp) && lngProp.ValueKind == JsonValueKind.Number ? lngProp.GetDouble() : null;
                        decimal shippingFee = root.TryGetProperty("ShippingFee", out var sfProp) && sfProp.ValueKind == JsonValueKind.Number ? sfProp.GetDecimal() : 0m;

                        // Fallback: for DELIVERY without explicit DeliveryAddress in payload,
                        // use CustomerInfo.Address (checkout stores delivery address there).
                        if (payloadOrderType == "DELIVERY" && string.IsNullOrWhiteSpace(deliveryAddress)
                            && root.TryGetProperty("CustomerInfo", out var ciForAddr)
                            && ciForAddr.TryGetProperty("Address", out var addrProp))
                        {
                            deliveryAddress = addrProp.GetString();
                        }

                        order.SetOrderType(payloadOrderType, deliveryAddress, deliveryLat, deliveryLng, shippingFee);
                    }
                }

                // Bug 2 fix: parse CustomerNotes from payload and set on order.
                // Previously notes were dropped during PG→SQLite sync → kitchen/order list never showed them.
                if (root.TryGetProperty("CustomerNotes", out var notesProp))
                {
                    string? notes = notesProp.GetString();
                    if (!string.IsNullOrWhiteSpace(notes))
                    {
                        order.SetCustomerNotes(notes.Trim());
                    }
                }

                // Set device ID if provided
                if (root.TryGetProperty("CustomerDeviceId", out var devProp))
                {
                    string? deviceId = devProp.GetString();
                    if (!string.IsNullOrWhiteSpace(deviceId))
                        order.SetCustomerDeviceId(deviceId);
                }

                // NF-4: sync salesman referral attribution (CC-S4 fields added to the created
                // payload). Keeps the SQLite replica attributable so ShopERP-published
                // completed events carry salesmanId for the salesman push fan-out.
                // Strict parse — malformed ids throw and are logged, never silently nulled.
                if (root.TryGetProperty("SalesmanId", out var smProp) && smProp.ValueKind == JsonValueKind.String
                    && root.TryGetProperty("ReferralProductId", out var rpProp) && rpProp.ValueKind == JsonValueKind.String
                    && root.TryGetProperty("ReferralCode", out var rcProp))
                {
                    try
                    {
                        Guid salesmanId = smProp.GetGuid();
                        Guid referralProductId = rpProp.GetGuid();
                        string? referralCode = rcProp.GetString();
                        if (salesmanId != Guid.Empty && referralProductId != Guid.Empty && !string.IsNullOrWhiteSpace(referralCode))
                        {
                            order.SetSalesmanReferral(salesmanId, referralProductId, referralCode.Trim());
                        }
                    }
                    catch (Exception refEx)
                    {
                        _logger.LogWarning(refEx,
                            "OrderSyncSubscriber: malformed referral fields for order {OrderId} — referral attribution NOT synced", orderId);
                    }
                }

                // Set status if provided (default is "pending")
                if (root.TryGetProperty("Status", out var statusProp))
                {
                    string? status = statusProp.GetString();
                    if (!string.IsNullOrWhiteSpace(status) && status != "pending")
                    {
                        order.UpdateOrderStatus(new OrderStatusId(status));
                    }
                }

                await dbContext.Orders.AddAsync(order, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);

                // A: Broadcast OrderCreated via SignalR so Blazor UI auto-refreshes instantly.
                // Without this, UI only updates via 10s poll timer or manual F5.
                try
                {
                    await _hubContext.Clients.All.SendAsync("OrderCreated", orderId, tenantId, cancellationToken);
                    _logger.LogDebug("OrderSyncSubscriber: broadcast OrderCreated SignalR event for {OrderId}", orderId);
                }
                catch (Exception hubEx)
                {
                    _logger.LogWarning(hubEx, "OrderSyncSubscriber: SignalR broadcast failed for {OrderId} (order already in SQLite)", orderId);
                }

                // NF-4: "đơn mới" push to the tenant owner — owner subscribes via KhachLink
                // PWA, so PushSubscriptions (SQLite) keyed by CustomerId carries their
                // subscription. Source: payload OwnerCustomerId (resolved on Gateway),
                // fallback to the local Tenants row. Best-effort — never fails the sync.
                try
                {
                    Guid? ownerCustomerId = null;
                    if (root.TryGetProperty("OwnerCustomerId", out var ocProp) && ocProp.ValueKind != JsonValueKind.Null)
                    {
                        // Strict parse — malformed value throws → caught below, push skipped, error logged.
                        Guid oc = ocProp.GetGuid();
                        ownerCustomerId = oc == Guid.Empty ? null : oc;
                    }
                    if (ownerCustomerId == null)
                    {
                        ownerCustomerId = await dbContext.Tenants
                            .Where(t => t.Id == order.TenantId)
                            .Select(t => t.OwnerCustomerId)
                            .FirstOrDefaultAsync(cancellationToken);
                    }

                    if (ownerCustomerId.HasValue && ownerCustomerId != order.CustomerId)
                    {
                        var pushService = scope.ServiceProvider.GetService<VanAn.CoreHub.Services.PushNotificationService>();
                        if (pushService != null)
                        {
                            var (sent, _) = await pushService.SendBulkNotificationAsync(
                                new[] { ownerCustomerId.Value },
                                "Vạn An",
                                $"Đơn hàng mới #{orderId.ToString()[..8]} — {order.TotalAmount:N0}đ vừa được đặt",
                                "/");
                            _logger.LogInformation("OrderSyncSubscriber: dispatched {Sent} new-order push(es) to owner {OwnerId} for {OrderId}",
                                sent, ownerCustomerId, orderId);
                        }
                    }
                }
                catch (Exception pushEx)
                {
                    _logger.LogWarning(pushEx, "OrderSyncSubscriber: owner new-order push failed for {OrderId} (order already in SQLite)", orderId);
                }

                _logger.LogInformation("OrderSyncSubscriber: synced order {OrderId} → SQLite ({ItemCount} items, {Total} VND)",
                    orderId, items.Count, order.TotalAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OrderSyncSubscriber: failed to sync OrderCreated event");
            }
        }

        /// <summary>
        /// Sync OrderStatusChanged event → update order status in SQLite.
        /// Payload shape: { orderId, newStatus, ... }
        /// </summary>
        private async Task SyncOrderStatusChangedAsync(byte[] data, CancellationToken cancellationToken)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                Guid orderId = root.GetProperty("orderId").GetGuid();
                string newStatus = root.TryGetProperty("newStatus", out var ns) ? ns.GetString() ?? "" : "";

                if (string.IsNullOrEmpty(newStatus))
                {
                    _logger.LogWarning("OrderSyncSubscriber: missing newStatus for order {OrderId}", orderId);
                    return;
                }

                using IServiceScope scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();

                var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
                if (order == null)
                {
                    _logger.LogWarning("OrderSyncSubscriber: order {OrderId} not found in SQLite — skipping status update", orderId);
                    return;
                }

                // NF-4: persist shipper assignment carried on the status event. Gateway sets
                // order.ShipperId when a shipper accepts (→ delivering); without syncing it the
                // SQLite replica never knows the assignee and a later owner-cancel cannot
                // fan out the shipper push. Strict parse — malformed value throws (outer catch).
                if (root.TryGetProperty("assignedShipperId", out var shProp) && shProp.ValueKind != JsonValueKind.Null)
                {
                    Guid shipperId = shProp.GetGuid();
                    if (shipperId != Guid.Empty && order.ShipperId != shipperId)
                    {
                        order.AssignShipper(shipperId);
                    }
                }

                if (order.Status.Value != newStatus)
                {
                    string oldStatus = order.Status.Value;
                    order.UpdateOrderStatus(new OrderStatusId(newStatus));
                    if (newStatus == "completed")
                    {
                        order.MarkAsCompleted();

                        // Fix: Update customer stats (LastOrderDate + TotalSpent) in SQLite when
                        // order is completed via Gateway (PG). Without this, the SQLite customer
                        // record never gets stats updated — admin/customers shows TotalSpent=0
                        // and LastOrderDate=null for all customers whose orders completed via Gateway.
                        // (HandleOrderCompletedAsync only runs in the context where TransitionStatusAsync
                        // is called — if Gateway completes the order, only PG customer is updated.)
                        if (order.CustomerId.HasValue && order.CustomerId.Value != Guid.Empty)
                        {
                            var customer = await dbContext.Customers
                                .IgnoreQueryFilters()
                                .FirstOrDefaultAsync(c => c.Id == order.CustomerId.Value, cancellationToken);
                            if (customer != null)
                            {
                                customer.UpdateOrderStats(DateTime.UtcNow, order.TotalAmount);
                                _logger.LogInformation(
                                    "OrderSyncSubscriber: updated customer {CustomerId} stats on order {OrderId} completion (TotalSpent+={Amount})",
                                    customer.Id, orderId, order.TotalAmount);
                            }
                        }
                    }
                    await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("OrderSyncSubscriber: synced order {OrderId} status → {Status} in SQLite", orderId, newStatus);

                    // NF-2: Broadcast OrderStatusChanged so staff pages (Orders/Kitchen/Dashboard)
                    // refresh in realtime when status changes arrive from Gateway (e.g. shipper
                    // accept → delivering, delivered → completed). Without this, synced status
                    // updates were invisible until the 10-30s poll timer.
                    try
                    {
                        await _hubContext.Clients.All.SendAsync("OrderStatusChanged",
                            orderId, order.TenantId.Value, oldStatus, newStatus, cancellationToken);
                    }
                    catch (Exception hubEx)
                    {
                        _logger.LogWarning(hubEx, "OrderSyncSubscriber: SignalR status broadcast failed for {OrderId}", orderId);
                    }
                }
                else if (dbContext.ChangeTracker.HasChanges())
                {
                    // Status already current but shipper assignment (or other fields) changed.
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OrderSyncSubscriber: failed to sync OrderStatusChanged event");
            }
        }

        public override void Dispose()
        {
            _subscriptionConnection?.Close();
            _subscriptionConnection?.Dispose();
            base.Dispose();
        }
    }
}
