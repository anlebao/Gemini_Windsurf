using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// REQ-1.2: Gateway impl — reads/writes SystemSetting in PG.
    /// ShopERP uses BackgroundServiceToggleApiClient (HTTP proxy to Gateway API).
    /// 30s memory cache to avoid DB query on every poll cycle.
    /// Uses IServiceScopeFactory (singleton-safe) to create scope per DB operation.
    /// </summary>
    public class BackgroundServiceToggleService : IBackgroundServiceToggleService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        // Known services — used by GetAllAsync to return full list even if no SystemSetting row exists
        private static readonly (string Name, string Display, string Desc, string Vps)[] KnownServices =
        [
            ("EInvoiceSyncSubscriber", "E-Invoice Sync Subscriber", "NATS subscriber — e-invoice sync-back from ShopERP to PG", "Gateway"),
            ("CoolingPeriodJob", "Cooling Period Job", "Auto-approves SalesReferral + AppInstallAttribution after 24h", "Gateway"),
            ("BirthdayBonusJob", "Birthday Bonus Job", "Daily — awards birthday bonus points + sends notification", "ShopERP"),
            ("VoucherExpiryReminderJob", "Voucher Expiry Reminder", "Daily — sends push notification for vouchers expiring soon", "ShopERP"),
            ("PromoCampaignJob", "Promo Campaign Job", "Polls 30s — sends bulk push notifications for pending campaigns", "ShopERP"),
            ("LoyaltySyncSubscriber", "Loyalty Sync Subscriber", "NATS subscriber — syncs cross-tenant Alliance wallet balance to SQLite", "ShopERP"),
            // VALCN v2.0 Phase 3 — Loyalty budget reset jobs (Gateway, PG is source of truth)
            ("LoyaltyBudgetDailyResetJob", "Loyalty Budget Daily Reset", "Daily 00:00 UTC — resets PointsIssuedToday to 0 for all tenants", "Gateway"),
            ("LoyaltyBudgetMonthlyResetJob", "Loyalty Budget Monthly Reset", "1st of month 00:00 UTC — resets PointsIssuedThisMonth to 0 for all tenants", "Gateway"),
        ];

        public BackgroundServiceToggleService(IServiceScopeFactory scopeFactory, IMemoryCache cache)
        {
            _scopeFactory = scopeFactory;
            _cache = cache;
        }

        public async Task<bool> IsEnabledAsync(string serviceName, CancellationToken ct = default)
        {
            string cacheKey = $"bg_toggle_{serviceName}";
            if (_cache.TryGetValue(cacheKey, out bool cached))
                return cached;

            string settingKey = $"BackgroundServices:Enable{serviceName}";
            string? value = null;
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
                var setting = await dbContext.SystemSettings
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == settingKey, ct);
                value = setting?.Value;
            }

            bool enabled = value != "false"; // default: enabled
            _cache.Set(cacheKey, enabled, CacheTtl);
            return enabled;
        }

        public async Task<IReadOnlyList<BackgroundServiceToggleDto>> GetAllAsync(CancellationToken ct = default)
        {
            Dictionary<string, string> settings;
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
                settings = await dbContext.SystemSettings
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(s => s.Key.StartsWith("BackgroundServices:Enable"))
                    .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
            }

            return KnownServices.Select(s => new BackgroundServiceToggleDto(
                s.Name,
                s.Display,
                s.Desc,
                s.Vps,
                settings.GetValueOrDefault($"BackgroundServices:Enable{s.Name}") != "false"
            )).ToList();
        }

        public async Task SetEnabledAsync(string serviceName, bool enabled, Guid updatedBy, CancellationToken ct = default)
        {
            string settingKey = $"BackgroundServices:Enable{serviceName}";
            string value = enabled ? "true" : "false";

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
                var setting = await dbContext.SystemSettings
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Key == settingKey, ct);

                if (setting == null)
                {
                    setting = new SystemSetting(new(Guid.Empty), settingKey, value, updatedBy);
                    dbContext.SystemSettings.Add(setting);
                }
                else
                {
                    setting.Update(value, updatedBy);
                }

                await dbContext.SaveChangesAsync(ct);
            }

            // Invalidate cache
            _cache.Remove($"bg_toggle_{serviceName}");
        }
    }
}
