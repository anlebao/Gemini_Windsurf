using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services.Accounting;

/// <summary>
/// TT 71 Phase 5 (S3) — Single source of truth cho auto-map tenant type → chuẩn kế toán
/// trên các màn hình báo cáo. User vẫn có thể đổi select sau khi auto-map (giữ UX hiện tại).
/// HTX → TT 71/2024 · Enterprise_Large → TT 99/2025 · Enterprise_SuperSmall → TT 58/2026
/// · còn lại (HKD/vừa) → TT 133/2016.
/// Regime-lock (2026-10-30): thêm ResolveEffective/AllowedStandards/IsVoluntary/DropdownLabel —
/// logic phân quyền chuẩn sống ở <see cref="AccountingStandardRules"/> (1_Shared, dùng chung
/// với CoreHub write-path validation). HKD không dùng BCTC DN — AllowedStandards rỗng khi
/// chưa bầu chọn TT 58 (Điều 2.2 TT 58/2026, qua Tenant.AccountingStandard override).
/// </summary>
public static class AccountingStandardResolver
{
    public static AccountingStandard Resolve(TenantType? tenantType)
        => AccountingStandardRules.DefaultStandard(tenantType);

    public static AccountingStandard? ResolveEffective(TenantType? tenantType, AccountingStandard? accountingStandardOverride)
        => AccountingStandardRules.ResolveEffective(tenantType, accountingStandardOverride);

    public static IReadOnlyList<AccountingStandard> AllowedStandards(TenantType? tenantType, AccountingStandard? accountingStandardOverride)
        => AccountingStandardRules.AllowedStandards(tenantType, accountingStandardOverride);

    public static bool IsVoluntary(TenantType? tenantType, AccountingStandard standard)
        => AccountingStandardRules.IsVoluntary(tenantType, standard);

    /// <summary>Nhãn hiển thị trong dropdown chuẩn trên các trang BCTC.</summary>
    public static string DropdownLabel(AccountingStandard standard) => standard switch
    {
        AccountingStandard.TT133_2016 => "TT 133/2016 (DN vừa)",
        AccountingStandard.TT99_2025 => "TT 99/2025 (DN lớn)",
        AccountingStandard.TT71_2024 => "TT 71/2024 (HTX)",
        AccountingStandard.TT58_2026 => "TT 58/2026 (DN siêu nhỏ)",
        _ => standard.ToString()
    };
}
