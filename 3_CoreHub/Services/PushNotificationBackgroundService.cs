using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using VanAn.CoreHub.Infrastructure.Messaging;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// FIX-BATCH-3: IHostedService that subscribes to NATS "order.status.changed" subject
    /// and triggers PushNotificationService.SendOrderStatusNotificationAsync for each event.
    ///
    /// Replaces the stub SubscribeToNatsAsync method on PushNotificationService
    /// which only logged a placeholder message.
    ///
    /// Degraded mode: if NATS is unavailable, the service starts normally and logs a warning.
    /// Push notifications are best-effort — missing them is not a workflow failure.
    /// </summary>
    public class PushNotificationBackgroundService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INatsEventPublisher? _natsPublisher;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PushNotificationBackgroundService> _logger;
        private IConnection? _subscriptionConnection;
        private IAsyncSubscription? _subscription;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        public PushNotificationBackgroundService(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<PushNotificationBackgroundService> logger,
            INatsEventPublisher? natsPublisher = null)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _natsPublisher = natsPublisher;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = new CancellationTokenSource();

            // Create a dedicated NATS connection for subscription (separate from publisher)
            string url = _configuration.GetValue<string>("NATS__Url") ?? "nats://localhost:4222";
            try
            {
                var opts = ConnectionFactory.GetDefaultOptions();
                opts.Url = url;
                opts.MaxReconnect = 5;
                opts.ReconnectWait = 2000;
                opts.Name = "vanan-push-notification-subscriber";
                _subscriptionConnection = new ConnectionFactory().CreateConnection(opts);

                _subscription = _subscriptionConnection.SubscribeAsync("order.status.changed", (sender, args) =>
                {
                    _ = HandleEventAsync(args.Message.Data, _cts.Token);
                });

                _logger.LogInformation("PushNotificationBackgroundService subscribed to NATS subject 'order.status.changed'");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PushNotificationBackgroundService: could not subscribe to NATS at {Url}. " +
                    "Push notifications will not be delivered until NATS is available. Service will run in degraded mode.",
                    url);
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts?.Cancel();

            try
            {
                _subscription?.Drain();
                _subscriptionConnection?.Drain();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "PushNotificationBackgroundService: error during drain on stop");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Parse the NATS payload and dispatch to PushNotificationService.
        /// </summary>
        private async Task HandleEventAsync(byte[] payload, CancellationToken cancellationToken)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                if (!root.TryGetProperty("orderId", out var orderIdEl) ||
                    !root.TryGetProperty("customerId", out var customerIdEl) ||
                    !root.TryGetProperty("newStatus", out var newStatusEl))
                {
                    _logger.LogWarning("PushNotificationBackgroundService: payload missing required fields (orderId/customerId/newStatus). Payload: {Payload}",
                        JsonSerializer.Serialize(root));
                    return;
                }

                Guid orderId = orderIdEl.GetGuid();
                Guid? customerId = customerIdEl.ValueKind == JsonValueKind.Null ? null : customerIdEl.GetGuid();
                string newStatus = newStatusEl.GetString() ?? "unknown";
                string? customerName = root.TryGetProperty("customerName", out var cn) && cn.ValueKind != JsonValueKind.Null ? cn.GetString() : null;

                if (customerId == null)
                {
                    _logger.LogDebug("PushNotificationBackgroundService: skipping push for OrderId={OrderId} (no customerId)", orderId);
                    return;
                }

                // Create a scope to resolve scoped PushNotificationService
                using var scope = _serviceProvider.CreateScope();
                var pushService = scope.ServiceProvider.GetService<PushNotificationService>();
                if (pushService == null)
                {
                    _logger.LogWarning("PushNotificationBackgroundService: PushNotificationService not registered in DI — skipping push");
                    return;
                }

                int sent = await pushService.SendOrderStatusNotificationAsync(customerId.Value, orderId, newStatus, customerName);
                _logger.LogInformation("PushNotificationBackgroundService dispatched {Sent} push notification(s) for OrderId={OrderId} Status={Status}",
                    sent, orderId, newStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PushNotificationBackgroundService: error handling NATS event");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts?.Dispose();
            try { _subscription?.Dispose(); } catch { }
            try { _subscriptionConnection?.Dispose(); } catch { }
        }
    }
}
