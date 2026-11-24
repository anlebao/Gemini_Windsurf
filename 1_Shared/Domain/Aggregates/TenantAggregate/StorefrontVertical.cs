namespace VanAn.Shared.Domain.Aggregates.TenantAggregate
{
    /// <summary>
    /// Storefront content vertical (2026-11-24, Phương án A — user approved):
    /// ngành nghề của tenant → preset labels trên /store/{slug} (tiêu đề section,
    /// CTA, AI chips, search placeholder). Trước đây hardcode F&B ("Thực đơn đặc sắc",
    /// "món", "combo") — sai với tenant dịch vụ (spa, luật sư, kế toán, công nghệ).
    /// General = default trung lập cho tenant chưa chọn.
    /// </summary>
    public enum StorefrontVertical
    {
        /// <summary>Mặc định — labels trung lập "Sản phẩm & Dịch vụ".</summary>
        General = 0,
        /// <summary>F&B — quán ăn, nhà hàng, cafe, trà sữa, bakery.</summary>
        Fnb = 1,
        /// <summary>Beauty & Spa — spa, salon, nail, thẩm mỹ, vật lý trị liệu.</summary>
        BeautySpa = 2,
        /// <summary>Dịch vụ chuyên môn — luật sư, kế toán, thiết kế, tư vấn.</summary>
        ProfessionalService = 3,
        /// <summary>Công nghệ — phần mềm, giải pháp, dịch vụ IT.</summary>
        Technology = 4,
        /// <summary>Bán lẻ — shop thời trang, tạp hóa, showroom.</summary>
        Retail = 5
    }
}
