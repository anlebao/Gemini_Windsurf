using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// KhachLink Full Flow W0: EF persistence entity for the ShopFeatureSettings table.
/// Stores 6 feature toggles per tenant (shop) — admin can enable/disable business modules.
///
/// Inherits <see cref="BaseEntity"/> → gets <c>Id</c>, <c>TenantId</c>, audit fields, and
/// <c>IMustHaveTenant</c> (multi-tenancy query filter applies automatically).
/// Precedent: <see cref="PeriodClosingStatusEntity"/> (tenant-scoped Infrastructure entity).
/// </summary>
public class ShopFeatureSettingsEntity : BaseEntity
{
    /// <summary>Toggle: include table number in QR Code payload. Default: OFF.</summary>
    public bool QR_TableNumber_Enabled { get; private set; }

    /// <summary>Toggle: kitchen workflow (Nhận đơn → Đang chế biến → Sẵn sàng giao). Default: ON.</summary>
    public bool Kitchen_Workflow_Enabled { get; private set; } = true;

    /// <summary>Toggle: voice note (STT on client + TTS in kitchen). Default: OFF.</summary>
    public bool Voice_Note_Enabled { get; private set; }

    /// <summary>Toggle: loyalty program (OTP + points + PWA prompt). Default: ON.</summary>
    public bool Loyalty_Program_Enabled { get; private set; } = true;

    /// <summary>Toggle: auto-sync order data to HKD accounting. Default: ON.</summary>
    public bool Accounting_Sync_Enabled { get; private set; } = true;

    /// <summary>Toggle: auto-export e-invoice when order completed. Default: OFF (chờ sandbox Viettel/MISA).</summary>
    public bool EInvoice_Auto_Export_Enabled { get; private set; }

    /// <summary>Toggle: show VAT breakdown (Tạm tính / VAT / Tổng) on customer-facing UI. Default: ON.
    /// Small HKDs not issuing VAT invoices turn this OFF. Backend always computes VAT separately
    /// for future HKD→Công Ty migration.</summary>
    public bool VAT_Display_Enabled { get; private set; } = true;

    /// <summary>Phase 5: Toggle: validate QR/cart price against current product price before checkout.
    /// Default OFF (trust QR snapshot). When ON, KhachLink calls /api/products/{id}/validate-price.</summary>
    public bool Price_Validation_Enabled { get; private set; }

    /// <summary>Tenant Profile Page (2026-07-21): Toggle: show Campaign section on /store/{slug}.
    /// Default ON. Owner can hide if no active campaigns.</summary>
    public bool Campaign_Section_Enabled { get; private set; } = true;

    /// <summary>Tenant Profile Page (2026-07-21): Toggle: show VibeShowcase (product showcase) section.
    /// Default ON. Owner can hide if no products or wants minimal page.</summary>
    public bool VibeShowcase_Section_Enabled { get; private set; } = true;

    /// <summary>Tenant Profile Page (2026-07-21): Toggle: show Google Maps embed section.
    /// Default ON. Owner can hide if no physical store (online-only business).</summary>
    public bool GoogleMap_Section_Enabled { get; private set; } = true;

    /// <summary>Tenant Profile Page (2026-07-21): Toggle: show Social Hub section (Facebook/TikTok embeds).
    /// Default ON. Owner can hide if no social media presence.</summary>
    public bool SocialHub_Section_Enabled { get; private set; } = true;

    /// <summary>Tenant Profile Page (2026-07-21): Toggle: enable AI Chatbox widget on /store/{slug}.
    /// Default OFF. Owner opts in to AI-assisted customer service.</summary>
    public bool AIChat_Enabled { get; private set; }

    /// <summary>KhachLink OrderTracking polling interval in seconds. Default: 15. Range: 5-120.</summary>
    public int PollingIntervalSeconds { get; private set; } = 15;

    private ShopFeatureSettingsEntity() { } // EF Core materialization

    /// <summary>Factory: create with default toggle values for a tenant.</summary>
    public ShopFeatureSettingsEntity(TenantId tenantId) : base(tenantId)
    {
        QR_TableNumber_Enabled = false;
        Kitchen_Workflow_Enabled = true;
        Voice_Note_Enabled = false;
        Loyalty_Program_Enabled = true;
        Accounting_Sync_Enabled = true;
        EInvoice_Auto_Export_Enabled = false;
        VAT_Display_Enabled = true;
        Price_Validation_Enabled = false;
        Campaign_Section_Enabled = true;
        VibeShowcase_Section_Enabled = true;
        GoogleMap_Section_Enabled = true;
        SocialHub_Section_Enabled = true;
        AIChat_Enabled = false;
        PollingIntervalSeconds = 15;
    }

    /// <summary>Update all toggles + polling interval at once.</summary>
    public void UpdateToggles(
        bool qrTableNumber,
        bool kitchenWorkflow,
        bool voiceNote,
        bool loyaltyProgram,
        bool accountingSync,
        bool einvoiceAutoExport,
        int pollingIntervalSeconds = 15,
        bool vatDisplay = true,
        bool priceValidation = false,
        bool campaignSection = true,
        bool vibeShowcaseSection = true,
        bool googleMapSection = true,
        bool socialHubSection = true,
        bool aiChat = false)
    {
        QR_TableNumber_Enabled = qrTableNumber;
        Kitchen_Workflow_Enabled = kitchenWorkflow;
        Voice_Note_Enabled = voiceNote;
        Loyalty_Program_Enabled = loyaltyProgram;
        Accounting_Sync_Enabled = accountingSync;
        EInvoice_Auto_Export_Enabled = einvoiceAutoExport;
        VAT_Display_Enabled = vatDisplay;
        Price_Validation_Enabled = priceValidation;
        Campaign_Section_Enabled = campaignSection;
        VibeShowcase_Section_Enabled = vibeShowcaseSection;
        GoogleMap_Section_Enabled = googleMapSection;
        SocialHub_Section_Enabled = socialHubSection;
        AIChat_Enabled = aiChat;
        PollingIntervalSeconds = Math.Clamp(pollingIntervalSeconds, 5, 120);
        UpdateAudit();
    }
}
