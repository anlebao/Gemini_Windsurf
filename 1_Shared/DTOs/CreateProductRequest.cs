using System.ComponentModel.DataAnnotations;

namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// Create product request — validated at API boundary (DataAnnotations OK at DTO layer).
    /// </summary>
    public class CreateProductRequest
    {
        [Required(ErrorMessage = "Tên sản phẩm là bắt buộc")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá bán không được âm")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Danh mục là bắt buộc")]
        public string Category { get; set; } = string.Empty;

        [Range(0, 1, ErrorMessage = "VAT_RATE phải từ 0 đến 1")]
        public decimal VatRate { get; set; } = 0.10m;

        public string? ImageUrl { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá vốn không được âm")]
        public decimal CostPrice { get; set; } = 0m;
    }
}
