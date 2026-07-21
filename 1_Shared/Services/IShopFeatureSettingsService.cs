namespace VanAn.Shared.Services;

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
    public bool VAT_Display_Enabled { get; set; } = true;
    /// <summary>Phase 5: When ON, KhachLink validates QR/cart price against current product price
    /// before checkout. Prevents stale QR codes from charging wrong amount. Default OFF (trust QR).</summary>
    public bool Price_Validation_Enabled { get; set; }
    public int PollingIntervalSeconds { get; set; } = 15;

    /// <summary>Tenant Profile Page (2026-07-21): show Campaign section on /store/{slug}.</summary>
    public bool Campaign_Section_Enabled { get; set; } = true;
    /// <summary>Tenant Profile Page (2026-07-21): show VibeShowcase (product showcase) section.</summary>
    public bool VibeShowcase_Section_Enabled { get; set; } = true;
    /// <summary>Tenant Profile Page (2026-07-21): show Google Maps embed section.</summary>
    public bool GoogleMap_Section_Enabled { get; set; } = true;
    /// <summary>Tenant Profile Page (2026-07-21): show Social Hub section (Facebook/TikTok embeds).</summary>
    public bool SocialHub_Section_Enabled { get; set; } = true;
    /// <summary>Tenant Profile Page (2026-07-21): enable AI Chatbox widget. Default OFF (owner opts in).</summary>
    public bool AIChat_Enabled { get; set; }
}

/// <summary>
/// Phase 5: Result of price validation check (GET /api/products/{id}/validate-price).
/// </summary>
public class PriceValidationResult
{
    public Guid ProductId { get; set; }
    public bool Match { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal CurrentUnitPrice { get; set; }
    public decimal CurrentVatRate { get; set; }
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
