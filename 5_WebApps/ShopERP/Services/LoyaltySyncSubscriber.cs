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
        private readonly VanAn.CoreHub.Services.IBackgroundServiceToggleService _toggleService;
        private IConnection? _subscriptionConnection;

        public LoyaltySyncSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<LoyaltySyncSubscriber> logger,
            VanAn.CoreHub.Services.IBackgroundServiceToggleService toggleService)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _toggleService = toggleService;
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
        ///
        /// Loyalty Points Integrity (Batch 1) — extended payload from LoyaltyBalanceSyncPublisher:
        ///   { customerDeviceId?, customerId, tenantId, pointBalance, updatedAt, type, points, reason, sourceOrderId? }
        /// Resolution order:
        ///   1. (tenantId, customerId) — Silo events carry both → tenant-scoped row (fixes multi-tenant attribution).
        ///   2. Device join (legacy balance-only payload / Alliance events — customerId unknown).
        ///   3. Create the row if the customer exists locally but has no LoyaltyRewards row yet
        ///      (previously skipped → PG-earned points never appeared for fresh customers).
        /// Balance: MAX-merge (SQLite = max(SQLite, PG)) — preserves POS-only SQLite points during the
        /// interim window before PG becomes the sole authority (Batch 2); never regresses local balance.
        /// History: idempotent append (same timestamp + points + reason → skip).
        /// </summary>
        internal async Task SyncLoyaltyBalanceAsync(byte[] data, CancellationToken cancellationToken)
        {
            // REQ-1.2: Runtime toggle — skip if disabled via admin UI
            if (!await _toggleService.IsEnabledAsync("LoyaltySyncSubscriber", cancellationToken))
                return;

            try
            {
                string json = Encoding.UTF8.GetString(data);
                using JsonDocument doc = JsonDocument.Parse(json);

                using IServiceScope scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
                try
                {
                    await SyncRowCoreAsync(dbContext, doc.RootElement, cancellationToken);
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // Race: EVERY event is delivered TWICE by design (direct NATS publish + Outbox
                    // fallback). Concurrent deliveries both saw "no stub" and both inserted → UNIQUE
                    // collision → whole batch rolled back (stub + rewards row + history + balance).
                    // Retry ONCE with a fresh context — the winner's rows are now visible and the
                    // merge is idempotent (history dedup by timestamp+points+reason, MAX-merge balance).
                    _logger.LogWarning(ex,
                        "LoyaltySyncSubscriber: UNIQUE race (double delivery) — retrying once with a fresh context for {Event}",
                        data.Length > 128 ? Encoding.UTF8.GetString(data).Substring(0, 128) : Encoding.UTF8.GetString(data));
                    using IServiceScope retryScope = _serviceProvider.CreateScope();
                    var retryDb = retryScope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
                    await SyncRowCoreAsync(retryDb, doc.RootElement, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoyaltySyncSubscriber: failed to sync loyalty balance from NATS message");
            }
        }

        /// <summary>
        /// Batch 2 hardening: single-delivery merge — (tenantId, customerId) match → device fallback →
        /// create stub + row → idempotent history append → MAX-merge balance. Extracted from
        /// SyncLoyaltyBalanceAsync so a UNIQUE-race retry can re-run it with a fresh context.
        /// </summary>
        private async Task SyncRowCoreAsync(ShopERPDbContext dbContext, JsonElement root, CancellationToken cancellationToken)
        {

                // Batch 1: customerDeviceId is optional (extended payload may carry customerId + tenantId instead)
                Guid? customerDeviceId = root.TryGetProperty("customerDeviceId", out var devProp) && devProp.ValueKind == JsonValueKind.String
                    ? Guid.Parse(devProp.GetString()!)
                    : null;
                Guid? customerId = root.TryGetProperty("customerId", out var cidProp) && cidProp.ValueKind == JsonValueKind.String
                    ? Guid.Parse(cidProp.GetString()!)
                    : null;
                Guid? tenantId = root.TryGetProperty("tenantId", out var tidProp) && tidProp.ValueKind == JsonValueKind.String
                    ? Guid.Parse(tidProp.GetString()!)
                    : null;
                int pointBalance = root.GetProperty("pointBalance").GetInt32();

                // Loyalty Consistency Fix Phase 3 (BUG #9): optional extended fields for history sync
                string? type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                int? points = root.TryGetProperty("points", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;
                string? reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null;
                string? updatedAtStr = root.TryGetProperty("updatedAt", out var u) ? u.GetString() : null;

                // 1) Preferred match: (tenantId, customerId) — Silo events. Guid.Empty customerId = absent (Alliance).
                LoyaltyRewards? rewards = null;
                if (tenantId.HasValue && customerId.HasValue && customerId.Value != Guid.Empty)
                {
                    var tenantIdValue = new TenantId(tenantId.Value);
                    rewards = await dbContext.LoyaltyRewards
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(r => r.TenantId == tenantIdValue && r.CustomerId == customerId.Value, cancellationToken);
                }

                // 2) Fallback: device join (legacy balance-only payload / Alliance without customerId)
                if (rewards == null && customerDeviceId.HasValue)
                {
                    rewards = await (from c in dbContext.Customers.IgnoreQueryFilters()
                                     join lr in dbContext.LoyaltyRewards.IgnoreQueryFilters()
                                         on c.Id equals lr.CustomerId
                                     where c.DeviceId == customerDeviceId.Value && !c.IsDeleted
                                     select lr).FirstOrDefaultAsync(cancellationToken);
                }

                // 3) Create the mirror row (customer stub + LoyaltyRewards) when the customer has no
                //    local row yet. Previously skipped → Gateway-earned points were invisible for
                //    guest orders completed on Gateway (stub customer lives in PG only) and for
                //    customers whose local row was never created (e.g. order completed before first
                //    profile view). Mirrors OrderSyncSubscriber Bug 4 stub pattern (single-identity).
                if (rewards == null && tenantId.HasValue && customerId.HasValue && customerId.Value != Guid.Empty)
                {
                    bool customerExists = await dbContext.Customers
                        .IgnoreQueryFilters()
                        .AnyAsync(c => c.Id == customerId.Value && !c.IsDeleted, cancellationToken);

                    if (!customerExists)
                    {
                        var stub = new Customer(new TenantId(tenantId.Value), "Khách hàng", "N/A");
                        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(stub, customerId.Value);
                        typeof(Customer).GetProperty(nameof(Customer.CustomerId))!.SetValue(stub, new CustomerId(customerId.Value));
                        stub.UpdateCustomerDetails("Khách hàng", "N/A", null, "Bronze", customerDeviceId, true);
                        _ = dbContext.Customers.Add(stub);
                        _logger.LogInformation(
                            "LoyaltySyncSubscriber: created customer stub {CustomerId} for loyalty mirror (tenant {TenantId}, device {DeviceId})",
                            customerId.Value, tenantId.Value, customerDeviceId?.ToString() ?? "n/a");
                    }

                    rewards = new LoyaltyRewards(new TenantId(tenantId.Value), customerId.Value);
                    typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(rewards, Guid.NewGuid());
                    _ = dbContext.LoyaltyRewards.Add(rewards);
                    _logger.LogInformation("LoyaltySyncSubscriber: created LoyaltyRewards row for customer {CustomerId} tenant {TenantId}",
                        customerId.Value, tenantId.Value);
                }

                if (rewards == null)
                {
                    // No local customer/row to update.
                    _logger.LogDebug("LoyaltySyncSubscriber: no local LoyaltyRewards for device {DeviceId} customer {CustomerId} — skipping",
                        customerDeviceId?.ToString() ?? "n/a", customerId?.ToString() ?? "n/a");
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
                        _logger.LogInformation("LoyaltySyncSubscriber: appended history entry for customer {CustomerId} (type={Type}, points={Points})",
                            customerId?.ToString() ?? customerDeviceId?.ToString() ?? "n/a", type, points);
                    }
                }

                // Loyalty Points Integrity (Batch 2+, BUG-1b fix): PG ledger is the SINGLE source
                // of truth — ALL loyalty writes (Silo + Alliance) route through the Gateway ledger
                // (POS writes go via the internal API; the SQLite→PG backfill ran before the Batch 2
                // cutover). The mirror therefore OVERWRITES the local balance with the authoritative
                // PG balance. Batch 1 used MAX-merge to protect POS points pre-cutover — keeping it
                // now would leave SPEND events unable to decrease the mirror (upward drift forever:
                // local 150 after earn, PG 70 after spend → mirror never converges to PG).
                if (pointBalance != rewards.PointBalance)
                {
                    typeof(LoyaltyRewards)
                        .GetProperty(nameof(LoyaltyRewards.PointBalance))!
                        .SetValue(rewards, pointBalance);
                    changed = true;
                    _logger.LogInformation("LoyaltySyncSubscriber: synced balance → {Balance} (customer {CustomerId})",
                        pointBalance, customerId?.ToString() ?? customerDeviceId?.ToString() ?? "n/a");
                }

                if (changed)
                {
                    _ = await dbContext.SaveChangesAsync(cancellationToken);
                }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // SQLite: SqliteException SqliteErrorCode 19 (SQLITE_CONSTRAINT — UNIQUE/PRIMARY KEY).
            // Keep a message fallback so any provider variant is covered.
            return ex.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 }
                || ex.InnerException?.Message?.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true
                || ex.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
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
