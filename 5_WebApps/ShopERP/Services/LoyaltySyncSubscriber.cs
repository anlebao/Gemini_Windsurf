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
using VanAn.ShopERP.Infrastructure;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Loyalty Alliance Phase 2C: Subscribes to NATS "vanan.cloud.loyalty.changed.*" events
    /// published by AllianceWalletService (Gateway/CoreHub). Updates local SQLite LoyaltyRewards.PointBalance
    /// so ShopERP UI reflects the cross-tenant wallet balance in real time.
    ///
    /// Flow: AllianceWalletService mutates PG wallet → publishes NATS → this subscriber → SQLite LoyaltyRewards update.
    /// Subject: vanan.cloud.loyalty.changed.{customerDeviceId} (wildcard subscription — all devices).
    /// Payload shape (from AllianceWalletService.PublishLoyaltyChangedAsync):
    ///   { customerDeviceId, pointBalance, updatedAt }
    ///
    /// Pattern: same as OrderSyncSubscriber. Uses scoped ShopERPDbContext per message (background service
    /// singleton + scoped DbContext). Idempotent: if no local LoyaltyRewards row exists for the device,
    /// the message is logged + skipped (customer may not have shopped at this tenant yet).
    /// </summary>
    public class LoyaltySyncSubscriber : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoyaltySyncSubscriber> _logger;
        private IConnection? _subscriptionConnection;

        public LoyaltySyncSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<LoyaltySyncSubscriber> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wildcard subject: vanan.cloud.loyalty.changed.{customerDeviceId}
            // All loyalty balance changes for all devices are received — subscriber filters by local customer existence.
            string subject = "vanan.cloud.loyalty.changed.>";

            string url = _configuration.GetValue<string>("Nats:Url")
                ?? _configuration.GetValue<string>("NATS:Url")
                ?? _configuration.GetValue<string>("NATS__Url")
                ?? _configuration.GetValue<string>("ConnectionStrings:Nats")
                ?? "nats://localhost:4222";

            try
            {
                _subscriptionConnection = CreateSubscriptionConnection(url);
                _ = _subscriptionConnection.SubscribeAsync(subject, async (sender, args) =>
                {
                    await SyncLoyaltyBalanceAsync(args.Message.Data, stoppingToken);
                });
                RecordSubscription(subject);

                _logger.LogInformation(
                    "LoyaltySyncSubscriber connected to NATS {Url}, subscribed to {Subject}",
                    url, subject);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "LoyaltySyncSubscriber: NATS unavailable at {Url}. Running in degraded mode — sync will resume when NATS is available. Subject: {Subject}",
                    url, subject);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Sync loyalty balance from PG (Gateway) → SQLite (ShopERP).
        /// Finds local LoyaltyRewards by Customer.DeviceId, updates PointBalance to match PG wallet.
        /// </summary>
        internal async Task SyncLoyaltyBalanceAsync(byte[] data, CancellationToken cancellationToken)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                Guid customerDeviceId = root.GetProperty("customerDeviceId").GetGuid();
                int pointBalance = root.GetProperty("pointBalance").GetInt32();

                // Loyalty Consistency Fix Phase 3 (BUG #9): optional extended fields for history sync
                string? type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                int? points = root.TryGetProperty("points", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;
                string? reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null;
                string? updatedAtStr = root.TryGetProperty("updatedAt", out var u) ? u.GetString() : null;

                using IServiceScope scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();

                // Find local LoyaltyRewards by joining Customer.DeviceId → LoyaltyRewards.CustomerId
                var rewards = await (from c in dbContext.Customers.IgnoreQueryFilters()
                                     join lr in dbContext.LoyaltyRewards.IgnoreQueryFilters()
                                         on c.Id equals lr.CustomerId
                                     where c.DeviceId == customerDeviceId && !c.IsDeleted
                                     select lr).FirstOrDefaultAsync(cancellationToken);

                if (rewards == null)
                {
                    // Customer has not shopped at this tenant — no local LoyaltyRewards row to update.
                    _logger.LogDebug("LoyaltySyncSubscriber: no local LoyaltyRewards for device {DeviceId} — skipping", customerDeviceId);
                    return;
                }

                bool changed = false;

                // BUG #9: append history entry when extended fields present (idempotent — skip duplicates)
                if (type is not null && points.HasValue && reason is not null && updatedAtStr is not null)
                {
                    var history = DeserializeHistory(rewards.History);
                    DateTime ts = DateTime.Parse(updatedAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
                    // Idempotency: same timestamp + points + reason → already synced (skip duplicate)
                    bool exists = history.Any(h => h.Timestamp == ts && h.Points == points.Value && h.Reason == reason);
                    if (!exists)
                    {
                        history.Add(new LoyaltyHistoryEntry
                        {
                            Type = type,
                            Points = points.Value,
                            Reason = reason,
                            Timestamp = ts,
                            BalanceAfter = pointBalance
                        });
                        typeof(LoyaltyRewards)
                            .GetProperty(nameof(LoyaltyRewards.History))!
                            .SetValue(rewards, JsonSerializer.Serialize(history));
                        changed = true;
                        _logger.LogInformation("LoyaltySyncSubscriber: appended history entry for device {DeviceId} (type={Type}, points={Points})", customerDeviceId, type, points);
                    }
                }

                // Update PointBalance via reflection (PG source of truth sync)
                if (rewards.PointBalance != pointBalance)
                {
                    typeof(LoyaltyRewards)
                        .GetProperty(nameof(LoyaltyRewards.PointBalance))!
                        .SetValue(rewards, pointBalance);
                    changed = true;
                }

                if (changed)
                {
                    _ = await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("LoyaltySyncSubscriber: synced device {DeviceId} → balance={Balance}", customerDeviceId, pointBalance);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoyaltySyncSubscriber: failed to sync loyalty balance from NATS message");
            }
        }

        private static List<LoyaltyHistoryEntry> DeserializeHistory(string? json)
        {
            try { return JsonSerializer.Deserialize<List<LoyaltyHistoryEntry>>(json ?? "[]") ?? new(); }
            catch { return new(); }
        }

        /// <summary>
        /// Creates the NATS subscription connection. Extracted as protected virtual
        /// to enable testing without a real NATS server (test subclass overrides).
        /// </summary>
        protected virtual IConnection CreateSubscriptionConnection(string url)
        {
            var opts = ConnectionFactory.GetDefaultOptions();
            opts.Url = url;
            opts.MaxReconnect = 5;
            opts.ReconnectWait = 2000;
            opts.Name = "vanan-shoperp-loyalty-sync-subscriber";
            return new ConnectionFactory().CreateConnection(opts);
        }

        /// <summary>
        /// Records a subscribed subject string. Test subclasses override to capture
        /// the subject for assertion. Production implementation is a no-op.
        /// </summary>
        protected virtual void RecordSubscription(string subject) { }

        public override void Dispose()
        {
            _subscriptionConnection?.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
