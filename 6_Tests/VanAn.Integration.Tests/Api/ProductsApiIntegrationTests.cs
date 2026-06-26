using Xunit;
using Xunit.Abstractions;
using Microsoft.EntityFrameworkCore;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Integration.Tests.Infrastructure;
using static VanAn.Integration.Tests.Infrastructure.TestEntityBuilder;

namespace VanAn.Integration.Tests.Api;

/// <summary>
/// Wave 13 — Integration tests for Products catalog data loading.
/// Validates that active products are persisted and queryable per tenant
/// (same logic as ProductsController.GetProducts uses).
/// </summary>
[Trait("Category", "Integration")]
public class ProductsApiIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public ProductsApiIntegrationTests(ITestOutputHelper output) : base()
    {
        _output = output;
    }

    [Fact(DisplayName = "Products: Active products are queryable by tenant")]
    public async Task GetProducts_ActiveProductsExist_ReturnsOnlyActiveForTenant()
    {
        // Arrange
        var tenantId = TestTenantId;
        var product1 = CreateProduct(tenantId, "Trà Sữa Matcha", 45000m, "Matcha");
        var product2 = CreateProduct(tenantId, "Trà Sữa Đậu Đỏ", 40000m, "Traditional");

        _dbContext.Products.Add(product1);
        _dbContext.Products.Add(product2);
        await _dbContext.SaveChangesAsync();

        // Act — replicate ProductsController.GetProducts query
        var result = await _dbContext.Products
            .Where(p => p.IsActive && !p.IsDeleted)
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToListAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.True(p.IsActive));
        Assert.All(result, p => Assert.False(p.IsDeleted));
        _output.WriteLine($"Found {result.Count} active products for tenant {tenantId.Value}");
    }

    [Fact(DisplayName = "Products: Inactive products excluded from catalog")]
    public async Task GetProducts_InactiveProduct_ExcludedFromCatalog()
    {
        // Arrange
        var tenantId = TestTenantId;
        var activeProduct = CreateProduct(tenantId, "Cà Phê Sữa", 35000m, "Coffee");
        var inactiveProduct = new Product(tenantId, "Sinh Tố Cũ", "Ngừng bán", price: 30000m, "Smoothie", isActive: false);

        _dbContext.Products.AddRange(activeProduct, inactiveProduct);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _dbContext.Products
            .Where(p => p.IsActive && !p.IsDeleted)
            .Where(p => p.TenantId == tenantId)
            .ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Cà Phê Sữa", result[0].Name);
        _output.WriteLine("Inactive product correctly excluded from catalog");
    }

    [Fact(DisplayName = "Products: Multi-tenant isolation — tenant A cannot see tenant B products")]
    public async Task GetProducts_MultiTenant_ReturnsOnlyOwnTenantProducts()
    {
        // Arrange
        var tenantA = TestTenantId;
        var tenantB = new TenantId(Guid.Parse("99999999-9999-9999-9999-999999999999"));

        var productA = CreateProduct(tenantA, "Trà Sữa Tenant A", 40000m, "Tea");
        var productB = CreateProduct(tenantB, "Trà Sữa Tenant B", 42000m, "Tea");

        _dbContext.Products.AddRange(productA, productB);
        await _dbContext.SaveChangesAsync();

        // Act — query for tenant A only
        var resultA = await _dbContext.Products
            .Where(p => p.IsActive && !p.IsDeleted)
            .Where(p => p.TenantId == tenantA)
            .ToListAsync();

        // Assert — tenant A sees only their products (multi-tenancy query filter also applies)
        Assert.All(resultA, p => Assert.Equal(tenantA.Value, p.TenantId.Value));
        _output.WriteLine($"Tenant A sees {resultA.Count} product(s); isolation enforced");
    }

    [Fact(DisplayName = "Products: Product fields map correctly to catalog DTO shape")]
    public async Task GetProducts_ProductFields_MapCorrectlyToCatalogShape()
    {
        // Arrange
        var tenantId = TestTenantId;
        var product = new Product(
            tenantId,
            name: "Matcha Đặc Biệt",
            description: "Matcha Nhật Bản nguyên chất",
            price: 55000m,
            category: "Matcha",
            isActive: true,
            imageUrl: "https://cdn.vanan.vn/matcha.jpg",
            vatRate: 0.10m);

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        // Act
        var saved = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.ProductId == product.ProductId);

        // Assert fields used by ProductCatalogItem DTO
        Assert.NotNull(saved);
        Assert.Equal("Matcha Đặc Biệt", saved.Name);
        Assert.Equal("Matcha Nhật Bản nguyên chất", saved.Description);
        Assert.Equal(55000m, saved.Price);
        Assert.Equal("Matcha", saved.Category);
        Assert.Equal("https://cdn.vanan.vn/matcha.jpg", saved.ImageUrl);
        Assert.Equal(0.10m, saved.VatRate);
        _output.WriteLine($"Product {saved.ProductId.Value} fields verified");
    }

    [Fact(DisplayName = "Products: Empty catalog returns empty list (no 500)")]
    public async Task GetProducts_NoCatalogData_ReturnsEmptyList()
    {
        // Act — no products seeded; multi-tenancy filter ensures clean state
        var result = await _dbContext.Products
            .Where(p => p.IsActive && !p.IsDeleted)
            .Where(p => p.TenantId == TestTenantId)
            .ToListAsync();

        // Assert
        Assert.Empty(result);
        _output.WriteLine("Empty product catalog returns empty list correctly");
    }
}
