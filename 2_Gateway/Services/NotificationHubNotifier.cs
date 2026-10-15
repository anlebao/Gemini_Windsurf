using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client;
using System.Text;
using System.Text.Json;
using VanAn.Gateway.Hubs;

namespace VanAn.Gateway.Services;

/// <summary>
/// Notification Hub (2026-10-15): hosted service — subscribe NATS "vanan.cloud.notification.created"
/// (publish bởi NotificationCenterService — CoreHub) → đẩy "UnreadCountChanged" tới NotificationsHub
/// group owner/customer để badge số chưa đọc cập nhật realtime. Fail-safe: mất event chỉ làm badge
/// trễ (UI có poll fallback khi mở trang). Chạy trong Gateway (nơi có IHubContext).
/// </summary>
public class NotificationHubNotifier : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationHubNotifier> _logger;
    private IConnection? _connection;

    public NotificationHubNotifier(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<NotificationHubNotifier> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string url = _configuration.GetValue<string>("Nats:Url")
            ?? _configuration.GetValue<string>("NATS__Url")
            ?? "nats://localhost:4222";

        int retryDelay = 2000;
        const int maxRetryDelay = 30000;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var opts = ConnectionFactory.GetDefaultOptions();
                opts.Url = url;
                opts.MaxReconnect = 5;
                opts.ReconnectWait = 2000;
                opts.Name = "vanan-gateway-notification-hub-notifier";

                _connection = new ConnectionFactory().CreateConnection(opts);
                _ = _connection.SubscribeAsync("vanan.cloud.notification.created", (sender, args) =>
                {
                    _ = HandleCreatedAsync(args.Message.Data, stoppingToken);
                });

                _logger.LogInformation("NotificationHubNotifier connected to NATS {Url}, subscribed to vanan.cloud.notification.created", url);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NotificationHubNotifier: NATS connection failed at {Url}. Retry in {Delay}ms", url, retryDelay);
            }

            try
            {
                await Task.Delay(retryDelay, stoppingToken);
                retryDelay = Math.Min(retryDelay * 2, maxRetryDelay);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("NotificationHubNotifier stopped.");
    }

    private async Task HandleCreatedAsync(byte[] data, CancellationToken ct)
    {
        try
        {
            string json = Encoding.UTF8.GetString(data);
            using var doc = JsonDocument.Parse(json);

            string audience = doc.RootElement.TryGetProperty("Audience", out var a) ? a.GetString() ?? "" : "";
            Guid? tenantId = TryGetGuid(doc, "TenantId");
            Guid? customerId = TryGetGuid(doc, "CustomerId");

            using var scope = _serviceProvider.CreateScope();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationsHub>>();

            if (audience == "owner" && tenantId.HasValue)
            {
                await hub.Clients.Group(NotificationGroups.Owner(tenantId.Value))
                    .SendAsync("UnreadCountChanged", tenantId.Value.ToString(), ct);
            }
            else if (audience == "customer" && customerId.HasValue)
            {
                await hub.Clients.Group(NotificationGroups.Customer(customerId.Value))
                    .SendAsync("UnreadCountChanged", customerId.Value.ToString(), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationHubNotifier: failed to push notification.created event");
        }
    }

    private static Guid? TryGetGuid(JsonDocument doc, string property)
    {
        if (doc.RootElement.TryGetProperty(property, out var el)
            && el.ValueKind == JsonValueKind.String
            && Guid.TryParse(el.GetString(), out var g))
            return g;
        return null;
    }

    public override void Dispose()
    {
        try { _connection?.Drain(); } catch { }
        try { _connection?.Dispose(); } catch { }
        base.Dispose();
    }
}
