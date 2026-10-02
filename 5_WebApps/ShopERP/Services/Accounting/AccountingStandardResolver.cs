using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services.Accounting;

/// <summary>
/// TT 71 Phase 5 (S3) — Single source of truth cho auto-map tenant type → chuẩn kế toán
/// trên các màn hình báo cáo. User vẫn có thể đổi select sau khi auto-map (giữ UX hiện tại).
/// HTX → TT 71/2024 · Enterprise_Large → TT 99/2025 · còn lại (HKD/siêu nhỏ/vừa) → TT 133/2016.
/// NOTE (ngoài scope TT 71): HKD tenant thực chất KHÔNG dùng BCTC DN (HKD dùng sổ) —
/// đang rơi vào TT 133 từ lâu; ghi nhận, không xử lý trong phase này.
/// </summary>
public static class AccountingStandardResolver
{
    public static AccountingStandard Resolve(TenantType? tenantType) => tenantType switch
    {
        TenantType.Enterprise_Large => AccountingStandard.TT99_2025,
        TenantType.HTX => AccountingStandard.TT71_2024,
        _ => AccountingStandard.TT133_2016
    };
}
