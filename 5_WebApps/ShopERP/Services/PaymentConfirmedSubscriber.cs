using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using System.Text;
using System.Text.Json;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP.Infrastructure;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Phase 3.5: Subscribes to NATS "vanan.cloud.order.payment.confirmed.>" events published by Gateway's NatsSyncWorker.
    /// When Gateway webhook marks order as Paid → enqueues OrderPaymentConfirmed Outbox event → NATS → this subscriber.
    ///
    /// Flow:
    ///   KhachLink payment → Gateway WebhookController → OrderService.MarkPaidAsync (PG status=Paid + Outbox event)
    ///   → NatsSyncWorker → NATS vanan.cloud.order.payment.confirmed.{shopInstanceId}
    ///   → this subscriber → SQLite (set status=Paid + GenerateAccountingEntriesAsync + e-invoice)
    ///
    /// Single source of truth for accounting entries: ShopERP SQLite (not Gateway PG).
    /// </summary>
    public class PaymentConfirmedSubscriber : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentConfirmedSubscriber> _logger;
        private IConnection? _subscriptionConnection;

        public PaymentConfirmedSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<PaymentConfirmedSubscriber> logger)
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
                opts.Name = "vanan-shoperp-payment-confirmed-subscriber";

                _subscriptionConnection = new ConnectionFactory().CreateConnection(opts);

                // Subscribe to payment.confirmed with routing key wildcard (vanan.cloud.order.payment.confirmed.{shopInstanceId})
                _ = _subscriptionConnection.SubscribeAsync("vanan.cloud.order.payment.confirmed.>", async (sender, args) =>
                {
                    await HandlePaymentConfirmedAsync(args.Message.Data, stoppingToken);
                });

                _logger.LogInformation(
                    "PaymentConfirmedSubscriber connected to NATS {Url}, subscribed to vanan.cloud.order.payment.confirmed.>",
                    url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PaymentConfirmedSubscriber: NATS unavailable at {Url}. Running in degraded mode — sync will resume when NATS is available.",
                    url);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle OrderPaymentConfirmed event from Gateway.
        /// Payload: { eventId, orderId, tenantId, transactionId, paymentMethod, paidAt }
        /// </summary>
        private async Task HandlePaymentConfirmedAsync(byte[] data, CancellationToken cancellationToken)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                Guid orderId = root.GetProperty("OrderId").GetGuid();
                Guid tenantId = root.GetProperty("TenantId").GetGuid();
                string transactionId = root.GetProperty("TransactionId").GetString() ?? "";
                string paymentMethod = root.GetProperty("PaymentMethod").GetString() ?? "Cash";

                _logger.LogInformation("PaymentConfirmedSubscriber: received event for order {OrderId}, tenant {TenantId}", orderId, tenantId);

                using IServiceScope scope = _serviceProvider.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
                var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();

                // Set tenant context for SQLite query filter
                tenantProvider.SetTenant(tenantId);

                // Retry loop: load order from SQLite (should already exist via OrderSyncSubscriber)
                Order? order = null;
                for (int i = 0; i < 5; i++)
                {
                    order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
                    if (order != null) break;
                    _logger.LogWarning("PaymentConfirmedSubscriber: order {OrderId} not yet in SQLite (attempt {Attempt}/5) — retrying in 2s", orderId, i + 1);
                    await Task.Delay(2000, cancellationToken);
                }

                if (order == null)
                {
                    _logger.LogError("PaymentConfirmedSubscriber: order {OrderId} not found in SQLite after 5 retries — dead-lettering", orderId);
                    return;
                }

                // If order not yet Paid in SQLite, mark it
                if (order.PaymentStatus != "Paid")
                {
                    order.ConfirmPayment(transactionId, paymentMethod);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("PaymentConfirmedSubscriber: order {OrderId} marked as Paid in SQLite", orderId);
                }
                else
                {
                    _logger.LogInformation("PaymentConfirmedSubscriber: order {OrderId} already Paid in SQLite (idempotent)", orderId);
                }

                // Generate accounting entries (idempotent — checks JournalEntry.Reference)
                bool accountingEnabled = true;
                var shopFeatureSettingsService = scope.ServiceProvider.GetService<IShopFeatureSettingsService>();
                if (shopFeatureSettingsService != null)
                {
                    try
                    {
                        accountingEnabled = await shopFeatureSettingsService.IsEnabledAsync(
                            tenantId,
                            nameof(ShopFeatureSettingsDto.Accounting_Sync_Enabled));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PaymentConfirmedSubscriber: Failed to fetch Accounting_Sync_Enabled — defaulting to ON");
                    }
                }

                if (accountingEnabled)
                {
                    try
                    {
                        await orderService.GenerateAccountingEntriesAsync(order, new TenantId(tenantId));
                        _logger.LogInformation("PaymentConfirmedSubscriber: accounting entries generated for order {OrderId}", orderId);
                    }
                    catch (Exception ex) when (ex is not KeyNotFoundException)
                    {
                        _logger.LogError(ex, "PaymentConfirmedSubscriber: accounting entry generation failed for order {OrderId}", orderId);
                    }
                }
                else
                {
                    _logger.LogInformation("PaymentConfirmedSubscriber: accounting sync disabled for tenant {TenantId} — skipping", tenantId);
                }

                // TODO Phase 6+: E-invoice submission if EInvoice_Auto_Export_Enabled toggle is ON
                // if (einvoiceEnabled) { await eInvoiceService.SubmitAsync(...); }
                // Enqueue EInvoiceSynced Outbox event for Gateway PG sync-back.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PaymentConfirmedSubscriber: failed to process payment confirmed event");
            }
        }
    }
}
