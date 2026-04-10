using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.Shared.Domain.Common;

namespace VanAn.Tests;

public class ShopServiceMultiTenancyTests
{
    private ServiceProvider _serviceProvider;
    private VanAnDbContext _context;
    private IShopService _shopService;
    private ITenantProvider _tenantProvider;

    public ShopServiceMultiTenancyTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));
        services.AddScoped<ITenantProvider, TestTenantProvider>();
        services.AddScoped<IShopService, ShopService>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<VanAnDbContext>();
        _shopService = _serviceProvider.GetRequiredService<IShopService>();
        _tenantProvider = _serviceProvider.GetRequiredService<ITenantProvider>();
    }

    [Fact]
    public async Task CreateShop_ShouldAutoInjectTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantProvider.SetTenant(tenantId);
        
        var shop = new Shop
        {
            Name = "Test Shop",
            Address = "Test Address",
            Phone = "123456789",
            Email = "test@example.com"
        };

        // Act
        var result = await _shopService.CreateShopAsync(shop);

        // Assert
        Assert.Equal(tenantId, result.TenantId);
    }

    [Fact]
    public async Task GetShops_ShouldOnlyReturnTenantSpecificShops()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        // Create shops for tenant 1
        _tenantProvider.SetTenant(tenant1Id);
        await _shopService.CreateShopAsync(new Shop { Name = "Tenant1 Shop1", Address = "Addr1", Phone = "123", Email = "t1s1@test.com" });
        await _shopService.CreateShopAsync(new Shop { Name = "Tenant1 Shop2", Address = "Addr2", Phone = "456", Email = "t1s2@test.com" });

        // Create shops for tenant 2
        _tenantProvider.SetTenant(tenant2Id);
        await _shopService.CreateShopAsync(new Shop { Name = "Tenant2 Shop1", Address = "Addr3", Phone = "789", Email = "t2s1@test.com" });

        // Act - Query as tenant 1
        _tenantProvider.SetTenant(tenant1Id);
        var tenant1Shops = await _shopService.GetShopsAsync();

        // Act - Query as tenant 2
        _tenantProvider.SetTenant(tenant2Id);
        var tenant2Shops = await _shopService.GetShopsAsync();

        // Assert
        Assert.Equal(2, tenant1Shops.Count);
        Assert.All(tenant1Shops, shop => Assert.Equal(tenant1Id, shop.TenantId));
        Assert.Single(tenant2Shops);
        Assert.All(tenant2Shops, shop => Assert.Equal(tenant2Id, shop.TenantId));
    }

    [Fact]
    public async Task GetShopById_ShouldReturnOnlyTenantSpecificShop()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        _tenantProvider.SetTenant(tenant1Id);
        var tenant1Shop = await _shopService.CreateShopAsync(new Shop 
        { 
            Name = "Tenant1 Shop", 
            Address = "Addr1", 
            Phone = "123", 
            Email = "t1@test.com" 
        });

        _tenantProvider.SetTenant(tenant2Id);
        var tenant2Shop = await _shopService.CreateShopAsync(new Shop 
        { 
            Name = "Tenant2 Shop", 
            Address = "Addr2", 
            Phone = "456", 
            Email = "t2@test.com" 
        });

        // Act - Try to access tenant2's shop as tenant1
        _tenantProvider.SetTenant(tenant1Id);
        var result = await _shopService.GetShopByIdAsync(tenant2Shop.Id);

        // Assert - Should return null (data isolation)
        Assert.Null(result);

        // Act - Access tenant1's own shop
        var ownShop = await _shopService.GetShopByIdAsync(tenant1Shop.Id);

        // Assert - Should return the shop
        Assert.NotNull(ownShop);
        Assert.Equal("Tenant1 Shop", ownShop.Name);
    }
}
