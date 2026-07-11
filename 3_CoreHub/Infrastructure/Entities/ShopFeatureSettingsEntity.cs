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
    }

    /// <summary>Update all toggles at once.</summary>
    public void UpdateToggles(
        bool qrTableNumber,
        bool kitchenWorkflow,
        bool voiceNote,
        bool loyaltyProgram,
        bool accountingSync,
        bool einvoiceAutoExport)
    {
        QR_TableNumber_Enabled = qrTableNumber;
        Kitchen_Workflow_Enabled = kitchenWorkflow;
        Voice_Note_Enabled = voiceNote;
        Loyalty_Program_Enabled = loyaltyProgram;
        Accounting_Sync_Enabled = accountingSync;
        EInvoice_Auto_Export_Enabled = einvoiceAutoExport;
        UpdateAudit();
    }
}
