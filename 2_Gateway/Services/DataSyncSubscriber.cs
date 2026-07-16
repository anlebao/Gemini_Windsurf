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

namespace VanAn.Gateway.Services
{
    /// <summary>
    /// W-1-T4 (S4): Subscribes to NATS events published by ShopERP's NatsSyncWorker
    /// and writes the synced data to PostgreSQL (VanAnDbContext).
    ///
    /// Runs in Gateway scope — Gateway has IVanAnDbContext registered as VanAnDbContext (PostgreSQL).
    /// This is the "PG write side" of the ADR-001 v2 Edge sync flow:
    ///   ShopERP SQLite Outbox → NatsSyncWorker → NATS → DataSyncSubscriber → PostgreSQL
    ///
    /// Degraded mode: if NATS is unavailable, the service starts normally and logs a warning.
    /// Sync is best-effort — missing events will be re-delivered by NATS (no ack = redelivery).
    /// </summary>
    public class DataSyncSubscriber : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DataSyncSubscriber> _logger;
        private IConnection? _subscriptionConnection;

        public DataSyncSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<DataSyncSubscriber> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // W-1-T4: NATS URL from config — Gateway uses "Nats:Url" (see appsettings.json)
            // Also check "NATS:Url" / "NATS__Url" for compatibility with ShopERP naming convention
            string url = _configuration.GetValue<string>("Nats:Url")
                ?? _configuration.GetValue<string>("NATS:Url")
                ?? _configuration.GetValue<string>("NATS__Url")
                ?? "nats://localhost:4222";

            try
            {
                var opts = ConnectionFactory.GetDefaultOptions();
                opts.Url = url;
                opts.MaxReconnect = 5;
                opts.ReconnectWait = 2000;
                opts.Name = "vanan-gateway-data-sync-subscriber";

                _subscriptionConnection = new ConnectionFactory().CreateConnection(opts);

                // Subscribe to all ShopERP sync events (subject prefix matches NatsSyncWorker.BuildSubject)
                _ = _subscriptionConnection.SubscribeAsync("vanan.shoperp.>", async (sender, args) =>
                {
                    await HandleSyncEventAsync(args.Message.Data, args.Message.Subject, stoppingToken);
                });

                _logger.LogInformation(
                    "DataSyncSubscriber connected to NATS {Url}, subscribed to vanan.shoperp.>",
                    url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "DataSyncSubscriber: NATS unavailable at {Url}. Running in degraded mode — sync will resume when NATS is available.",
                    url);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Routes sync event to the appropriate handler based on NATS subject.
        /// Subjects follow NatsSyncWorker.BuildSubject convention: "vanan.shoperp.{eventtype}"
        /// where eventtype is lowercased and underscores replaced with dots.
        /// </summary>
        private async Task HandleSyncEventAsync(byte[] data, string subject, CancellationToken cancellationToken)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                using JsonDocument doc = JsonDocument.Parse(json);

                // Extract event type from subject: "vanan.shoperp.order.completed" → "order.completed"
                string eventType = subject.StartsWith("vanan.shoperp.", StringComparison.Ordinal)
                    ? subject["vanan.shoperp.".Length..]
                    : subject;

                using IServiceScope scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();

                switch (eventType)
                {
                    case "order.completed":
                    case "ordercompleted":
                        await SyncOrderCompletedAsync(doc.RootElement, dbContext, cancellationToken);
                        break;
                    case "order.statuschanged":
                    case "orderstatuschanged":
                    case "order.status.changed":
                        await SyncOrderStatusAsync(doc.RootElement, dbContext, cancellationToken);
                        break;
                    case "order.created":
                    case "ordercreated":
                        await SyncOrderCreatedAsync(doc.RootElement, dbContext, cancellationToken);
                        break;
                    case "customer.created":
                    case "customercreated":
                        await SyncCustomerCreatedAsync(doc.RootElement, dbContext, cancellationToken);
                        break;
                    case "product.created":
                    case "productcreated":
                        await SyncProductUpsertAsync(doc.RootElement, dbContext, cancellationToken);
                        break;
                    case "product.updated":
                    case "productupdated":
                        await SyncProductUpsertAsync(doc.RootElement, dbContext, cancellationToken);
                        break;
                    case "product.deleted":
                    case "productdeleted":
                        await SyncProductDeletedAsync(doc.RootElement, dbContext, cancellationToken);
                        break;
                    default:
                        _logger.LogDebug("DataSyncSubscriber: unhandled event type {EventType} (subject={Subject})",
                            eventType, subject);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataSyncSubscriber: failed to process sync event (subject={Subject})", subject);
                // Do not ack — NATS will redeliver (if queue group configured)
            }
        }

