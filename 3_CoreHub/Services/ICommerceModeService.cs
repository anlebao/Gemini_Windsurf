using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Sprint 7 — Commerce mode service. Get/set global + tenant override + resolve for order.
/// Used by CommunityAdminController (SystemAdmin JWT auth) + OrderService (resolve at creation).
/// </summary>
public interface ICommerceModeService
{
    /// <summary>Get global commerce mode + default rates + all tenant overrides.</summary>
    Task<CommerceModeSettingsDto> GetSettingsAsync();

    /// <summary>Set global commerce mode + default rates. Affects future orders only.</summary>
    Task SetGlobalModeAsync(CommerceMode mode, decimal platformFeeRate, decimal communityFundRate, decimal deliveryFee, Guid updatedBy);

    /// <summary>Set tenant override. Inherit = use global. Affects future orders only.</summary>
    Task SetTenantOverrideAsync(Guid tenantId, CommerceMode overrideMode, Guid updatedBy);

    /// <summary>Resolve effective mode for a tenant (override ≠ Inherit → override; else global).</summary>
    Task<CommerceMode> ResolveModeForTenantAsync(Guid tenantId);

    /// <summary>Get default rates (platform fee, community fund, delivery fee) from SystemSetting.</summary>
    Task<(decimal PlatformFeeRate, decimal CommunityFundRate, decimal DeliveryFee)> GetDefaultRatesAsync();
}

/// <summary>DTO for commerce mode settings (admin UI).</summary>
public class CommerceModeSettingsDto
{
    public CommerceMode GlobalMode { get; set; } = CommerceMode.Marketplace;
    public decimal DefaultPlatformFeeRate { get; set; } = 0.30m;
    public decimal DefaultCommunityFundRate { get; set; } = 0.05m;
    public decimal DefaultDeliveryFee { get; set; } = 15000m;
    public List<TenantOverrideDto> TenantOverrides { get; set; } = new();
}

/// <summary>DTO for tenant override row in admin table.</summary>
public class TenantOverrideDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public CommerceMode Override { get; set; } = CommerceMode.Inherit;
    public CommerceMode ResolvedMode { get; set; } = CommerceMode.Marketplace;
}
