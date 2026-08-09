using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// REQ-1.2: Gateway impl — reads/writes SystemSetting in PG.
    /// ShopERP uses BackgroundServiceToggleApiClient (HTTP proxy to Gateway API).
    /// 30s memory cache to avoid DB query on every poll cycle.
    /// </summary>
    public class BackgroundServiceToggleService : IBackgroundServiceToggleService
    {
        private readonly IVanAnDbContext _dbContext;
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
        ];

        public BackgroundServiceToggleService(IVanAnDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        public async Task<bool> IsEnabledAsync(string serviceName, CancellationToken ct = default)
        {
            string cacheKey = $"bg_toggle_{serviceName}";
            if (_cache.TryGetValue(cacheKey, out bool cached))
                return cached;

            string settingKey = $"BackgroundServices:Enable{serviceName}";
            var setting = await _dbContext.SystemSettings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == settingKey, ct);

            bool enabled = setting?.Value != "false"; // default: enabled
            _cache.Set(cacheKey, enabled, CacheTtl);
            return enabled;
        }

        public async Task<IReadOnlyList<BackgroundServiceToggleDto>> GetAllAsync(CancellationToken ct = default)
        {
            // Load all toggle settings in one query
            var settings = await _dbContext.SystemSettings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.Key.StartsWith("BackgroundServices:Enable"))
                .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

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

            var setting = await _dbContext.SystemSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Key == settingKey, ct);

            if (setting == null)
            {
                setting = new SystemSetting(new(Guid.Empty), settingKey, value, updatedBy);
                _dbContext.SystemSettings.Add(setting);
            }
            else
            {
                setting.Update(value, updatedBy);
            }

            await _dbContext.SaveChangesAsync(ct);

            // Invalidate cache
            _cache.Remove($"bg_toggle_{serviceName}");
        }
    }
}