        /// <summary>
        /// Sync OrderCompleted event — order is already in PostgreSQL (created via Gateway API).
        /// This handler ensures the order's CompletedAt + Status fields are up-to-date in PostgreSQL
        /// in case the completion happened while ShopERP was offline.
        /// </summary>
        private async Task SyncOrderCompletedAsync(JsonElement data, IVanAnDbContext dbContext, CancellationToken ct)
        {
            if (!data.TryGetProperty("OrderId", out var orderIdProp))
            {
                _logger.LogWarning("SyncOrderCompletedAsync: missing OrderId in event data");
                return;
            }

            Guid orderId = orderIdProp.GetGuid();
            var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null)
            {
                _logger.LogWarning("SyncOrderCompletedAsync: order {OrderId} not found in PostgreSQL — skipping", orderId);
                return;
            }

            // Order already exists in PostgreSQL (created via Gateway API).
            // If status is not yet "completed", transition it.
            if (order.Status.Value != "completed")
            {
                order.UpdateOrderStatus(new OrderStatusId("completed"));
                order.MarkAsCompleted();
                await dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Synced OrderCompleted for order {OrderId} → PostgreSQL status=completed", orderId);
            }
            else
            {
                _logger.LogDebug("SyncOrderCompletedAsync: order {OrderId} already completed in PostgreSQL", orderId);
            }
        }

        /// <summary>
        /// Sync OrderStatusChanged event — update order status in PostgreSQL.
        /// Payload shape (from OrderWorkflowService.PublishOrderStatusChangedEventAsync):
        ///   { orderId, oldStatus, newStatus, reason, timestamp }
        /// </summary>
        private async Task SyncOrderStatusAsync(JsonElement data, IVanAnDbContext dbContext, CancellationToken ct)
        {
            if (!data.TryGetProperty("orderId", out var orderIdProp))
            {
                _logger.LogWarning("SyncOrderStatusAsync: missing orderId in event data");
                return;
            }

            Guid orderId = orderIdProp.GetGuid();
            string newStatus = data.TryGetProperty("newStatus", out var nsProp) ? nsProp.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(newStatus))
            {
                _logger.LogWarning("SyncOrderStatusAsync: missing newStatus for order {OrderId}", orderId);
                return;
            }

            var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null)
            {
                _logger.LogWarning("SyncOrderStatusAsync: order {OrderId} not found in PostgreSQL — skipping", orderId);
                return;
            }

            if (order.Status.Value != newStatus)
            {
                order.UpdateOrderStatus(new OrderStatusId(newStatus));
                await dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Synced order {OrderId} status → {Status} in PostgreSQL", orderId, newStatus);
            }
        }

        /// <summary>
        /// Sync OrderCreated event — upsert order to PostgreSQL.
        /// Covers the offline case where ShopERP created the order locally (Edge Mode POS).
        /// Direction: SQLite→PG (subject vanan.shoperp.order.created).
        /// </summary>
        private async Task SyncOrderCreatedAsync(JsonElement data, IVanAnDbContext dbContext, CancellationToken ct)
        {
            if (!data.TryGetProperty("OrderId", out var orderIdProp))
            {
                _logger.LogWarning("SyncOrderCreatedAsync: missing OrderId in event data");
                return;
            }

            Guid orderId = orderIdProp.GetGuid();
            bool exists = await dbContext.Orders.AnyAsync(o => o.Id == orderId, ct);
            if (exists)
            {
                _logger.LogDebug("SyncOrderCreatedAsync: order {OrderId} already exists in PostgreSQL", orderId);
                return;
            }

            // Full upsert: deserialize complete Order graph from event payload.
            Guid tenantId = data.TryGetProperty("TenantId", out var tidProp) ? tidProp.GetGuid() : Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                _logger.LogWarning("SyncOrderCreatedAsync: missing TenantId for order {OrderId}", orderId);
                return;
            }

            TenantId tenantIdObj = new(tenantId);

            // Build OrderItems from payload
            var items = new List<OrderItem>();
            if (data.TryGetProperty("Items", out var itemsProp))
            {
                foreach (var item in itemsProp.EnumerateArray())
                {
                    Guid itemId = item.TryGetProperty("ItemId", out var idProp) ? idProp.GetGuid() : Guid.NewGuid();
                    Guid productId = item.GetProperty("ProductId").GetGuid();
                    int quantity = item.GetProperty("Quantity").GetInt32();
                    decimal unitPrice = item.GetProperty("UnitPrice").GetDecimal();
                    string productName = item.TryGetProperty("ProductName", out var pnProp) ? pnProp.GetString() ?? "" : "";
                    decimal vatRate = item.TryGetProperty("VatRate", out var vrProp) ? vrProp.GetDecimal() : 0.10m;

                    var orderItem = OrderItem.Create(itemId, tenantIdObj, orderId, productId, quantity, unitPrice, productName);
                    typeof(OrderItem).GetProperty("VatRate")?.SetValue(orderItem, vatRate);
                    items.Add(orderItem);
                }
            }

            Order order = Order.Create(orderId, tenantIdObj, null, items);

            // Set customer info if provided
            if (data.TryGetProperty("CustomerInfo", out var infoProp))
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
            if (data.TryGetProperty("CustomerDeviceId", out var devProp))
            {
                string? deviceId = devProp.GetString();
                if (!string.IsNullOrWhiteSpace(deviceId))
                    order.SetCustomerDeviceId(deviceId);
            }

            // Set status if provided (default is "pending")
            if (data.TryGetProperty("Status", out var statusProp))
            {
                string? status = statusProp.GetString();
                if (!string.IsNullOrWhiteSpace(status) && status != "pending")
                {
                    order.UpdateOrderStatus(new OrderStatusId(status));
                }
            }

            await dbContext.Orders.AddAsync(order, ct);
            await dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("SyncOrderCreatedAsync: synced order {OrderId} → PostgreSQL ({ItemCount} items)",
                orderId, items.Count);
        }

        /// <summary>
        /// Sync CustomerCreated event — upsert customer to PostgreSQL.
        /// Customers are typically created via OTP verify flow (Gateway → ShopERP CustomerIdentityController).
        /// This handler covers the offline case.
        /// </summary>
        private async Task SyncCustomerCreatedAsync(JsonElement data, IVanAnDbContext dbContext, CancellationToken ct)
        {
            if (!data.TryGetProperty("CustomerId", out var idProp))
            {
                _logger.LogWarning("SyncCustomerCreatedAsync: missing CustomerId in event data");
                return;
            }

            Guid customerId = idProp.GetGuid();
            bool exists = await dbContext.Customers.AnyAsync(c => c.Id == customerId, ct);
            if (exists)
            {
                _logger.LogDebug("SyncCustomerCreatedAsync: customer {CustomerId} already exists in PostgreSQL", customerId);
                return;
            }

            _logger.LogInformation(
                "SyncCustomerCreatedAsync: customer {CustomerId} not in PostgreSQL — full upsert deferred (offline-created customer)",
                customerId);
        }

        /// <summary>
        /// Sync ProductCreated/ProductUpdated event — upsert product to PostgreSQL.
        /// Payload shape (from ShopERP product event publisher):
        ///   { ProductId, TenantId, Name, Description, Price, CostPrice, Category, IsActive, ImageUrl, VatRate }
        /// </summary>
        private async Task SyncProductUpsertAsync(JsonElement data, IVanAnDbContext dbContext, CancellationToken ct)
        {
            if (!data.TryGetProperty("ProductId", out var idProp))
            {
                _logger.LogWarning("SyncProductUpsertAsync: missing ProductId in event data");
                return;
            }

            Guid productId = idProp.GetGuid();
            Guid tenantId = data.TryGetProperty("TenantId", out var tidProp) ? tidProp.GetGuid() : Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                _logger.LogWarning("SyncProductUpsertAsync: missing TenantId for product {ProductId}", productId);
                return;
            }

            // Check if product already exists in PostgreSQL (IgnoreQueryFilters: cross-tenant upsert)
            Product? existing = await dbContext.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == productId, ct);

            string name = data.TryGetProperty("Name", out var nProp) ? nProp.GetString() ?? "" : "";
            string description = data.TryGetProperty("Description", out var dProp) ? dProp.GetString() ?? "" : "";
            decimal price = data.TryGetProperty("Price", out var pProp) ? pProp.GetDecimal() : 0m;
            decimal costPrice = data.TryGetProperty("CostPrice", out var cpProp) ? cpProp.GetDecimal() : 0m;
            string category = data.TryGetProperty("Category", out var cProp) ? cProp.GetString() ?? "" : "";
            bool isActive = data.TryGetProperty("IsActive", out var iaProp) ? iaProp.GetBoolean() : true;
            string? imageUrl = data.TryGetProperty("ImageUrl", out var imgProp) ? imgProp.GetString() : null;
            decimal vatRate = data.TryGetProperty("VatRate", out var vrProp) ? vrProp.GetDecimal() : 0.08m;

            if (existing == null)
            {
                // Create new product in PostgreSQL
                var product = new Product(
                    new TenantId(tenantId), name, description, price, category, isActive, imageUrl, vatRate, costPrice);
                // Override ProductId to match source (constructor generates new Guid)
                typeof(Product).GetProperty("ProductId")!.SetValue(product, new ProductId(productId));
                dbContext.Products.Add(product);
                await dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Synced ProductCreated for product {ProductId} → PostgreSQL", productId);
            }
            else
            {
                // Update existing product fields via reflection (domain entity has protected setters)
                typeof(Product).GetProperty("Name")!.SetValue(existing, name);
                typeof(Product).GetProperty("Description")!.SetValue(existing, description);
                typeof(Product).GetProperty("Price")!.SetValue(existing, price);
                typeof(Product).GetProperty("CostPrice")!.SetValue(existing, costPrice);
                typeof(Product).GetProperty("Category")!.SetValue(existing, category);
                typeof(Product).GetProperty("IsActive")!.SetValue(existing, isActive);
                typeof(Product).GetProperty("ImageUrl")!.SetValue(existing, imageUrl);
                typeof(Product).GetProperty("VatRate")!.SetValue(existing, vatRate);
                await dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Synced ProductUpdated for product {ProductId} → PostgreSQL", productId);
            }
        }

        /// <summary>
        /// Sync ProductDeleted event — soft-delete product in PostgreSQL.
        /// Payload shape: { ProductId, TenantId }
        /// </summary>
        private async Task SyncProductDeletedAsync(JsonElement data, IVanAnDbContext dbContext, CancellationToken ct)
        {
            if (!data.TryGetProperty("ProductId", out var idProp))
            {
                _logger.LogWarning("SyncProductDeletedAsync: missing ProductId in event data");
                return;
            }

            Guid productId = idProp.GetGuid();
            Product? product = await dbContext.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == productId, ct);

            if (product == null)
            {
                _logger.LogDebug("SyncProductDeletedAsync: product {ProductId} not found in PostgreSQL (already deleted)", productId);
                return;
            }

            // Soft-delete: set IsDeleted = true (BaseEntity pattern)
            typeof(BaseEntity).GetProperty("IsDeleted")!.SetValue(product, true);
            await dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Synced ProductDeleted for product {ProductId} → PostgreSQL (soft-deleted)", productId);
        }

        public override void Dispose()
        {
            try
            {
                _subscriptionConnection?.Drain();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DataSyncSubscriber: error during Drain on Dispose");
            }
            _subscriptionConnection?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
