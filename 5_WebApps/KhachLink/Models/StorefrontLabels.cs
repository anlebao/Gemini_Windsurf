using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.KhachLink.Models;

/// <summary>
/// Storefront content labels theo ngành nghề (Phương án A — 2026-11-24, user approved).
/// Trước đây hardcode F&B ("Thực đơn", "món", "combo") — sai với tenant dịch vụ
/// (spa, luật sư, kế toán, công nghệ). Gateway trả StorefrontVertical; KhachLink
/// resolve labels phía client — không cần thêm field trên wire.
/// </summary>
public sealed record StorefrontLabels(
    /// <summary>Tiêu đề section sản phẩm/dịch vụ trên /store/{slug}.</summary>
    string ProductsSectionTitle,
    /// <summary>Nhãn mục nav "Sản phẩm".</summary>
    string ProductsNavLabel,
    /// <summary>Nút CTA trên product card (action = AddToCart).</summary>
    string ProductCardCta,
    /// <summary>Chip AI gợi ý 1 — label hiển thị.</summary>
    string Chip1Label,
    /// <summary>Chip AI gợi ý 1 — câu hỏi gửi vào chat.</summary>
    string Chip1Question,
    /// <summary>Chip AI gợi ý 2 — label hiển thị.</summary>
    string Chip2Label,
    /// <summary>Chip AI gợi ý 2 — câu hỏi gửi vào chat.</summary>
    string Chip2Question,
    /// <summary>Placeholder ô search ngữ nghĩa.</summary>
    string SearchPlaceholder,
    /// <summary>Text khi search không có kết quả.</summary>
    string SearchEmptyText,
    /// <summary>Nút "Hỏi AI" trong quick-view modal.</summary>
    string QuickViewAskLabel);

public static class StorefrontLabelPresets
{
    /// <summary>Resolve labels theo vertical — General = trung lập, fallback an toàn.</summary>
    public static StorefrontLabels For(StorefrontVertical vertical) => vertical switch
    {
        StorefrontVertical.Fnb => new(
            ProductsSectionTitle: "Thực Đơn Đặc Sắc",
            ProductsNavLabel: "Thực đơn",
            ProductCardCta: "Đặt món",
            Chip1Label: "💡 Món bán chạy?",
            Chip1Question: "Món nào đang bán chạy nhất?",
            Chip2Label: "✨ Gợi ý combo?",
            Chip2Question: "Gợi ý combo hoặc món phù hợp cho nhóm của tôi?",
            SearchPlaceholder: "Hỏi gì về quán? (VD: món chay, combo, giá...)",
            SearchEmptyText: "Chưa tìm thấy món phù hợp — thử hỏi AI bên dưới nhé.",
            QuickViewAskLabel: "🤖 Hỏi AI về món này"),

        StorefrontVertical.BeautySpa => new(
            ProductsSectionTitle: "Dịch Vụ & Liệu Trình",
            ProductsNavLabel: "Dịch vụ",
            ProductCardCta: "Thêm vào giỏ",
            Chip1Label: "💡 Liệu trình nổi bật?",
            Chip1Question: "Liệu trình nào được yêu thích nhất?",
            Chip2Label: "✨ Tư vấn liệu trình?",
            Chip2Question: "Gợi ý liệu trình phù hợp với nhu cầu của tôi?",
            SearchPlaceholder: "Hỏi gì về spa? (VD: bảng giá, liệu trình...)",
            SearchEmptyText: "Chưa tìm thấy dịch vụ phù hợp — thử hỏi AI bên dưới nhé.",
            QuickViewAskLabel: "🤖 Hỏi AI về dịch vụ này"),

        StorefrontVertical.ProfessionalService => new(
            ProductsSectionTitle: "Dịch Vụ Chuyên Môn",
            ProductsNavLabel: "Dịch vụ",
            ProductCardCta: "Thêm vào giỏ",
            Chip1Label: "💼 Dịch vụ nổi bật?",
            Chip1Question: "Dịch vụ nào đang được quan tâm nhất?",
            Chip2Label: "✨ Tư vấn giải pháp?",
            Chip2Question: "Tôi cần tư vấn dịch vụ phù hợp với nhu cầu của tôi?",
            SearchPlaceholder: "Hỏi gì về dịch vụ? (VD: bảng giá, quy trình...)",
            SearchEmptyText: "Chưa tìm thấy dịch vụ phù hợp — thử hỏi AI bên dưới nhé.",
            QuickViewAskLabel: "🤖 Hỏi AI về dịch vụ này"),

        StorefrontVertical.Technology => new(
            ProductsSectionTitle: "Sản Phẩm & Giải Pháp",
            ProductsNavLabel: "Giải pháp",
            ProductCardCta: "Thêm vào giỏ",
            Chip1Label: "💡 Giải pháp nổi bật?",
            Chip1Question: "Giải pháp nào được sử dụng nhiều nhất?",
            Chip2Label: "✨ Tư vấn giải pháp?",
            Chip2Question: "Tôi cần giải pháp phù hợp với doanh nghiệp của tôi?",
            SearchPlaceholder: "Hỏi gì về sản phẩm? (VD: tính năng, bảng giá...)",
            SearchEmptyText: "Chưa tìm thấy kết quả phù hợp — thử hỏi AI bên dưới nhé.",
            QuickViewAskLabel: "🤖 Hỏi AI về sản phẩm này"),

        StorefrontVertical.Retail => new(
            ProductsSectionTitle: "Bộ Sưu Tập Sản Phẩm",
            ProductsNavLabel: "Sản phẩm",
            ProductCardCta: "Thêm vào giỏ",
            Chip1Label: "💡 Sản phẩm nổi bật?",
            Chip1Question: "Sản phẩm nào đang được ưa chuộng nhất?",
            Chip2Label: "✨ Tư vấn lựa chọn?",
            Chip2Question: "Gợi ý sản phẩm phù hợp với nhu cầu của tôi?",
            SearchPlaceholder: "Hỏi gì về cửa hàng? (VD: size, bảng giá...)",
            SearchEmptyText: "Chưa tìm thấy sản phẩm phù hợp — thử hỏi AI bên dưới nhé.",
            QuickViewAskLabel: "🤖 Hỏi AI về sản phẩm này"),

        // General + fallback: labels trung lập — không gắn ngành cụ thể
        _ => new(
            ProductsSectionTitle: "Bộ Sưu Tập Sản Phẩm",
            ProductsNavLabel: "Sản phẩm",
            ProductCardCta: "Thêm vào giỏ",
            Chip1Label: "💡 Gợi ý nổi bật?",
            Chip1Question: "Gợi ý sản phẩm hoặc dịch vụ nổi bật?",
            Chip2Label: "✨ Tư vấn lựa chọn",
            Chip2Question: "Gợi ý lựa chọn phù hợp với nhu cầu của tôi?",
            SearchPlaceholder: "Hỏi gì về cửa hàng? (VD: dịch vụ, bảng giá...)",
            SearchEmptyText: "Chưa tìm thấy kết quả phù hợp — thử hỏi AI bên dưới nhé.",
            QuickViewAskLabel: "🤖 Hỏi AI về sản phẩm này")
    };
}
