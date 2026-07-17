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
            string url = _configuration.GetValue<string>("Nats:Url")
                ?? _configuration.GetValue<string>("NATS:Url")
                ?? _configuration.GetValue<string>("NATS__Url")
                ?? _configuration.GetValue<string>("ConnectionStrings:Nats")
                ?? "nats://localhost:4222";

            try
            {
                var opts = ConnectionFactory.GetDefaultOptions();
                opts.Url = url;
                opts.MaxReconnect = 5;
                opts.ReconnectWait = 2000;
                opts.Name = "vanan-shoperp-order-sync-subscriber";

                _subscriptionConnection = new ConnectionFactory().CreateConnection(opts);

                // RC-2 fix: subscribe to vanan.cloud.* (PG→SQLite direction).
                // Gateway publishes with prefix "cloud"; ShopERP publishes with prefix "shoperp".
                // This prevents subject collision where Gateway would receive its own events back.
                _ = _subscriptionConnection.SubscribeAsync("vanan.cloud.order.created", async (sender, args) =>
                {
                    await SyncOrderCreatedAsync(args.Message.Data, stoppingToken);
                });

                // Also subscribe to order.statuschanged for status updates
                _ = _subscriptionConnection.SubscribeAsync("vanan.cloud.order.statuschanged", async (sender, args) =>
                {
                    await SyncOrderStatusChangedAsync(args.Message.Data, stoppingToken);
                });

                _logger.LogInformation(
                    "OrderSyncSubscriber connected to NATS {Url}, subscribed to order.created + order.statuschanged",
                    url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "OrderSyncSubscriber: NATS unavailable at {Url}. Running in degraded mode — sync will resume when NATS is available.",
                    url);
            }

            return Task.CompletedTask;
        }

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
                    var productIds = new List<(Guid ProductId, string ProductName, decimal VatRate)>();
                    foreach (var item in itemsProp.EnumerateArray())
                    {
                        Guid productId = item.GetProperty("ProductId").GetGuid();
                        string productName = item.TryGetProperty("ProductName", out var pnProp) ? pnProp.GetString() ?? "" : "";
                        decimal vatRate = item.TryGetProperty("VatRate", out var vrProp) ? vrProp.GetDecimal() : 0.10m;
                        productIds.Add((productId, productName, vatRate));
                    }

                    // Auto-create missing products as stubs (idempotent — skip if already exists)
                    foreach (var (productId, productName, vatRate) in productIds)
                    {
                        bool productExists = await dbContext.Products
                            .IgnoreQueryFilters()
                            .AnyAsync(p => p.Id == productId, cancellationToken);
                        if (!productExists)
                        {
                            var stub = new Product(tenantIdObj, productName, "Synced from Gateway", 0m, "Synced", true, null, vatRate, 0m);
                            typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(stub, productId);
                            typeof(Product).GetProperty("ProductId")!.SetValue(stub, new ProductId(productId));
                            _ = dbContext.Products.Add(stub);
                            _logger.LogInformation("OrderSyncSubscriber: auto-created product stub {ProductId} ({Name})", productId, productName);
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
