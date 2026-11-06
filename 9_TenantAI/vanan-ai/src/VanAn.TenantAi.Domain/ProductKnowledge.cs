namespace VanAn.TenantAi.Domain;

/// <summary>
/// SRS v1.1 §7 + [G16] — curated mirror of a ShopERP product (source of truth stays
/// in ShopERP SQLite). One row per (TenantId, ProductId); sync updates in place and
/// bumps SourceVersion + SyncedAtUtc. Staleness is computed from SyncedAtUtc.
/// </summary>
public sealed class ProductKnowledge
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public int? Calories { get; private set; }
    public string IngredientsJson { get; private set; } = "[]";
    public string AllergensJson { get; private set; } = "[]";
    public bool Available { get; private set; }
    public string? CategoryName { get; private set; }
    public string SourceVersion { get; private set; } = string.Empty;
    public DateTime SyncedAtUtc { get; private set; }

    private ProductKnowledge() { } // EF Core

    public static ProductKnowledge Create(
        Guid tenantId,
        Guid productId,
        string name,
        decimal price,
        string sourceVersion)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Name = name,
            Price = price,
            Available = true,
            SourceVersion = sourceVersion,
            SyncedAtUtc = DateTime.UtcNow
        };

    /// <summary>Replace-by-source sync: updates all mirrored fields and freshness markers.</summary>
    public void UpdateFromSource(
        string name,
        string? description,
        decimal price,
        int? calories,
        string ingredientsJson,
        string allergensJson,
        bool available,
        string? categoryName,
        string sourceVersion)
    {
        Name = name;
        Description = description;
        Price = price;
        Calories = calories;
        IngredientsJson = ingredientsJson;
        AllergensJson = allergensJson;
        Available = available;
        CategoryName = categoryName;
        SourceVersion = sourceVersion;
        SyncedAtUtc = DateTime.UtcNow;
    }
}
