using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// SRS v1.2 W3/D (M9): tài khoản tiền per tenant — quỹ 111 mặc định + ngân hàng 112x.
/// Data-only Infrastructure entity (precedent: ShopFeatureSettingsEntity) — không domain logic.
/// </summary>
public class BankAccountEntity : BaseEntity
{
    /// <summary>Mã TK kế toán — "111" (quỹ) hoặc "112"/"1121"/"112A"... Unique per tenant.</summary>
    public string AccountCode { get; private set; } = string.Empty;

    /// <summary>Tên ngân hàng (null cho quỹ tiền mặt).</summary>
    public string? BankName { get; private set; }

    /// <summary>Số tài khoản ngân hàng.</summary>
    public string? AccountNumber { get; private set; }

    /// <summary>Tên hiển thị trên phiếu/sổ.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    /// <summary>Ngày hiệu lực — tài khoản chỉ dùng được từ ngày này.</summary>
    public DateTime? EffectiveFrom { get; private set; }

    private BankAccountEntity() { } // EF Core materialization

    public BankAccountEntity(TenantId tenantId, string accountCode, string displayName,
        string? bankName = null, string? accountNumber = null, DateTime? effectiveFrom = null)
        : base(tenantId)
    {
        AccountCode = accountCode;
        DisplayName = displayName;
        BankName = bankName;
        AccountNumber = accountNumber;
        EffectiveFrom = effectiveFrom;
    }

    public void Update(string displayName, string? bankName, string? accountNumber)
    {
        DisplayName = displayName;
        BankName = bankName;
        AccountNumber = accountNumber;
        UpdateAudit();
    }

    /// <summary>Deactivate thay vì xóa — giữ lịch sử trỏ về account trên entries/JEs.</summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdateAudit();
    }
}
