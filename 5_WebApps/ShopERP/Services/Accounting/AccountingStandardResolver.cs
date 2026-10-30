using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services.Accounting;

/// <summary>
/// TT 71 Phase 5 (S3) — Single source of truth cho auto-map tenant type → chuẩn kế toán
/// trên các màn hình báo cáo. User vẫn có thể đổi select sau khi auto-map (giữ UX hiện tại).
/// HTX → TT 71/2024 · Enterprise_Large → TT 99/2025 · Enterprise_SuperSmall → TT 58/2026
/// · còn lại (HKD/vừa) → TT 133/2016.
/// NOTE: HKD tenant thực chất KHÔNG dùng BCTC DN (HKD dùng sổ TT152 hoặc chọn TT58 —
/// Tenant.AccountingStandard override). Rơi vào TT133 chỉ là fallback hiển thị.
/// </summary>
public static class AccountingStandardResolver
{
    public static AccountingStandard Resolve(TenantType? tenantType) => tenantType switch
    {
        TenantType.Enterprise_Large => AccountingStandard.TT99_2025,
        TenantType.Enterprise_SuperSmall => AccountingStandard.TT58_2026,
        TenantType.HTX => AccountingStandard.TT71_2024,
        _ => AccountingStandard.TT133_2016
    };
}
