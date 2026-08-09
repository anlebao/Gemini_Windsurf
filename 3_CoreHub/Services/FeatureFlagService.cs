using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 feature flag service — copy pattern from BackgroundServiceToggleService.
/// CRITICAL DIFFERENCE: default = false (disabled), not true.
/// Uses IServiceScopeFactory (singleton-safe) + 30s memory cache.
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    // Known features — used by GetAllAsync to return full list even if no SystemSetting row exists
    private static readonly (string Name, string Display, string Desc, string Phase)[] KnownFeatures =
    [
        ("ValcnV2_PlatformFee", "Platform Fee (Marketplace)", "Tính PlatformFeeAmount trên Marketplace orders (Phase 2)", "Phase 2"),
        ("ValcnV2_LoyaltyBudget", "Loyalty Budget Cap", "Check budget trước AddPoints + reset jobs (Phase 3)", "Phase 3"),
        ("ValcnV2_RefundReversal", "Refund Reversal (UC-06)", "4-step reversal on order cancel (Phase 4)", "Phase 4"),
    ];

    public FeatureFlagService(IServiceScopeFactory scopeFactory, IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken ct = default)
    {
        string cacheKey = $"feat_flag_{featureName}";
        if (_cache.TryGetValue(cacheKey, out bool cached))
            return cached;

        string settingKey = $"Features:Enable{featureName}";
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

        // CRITICAL: default = false (disabled) — opposite of BackgroundServiceToggleService
        bool enabled = value == "true";
        _cache.Set(cacheKey, enabled, CacheTtl);
        return enabled;
    }

    public async Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync(CancellationToken ct = default)
    {
        Dictionary<string, string> settings;
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
            settings = await dbContext.SystemSettings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.Key.StartsWith("Features:Enable"))
                .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        }

        return KnownFeatures.Select(f => new FeatureFlagDto(
            f.Name,
            f.Display,
            f.Desc,
            f.Phase,
            settings.GetValueOrDefault($"Features:Enable{f.Name}") == "true"
        )).ToList();
    }

    public async Task SetEnabledAsync(string featureName, bool enabled, Guid updatedBy, CancellationToken ct = default)
    {
        string settingKey = $"Features:Enable{featureName}";
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
        _cache.Remove($"feat_flag_{featureName}");
    }
}
