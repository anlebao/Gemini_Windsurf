namespace VanAn.CoreHub.Services;

/// <summary>
/// DTO for shop feature toggle settings (6 toggles).
/// Used by API controllers and KhachLink HTTP service.
/// </summary>
public record ShopFeatureSettingsDto
{
    public bool QR_TableNumber_Enabled { get; set; }
    public bool Kitchen_Workflow_Enabled { get; set; } = true;
    public bool Voice_Note_Enabled { get; set; }
    public bool Loyalty_Program_Enabled { get; set; } = true;
    public bool Accounting_Sync_Enabled { get; set; } = true;
    public bool EInvoice_Auto_Export_Enabled { get; set; }
}

/// <summary>
/// Read/write shop feature toggles per tenant.
/// Used by ShopERP Settings UI (direct inject) and API controller (for KhachLink HTTP).
/// </summary>
public interface IShopFeatureSettingsService
{
    /// <summary>Get feature settings for a tenant. Creates default if not exists.</summary>
    Task<ShopFeatureSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Update feature settings for a tenant. Creates if not exists.</summary>
    Task<ShopFeatureSettingsDto> UpdateSettingsAsync(Guid tenantId, ShopFeatureSettingsDto settings, CancellationToken ct = default);

    /// <summary>Check if a specific toggle is enabled for a tenant. Returns default if not configured.</summary>
    Task<bool> IsEnabledAsync(Guid tenantId, string toggleName, CancellationToken ct = default);
}
