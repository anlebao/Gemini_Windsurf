using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using System.Text;
using System.Text.Json;

namespace VanAn.Gateway.Services
{
    /// <summary>
    /// Phase 3.5: Subscribes to NATS "vanan.shoperp.einvoice.synced.>" events published by ShopERP's NatsSyncWorker.
    /// When ShopERP submits e-invoice → publishes EInvoiceSynced event → this subscriber updates PG ElectronicInvoice table.
    ///
    /// Flow:
    ///   ShopERP PaymentConfirmedSubscriber → EInvoiceService.SubmitAsync → Outbox EInvoiceSynced event
    ///   → NatsSyncWorker → NATS vanan.shoperp.einvoice.synced.{shopInstanceId}
    ///   → this subscriber → Gateway PG ElectronicInvoice table updated
    ///
    /// Gateway admin can view e-invoice status without querying remote ShopERP.
    /// </summary>
    public class EInvoiceSyncSubscriber : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EInvoiceSyncSubscriber> _logger;
        private readonly CoreHub.Services.IBackgroundServiceToggleService _toggleService;
        private IConnection? _subscriptionConnection;

        public EInvoiceSyncSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<EInvoiceSyncSubscriber> logger,
            CoreHub.Services.IBackgroundServiceToggleService toggleService)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _toggleService = toggleService;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
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
                opts.Name = "vanan-gateway-einvoice-sync-subscriber";

                _subscriptionConnection = new ConnectionFactory().CreateConnection(opts);

                _ = _subscriptionConnection.SubscribeAsync("vanan.shoperp.einvoice.synced.>", async (sender, args) =>
                {
                    await HandleEInvoiceSyncedAsync(args.Message.Data, stoppingToken);
                });

                _logger.LogInformation(
                    "EInvoiceSyncSubscriber connected to NATS {Url}, subscribed to vanan.shoperp.einvoice.synced.>",
                    url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "EInvoiceSyncSubscriber: NATS unavailable at {Url}. Running in degraded mode.",
                    url);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle EInvoiceSynced event from ShopERP.
        /// Payload: { orderId, tenantId, invoiceId, providerInvoiceNumber, status, submittedAt }
        /// </summary>
        private async Task HandleEInvoiceSyncedAsync(byte[] data, CancellationToken cancellationToken)
        {
            // REQ-1.2: Runtime toggle — skip if disabled via admin UI
            if (!await _toggleService.IsEnabledAsync("EInvoiceSyncSubscriber", cancellationToken))
                return;

            try
            {
                string json = Encoding.UTF8.GetString(data);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                Guid orderId = root.GetProperty("OrderId").GetGuid();
                Guid tenantId = root.GetProperty("TenantId").GetGuid();
                string? providerInvoiceNumber = root.TryGetProperty("ProviderInvoiceNumber", out var pinProp) ? pinProp.GetString() : null;
                string? status = root.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() : "Submitted";

                _logger.LogInformation("EInvoiceSyncSubscriber: received event for order {OrderId}, tenant {TenantId}, status {Status}", orderId, tenantId, status);

                // Phase 3.5: Full ElectronicInvoice PG update deferred to Phase 6+ (needs ElectronicInvoice entity
                // + IVanAnDbContext.ElectronicInvoices DbSet wiring). For now, just log the sync-back event.
                // Gateway admin can view e-invoice status via ShopERP HTTP forwarding (Phase 6).
                _logger.LogInformation("EInvoiceSyncSubscriber: e-invoice sync-back logged for order {OrderId} (full PG update in Phase 6+)", orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EInvoiceSyncSubscriber: failed to process e-invoice synced event");
            }
        }
    }
}
