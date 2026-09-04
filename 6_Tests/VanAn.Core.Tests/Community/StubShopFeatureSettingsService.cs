using VanAn.Shared.Services;

namespace VanAn.Core.Tests.Community;

/// <summary>
/// R2.1 (2026-09-04): Stub IShopFeatureSettingsService for unit tests.
/// Returns configurable per-tenant ShopFeatureSettingsDto.
/// Defaults: 1000 points + Verified (backward compat).
/// Tests can override via SetTenantSettings() to test per-tenant thresholds.
/// </summary>
public class StubShopFeatureSettingsService : IShopFeatureSettingsService
{
    private readonly Dictionary<Guid, ShopFeatureSettingsDto> _settings = new();

    public Task<ShopFeatureSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (_settings.TryGetValue(tenantId, out var s))
            return Task.FromResult(s);
        // Default: backward compat (1000 points + Verified)
        return Task.FromResult(new ShopFeatureSettingsDto());
    }

    public Task<ShopFeatureSettingsDto> UpdateSettingsAsync(Guid tenantId, ShopFeatureSettingsDto settings, CancellationToken ct = default)
    {
        _settings[tenantId] = settings;
        return Task.FromResult(settings);
    }

    public Task<bool> IsEnabledAsync(Guid tenantId, string toggleName, CancellationToken ct = default)
    {
        var s = GetSettingsAsync(tenantId, ct).GetAwaiter().GetResult();
        return Task.FromResult(toggleName switch
        {
            nameof(ShopFeatureSettingsDto.Loyalty_Program_Enabled) => s.Loyalty_Program_Enabled,
            _ => false
        });
    }

    /// <summary>Configure per-tenant thresholds for testing.</summary>
    public void SetTenantThresholds(Guid tenantId, int salesmanMinPoints, int shipperMinPoints, int requiredIdentityLevel)
    {
        _settings[tenantId] = new ShopFeatureSettingsDto
        {
            Community_SalesmanMinPoints = salesmanMinPoints,
            Community_ShipperMinPoints = shipperMinPoints,
            Community_RequiredIdentityLevel = requiredIdentityLevel
        };
    }
}
