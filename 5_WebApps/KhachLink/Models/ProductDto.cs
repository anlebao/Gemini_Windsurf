using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Models;

public class ProductDto
{
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public decimal VatRate { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>Product type — Paid (default), Free, or Charity.
    /// When Free/Charity, Price=0 is valid and IsFree is set on CartItem at add time.</summary>
    public FeaturedProductType ProductType { get; set; } = FeaturedProductType.Paid;

    /// <summary>True when ProductType is Free or Charity (charity/giveaway products).
    /// Convenience flag for cart/checkout logic.</summary>
    public bool IsFree => ProductType != FeaturedProductType.Paid;
}

/// <summary>
/// Product DTO with recommendation metadata for personalized sections
/// </summary>
public class RecommendedProductDto : ProductDto
{
    public int FrequencyScore { get; set; }
    public decimal TotalSpent { get; set; }
    public string RecommendationReason { get; set; } = string.Empty;
}
