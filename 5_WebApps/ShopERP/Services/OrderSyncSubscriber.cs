using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using System.Text;
using System.Text.Json;
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
        private IConnection? _subscriptionConnection;

        public OrderSyncSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<OrderSyncSubscriber> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "OrderSyncSubscriber: NATS unavailable at {Url}. Running in degraded mode — sync will resume when NATS is available. Routed subjects: {CreatedSubject}, {StatusSubject}",
                    url, createdSubject, statusSubject);
            }

            return Task.CompletedTask;
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

                Order order = Order.Create(orderId, tenantIdObj, null, items);

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

                // Set device ID if provided
                if (root.TryGetProperty("CustomerDeviceId", out var devProp))
                {
                    string? deviceId = devProp.GetString();
                    if (!string.IsNullOrWhiteSpace(deviceId))
                        order.SetCustomerDeviceId(deviceId);
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

                if (order.Status.Value != newStatus)
                {
                    order.UpdateOrderStatus(new OrderStatusId(newStatus));
                    if (newStatus == "completed")
                        order.MarkAsCompleted();
                    await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("OrderSyncSubscriber: synced order {OrderId} status → {Status} in SQLite", orderId, newStatus);
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
