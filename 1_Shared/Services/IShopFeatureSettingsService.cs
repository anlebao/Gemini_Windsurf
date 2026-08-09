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

    // VALCN v2.0 Phase 1 — Per-tenant platform fee rate (default 5%, null = fallback to global 30%)
    public decimal? PlatformFeeRate { get; set; } = 0.05m;

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

    // === #100: KhachLink Home page section toggles (SystemAdmin controls which sections appear on home) ===
    /// <summary>#100.2: Show "Khuyến Mãi Cửa Hàng" (Campaign) section on KhachLink home. Default ON.</summary>
    public bool Home_CampaignSection_Enabled { get; set; } = true;
    /// <summary>#100.2: Show "Cửa Hàng Của Bạn" (Store info) section on KhachLink home. Default ON.</summary>
    public bool Home_StoreSection_Enabled { get; set; } = true;
    /// <summary>#100.2: Show "Sản Phẩm Nổi Bật" (Featured products) section on KhachLink home. Default ON.</summary>
    public bool Home_FeaturedSection_Enabled { get; set; } = true;
    /// <summary>#100.2: Show Social Hub (Facebook/TikTok links) on KhachLink home. Default ON.</summary>
    public bool Home_SocialHub_Enabled { get; set; } = true;

    // === Loyalty-C WS-A: Per-tenant loyalty points formula (overrides global IOptions<LoyaltyPointsConfig> default) ===
    /// <summary>Loyalty-C WS-A: Points rate (fraction of TotalAmount). 0 = use global default (appsettings.json). Default 0 = fallback.</summary>
    public decimal Loyalty_PointsRate { get; set; } = 0m;
    /// <summary>Loyalty-C WS-A: Min points awarded per order. 0 = use global default. Default 0 = fallback.</summary>
    public int Loyalty_MinPointsPerOrder { get; set; } = 0;
    /// <summary>Loyalty-C WS-A: Max points per order (null = no cap / use global default). Default null = fallback.</summary>
    public int? Loyalty_MaxPointsPerOrder { get; set; } = null;
    /// <summary>Loyalty-C WS-A: Award points on ALL orders (true) or only orders with TrackingCode (false). Default true.</summary>
    public bool Loyalty_AwardOnAllOrders { get; set; } = true;

    // === Loyalty-C WS-C: Per-tenant notification rules for loyalty events ===
    /// <summary>Loyalty-C WS-C: Push notification when customer completes a mission. Default ON.</summary>
    public bool Notify_MissionCompleted { get; set; } = true;
    /// <summary>Loyalty-C WS-C: Push notification on birthday bonus award. Default ON.</summary>
    public bool Notify_BirthdayBonus { get; set; } = true;
    /// <summary>Loyalty-C WS-C: Push notification when admin fulfills a redemption voucher. Default ON.</summary>
    public bool Notify_RedemptionFulfilled { get; set; } = true;
    /// <summary>Loyalty-C WS-C: Push notification when admin cancels a redemption + refunds points. Default ON.</summary>
    public bool Notify_RedemptionCancelled { get; set; } = true;
    /// <summary>Loyalty-C WS-C: Push notification when voucher is expiring soon. Default ON.</summary>
    public bool Notify_VoucherExpiringSoon { get; set; } = true;
    /// <summary>Loyalty-C WS-C: Hours before voucher expiry to send reminder push. Default 24. Range 1-168 (7 days).</summary>
    public int VoucherExpiryNotifyHours { get; set; } = 24;
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
