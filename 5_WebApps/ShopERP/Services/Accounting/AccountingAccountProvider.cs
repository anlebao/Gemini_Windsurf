using VanAn.Shared.Domain;
using PlatformComposite = VanAn.UI.Platform.Components.Composite;

namespace VanAn.ShopERP.Services.Accounting;

/// <summary>
/// TT 71 Phase 4a (S3) — Account options cho phiếu thu/chi theo chuẩn kế toán của tenant.
/// HTX (TT 71/2024): thu = 511 (giao dịch bên ngoài) / 512 (giao dịch nội bộ) / 558 (thu nhập khác);
/// chi = 642 (CP QLKD) / 658 (chi phí khác). KHÔNG dùng 515/711 (thu) và 621/622/627/641 (chi) —
/// các TK này không tồn tại trong TT 71 (Phụ lục I).
/// DN/HKD: giữ nguyên danh sách hiện tại (không regress).
/// NOTE: 611/612 (giá vốn nội/ngoài) chưa đưa vào phiếu chi HTX — chờ user duyệt (master plan D6 "nếu duyệt").
/// </summary>
public static class AccountingAccountProvider
{
    public static bool IsHtx(TenantType? tenantType) => tenantType == TenantType.HTX;

    /// <summary>Danh sách TK doanh thu hợp lệ cho phiếu thu theo loại tenant.</summary>
    public static List<PlatformComposite.FieldOption> GetRevenueAccounts(TenantType? tenantType)
    {
        if (IsHtx(tenantType))
        {
            // TT 71 — Phụ lục I: 511 Doanh thu giao dịch bên ngoài · 512 Doanh thu giao dịch nội bộ · 558 Thu nhập khác
            return new List<PlatformComposite.FieldOption>
            {
                new() { Value = "511", Label = "511 - Doanh thu giao dịch bên ngoài" },
                new() { Value = "512", Label = "512 - Doanh thu giao dịch nội bộ" },
                new() { Value = "558", Label = "558 - Thu nhập khác" }
            };
        }

        // DN/HKD — giữ nguyên options hiện tại
        return new List<PlatformComposite.FieldOption>
        {
            new() { Value = "511", Label = "511 - Doanh thu bán hàng hóa" },
            new() { Value = "512", Label = "512 - Doanh thu cung cấp dịch vụ" },
            new() { Value = "515", Label = "515 - Doanh thu hoạt động khác" },
            new() { Value = "711", Label = "711 - Chênh lệch tỷ giá hối đoái" }
        };
    }

    /// <summary>Danh sách TK chi phí hợp lệ cho phiếu chi theo loại tenant.</summary>
    public static List<PlatformComposite.FieldOption> GetExpenseAccounts(TenantType? tenantType)
    {
        if (IsHtx(tenantType))
        {
            // TT 71 — Phụ lục I: 642 Chi phí quản lý kinh doanh · 658 Chi phí khác
            return new List<PlatformComposite.FieldOption>
            {
                new() { Value = "642", Label = "642 - Chi phí quản lý kinh doanh" },
                new() { Value = "658", Label = "658 - Chi phí khác" }
            };
        }

        // DN/HKD — giữ nguyên options hiện tại
        return new List<PlatformComposite.FieldOption>
        {
            new() { Value = "621", Label = "621 - Chi phí nguyên vật liệu" },
            new() { Value = "622", Label = "622 - Chi phí nhân công" },
            new() { Value = "627", Label = "627 - Chi phí bán hàng" },
            new() { Value = "641", Label = "641 - Chi phí quản lý doanh nghiệp" },
            new() { Value = "642", Label = "642 - Chi phí tài chính" }
        };
    }

    /// <summary>HelpText cho field "Tài Khoản" — note chuẩn áp dụng khi tenant HTX.</summary>
    public static string GetAccountHelpText(TenantType? tenantType)
    {
        return IsHtx(tenantType)
            ? "Chọn tài khoản kế toán — Theo TT 71/2024 (Chế độ kế toán HTX)"
            : "Chọn tài khoản kế toán";
    }
}
