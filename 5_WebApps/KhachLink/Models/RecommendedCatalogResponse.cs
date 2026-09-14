using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Models;

/// <summary>
/// Phase 6: Response from Gateway GET /api/catalog/recommended.
/// Union of FeaturedProducts + customer purchase history.
/// </summary>
public class RecommendedCatalogResponse
{
    public List<RecommendedCatalogItem> Products { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Phase 6: Single item in the recommended catalog response.
/// Source = "Featured" (sysadmin curated) or "History" (previously purchased).
/// </summary>
public class RecommendedCatalogItem
{
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal DisplayPrice { get; set; }
    public decimal VatRate { get; set; } = 0.10m;
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string Source { get; set; } = "Featured"; // "Featured" | "History"
    public DateTime? LastOrderedAt { get; set; }
    /// <summary>Tenant display name — resolved from PG Tenants table by Gateway.</summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>Product type — Paid (default), Free, or Charity.
    /// When Free/Charity, DisplayPrice=0 is valid and IsFree is set on CartItem at add time.</summary>
    public FeaturedProductType ProductType { get; set; } = FeaturedProductType.Paid;

    /// <summary>True when ProductType is Free or Charity (charity/giveaway products).</summary>
    public bool IsFree => ProductType != FeaturedProductType.Paid;
}
