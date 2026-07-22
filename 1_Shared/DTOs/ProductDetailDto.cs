namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// Product detail DTO â€” API response shape for product management endpoints.
    /// Maps from <see cref="VanAn.Shared.Domain.Product"/> domain entity.
    /// </summary>
    public class ProductDetailDto
    {
        public Guid ProductId { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal CostPrice { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? ImageUrl { get; set; }
        public decimal VatRate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
