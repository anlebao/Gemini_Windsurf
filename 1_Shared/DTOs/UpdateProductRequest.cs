using System.ComponentModel.DataAnnotations;

namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// Update product request â€” same fields as Create (minus CostPrice, which has its own endpoint).
    /// </summary>
    public class UpdateProductRequest
    {
        [Required(ErrorMessage = "TÃªn sáº£n pháº©m lÃ  báº¯t buá»™c")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "GiÃ¡ bÃ¡n khÃ´ng Ä‘Æ°á»£c Ã¢m")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Danh má»¥c lÃ  báº¯t buá»™c")]
        public string Category { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [Range(0, 1, ErrorMessage = "VAT_RATE pháº£i tá»« 0 Ä‘áº¿n 1")]
        public decimal VatRate { get; set; } = 0.10m;

        public string? ImageUrl { get; set; }
    }
}
