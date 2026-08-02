using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community;

/// <summary>
/// CC-S4 (Sprint 4): SalesmanService unit tests — nearby products, composite QR, commission.
/// 12 test cases per detailed plan Section 4. Uses SQLite in-memory.
/// </summary>
public class SalesmanServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly SalesmanService _service;
    private static readonly Guid SalesmanId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.NewGuid();

    public SalesmanServiceTests()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();

        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new VanAnDbContext(options);
        _context.Database.EnsureCreated();

        var riskService = new RiskScoringService();
        var fraudFlagService = new FraudFlagService(_context, NullLogger<FraudFlagService>.Instance);
        _service = new SalesmanService(_context, riskService, fraudFlagService, NullLogger<SalesmanService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static void SetProp<T>(T obj, string propName, object value)
        => typeof(T).GetProperty(propName)?.SetValue(obj, value);

    private async Task SeedTenantAsync(Guid id, string name, double lat, double lng)
    {
        var tenant = Tenant.CreateCompany(new TenantId(id), name,
            TenantSettings.Empty().WithCoordinates(lat, lng));
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
    }

    private async Task SeedSalesmanRoleAsync()
    {
        var role = new CommunityRole(new TenantId(TenantId), SalesmanId, CommunityRoleType.Salesman, Guid.NewGuid());
        _context.CommunityRoles.Add(role);
        await _context.SaveChangesAsync();
        return;
    }

    private async Task SeedFeaturedProductAsync(Guid productId, Guid tenantId, string name, decimal price)
    {
        var fp = FeaturedProduct.Create(Guid.NewGuid(), new TenantId(tenantId), productId, name, price);
        _context.FeaturedProducts.Add(fp);
        await _context.SaveChangesAsync();
    }

    private async Task SeedProductReferralConfigAsync(Guid productId, decimal rate, decimal bonus, string shortCode)
    {
        var config = new ProductReferralConfig(new TenantId(TenantId), productId, rate, bonus, shortCode);
        _context.ProductReferralConfigs.Add(config);
        await _context.SaveChangesAsync();
    }

    private async Task SeedOrderWithSalesmanAsync(Guid orderId, Guid salesmanId, Guid productId, decimal total)
    {
        var order = new Order(new TenantId(TenantId), null, 0);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId("completed"));
        SetProp(order, "TotalAmount", total);
        SetProp(order, "SalesmanId", salesmanId);
        SetProp(order, "ReferralProductId", productId);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
    }

    // === T1: GetNearbyProducts_FiltersByRadius ===
    [Fact(DisplayName = "T1: GetNearbyProducts_FiltersByRadius")]
    public async Task GetNearbyProducts_FiltersByRadius()
    {
        await SeedTenantAsync(TenantId, "Shop A", 10.8, 106.7);
        await SeedFeaturedProductAsync(ProductId, TenantId, "Product 1", 50000);
        await SeedSalesmanRoleAsync();

        // Query from far away — 100km radius, shop at 10.8/106.7, query from 11.8/107.7 (~130km)
        var result = await _service.GetNearbyProductsAsync(11.8, 107.7, 10, SalesmanId);

        Assert.Empty(result);
    }

    // === T2: GetNearbyProducts_ReturnsProductDetails ===
    [Fact(DisplayName = "T2: GetNearbyProducts_ReturnsProductDetails")]
    public async Task GetNearbyProducts_ReturnsProductDetails()
    {
        await SeedTenantAsync(TenantId, "Shop A", 10.8, 106.7);
        await SeedFeaturedProductAsync(ProductId, TenantId, "Product 1", 50000);
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");
        await SeedSalesmanRoleAsync();

        var result = await _service.GetNearbyProductsAsync(10.8, 106.7, 10, SalesmanId);

        Assert.Single(result);
        var product = result[0];
        Assert.Equal("Product 1", product.Name);
        Assert.Equal(50000, product.Price);
        Assert.Equal("Shop A", product.ShopName);
        Assert.Equal(0.05m, product.CommissionRate);
        Assert.Equal(10000, product.AppInstallBonus);
        Assert.Equal("TR-001", product.ProductShortCode);
        Assert.True(product.HasReferralConfig);
    }

    // === T3: GetNearbyProducts_SortsByDistance ===
    [Fact(DisplayName = "T3: GetNearbyProducts_SortsByDistance")]
    public async Task GetNearbyProducts_SortsByDistance()
    {
        var tenant2Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var product2Id = Guid.NewGuid();

        await SeedTenantAsync(TenantId, "Near Shop", 10.801, 106.7);
        await SeedTenantAsync(tenant2Id, "Far Shop", 10.85, 106.75);
        await SeedFeaturedProductAsync(ProductId, TenantId, "Product 1", 50000);
        await SeedFeaturedProductAsync(product2Id, tenant2Id, "Product 2", 60000);
        await SeedSalesmanRoleAsync();

        var result = await _service.GetNearbyProductsAsync(10.8, 106.7, 20, SalesmanId);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].DistanceKm <= result[1].DistanceKm);
        Assert.Equal("Near Shop", result[0].ShopName);
    }

    // === T4: GetNearbyProducts_NoConfig_ShowsNotSetup ===
    [Fact(DisplayName = "T4: GetNearbyProducts_NoConfig_ShowsNotSetup")]
    public async Task GetNearbyProducts_NoConfig_ShowsNotSetup()
    {
        await SeedTenantAsync(TenantId, "Shop A", 10.8, 106.7);
        await SeedFeaturedProductAsync(ProductId, TenantId, "Product 1", 50000);
        await SeedSalesmanRoleAsync();

        var result = await _service.GetNearbyProductsAsync(10.8, 106.7, 10, SalesmanId);

        Assert.Single(result);
        Assert.False(result[0].HasReferralConfig);
        Assert.Null(result[0].CommissionRate);
        Assert.Null(result[0].AppInstallBonus);
    }

    // === T5: GetCompositeSalesmanQr_ReturnsCompositeCode ===
    [Fact(DisplayName = "T5: GetCompositeSalesmanQr_ReturnsCompositeCode")]
    public async Task GetCompositeSalesmanQr_ReturnsCompositeCode()
    {
        await SeedSalesmanRoleAsync();
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var result = await _service.GetCompositeSalesmanQrAsync(SalesmanId, ProductId);

        Assert.NotNull(result);
        Assert.Contains("|", result!.CompositeCode);
        Assert.Contains("TR-001", result.CompositeCode);
        Assert.Contains("/r/", result.QrUrl);
    }

    // === T6: GetCompositeSalesmanQr_NoProductConfig_ReturnsNull ===
    [Fact(DisplayName = "T6: GetCompositeSalesmanQr_NoProductConfig_ReturnsNull")]
    public async Task GetCompositeSalesmanQr_NoProductConfig_ReturnsNull()
    {
        await SeedSalesmanRoleAsync();

        var result = await _service.GetCompositeSalesmanQrAsync(SalesmanId, ProductId);

        Assert.Null(result);
    }

    // === T7: GetCompositeSalesmanQr_NoRole_ReturnsNull ===
    [Fact(DisplayName = "T7: GetCompositeSalesmanQr_NoRole_ReturnsNull")]
    public async Task GetCompositeSalesmanQr_NoRole_ReturnsNull()
    {
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var result = await _service.GetCompositeSalesmanQrAsync(SalesmanId, ProductId);

        Assert.Null(result);
    }

    // === T8: ResolveCompositeReferralCode_Valid_ReturnsBothIds ===
    [Fact(DisplayName = "T8: ResolveCompositeReferralCode_Valid_ReturnsBothIds")]
    public async Task ResolveCompositeReferralCode_Valid_ReturnsBothIds()
    {
        await SeedSalesmanRoleAsync();
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        // Get the actual salesman code from the role
        var role = await _context.CommunityRoles.IgnoreQueryFilters().FirstAsync();
        var compositeCode = $"{role.SalesmanCode}|TR-001";

        var result = await _service.ResolveCompositeReferralCodeAsync(compositeCode);

        Assert.NotNull(result);
        Assert.Equal(SalesmanId, result!.Value.salesmanId);
        Assert.Equal(ProductId, result.Value.productId);
    }

    // === T9: ResolveCompositeReferralCode_Invalid_ReturnsNull ===
    [Fact(DisplayName = "T9: ResolveCompositeReferralCode_Invalid_ReturnsNull")]
    public async Task ResolveCompositeReferralCode_Invalid_ReturnsNull()
    {
        var result = await _service.ResolveCompositeReferralCodeAsync("INVALID|CODE");

        Assert.Null(result);
    }

    // === T10: CreateCommission_PerProduct_CalculatesCorrectly ===
    [Fact(DisplayName = "T10: CreateCommission_PerProduct_CalculatesCorrectly")]
    public async Task CreateCommission_PerProduct_CalculatesCorrectly()
    {
        await SeedSalesmanRoleAsync();
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var orderId = Guid.NewGuid();
        await SeedOrderWithSalesmanAsync(orderId, SalesmanId, ProductId, 100000);

        var referral = await _service.CreateCommissionAsync(orderId);

        Assert.NotNull(referral);
        Assert.Equal(0.05m, referral!.CommissionRate);
        Assert.Equal(5000, referral.CommissionAmount); // 100000 * 0.05
    }

    // === T11: CreateCommission_NoSalesmanId_ReturnsNull ===
    [Fact(DisplayName = "T11: CreateCommission_NoSalesmanId_ReturnsNull")]
    public async Task CreateCommission_NoSalesmanId_ReturnsNull()
    {
        var orderId = Guid.NewGuid();
        var order = new Order(new TenantId(TenantId), null, 0);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId("completed"));
        SetProp(order, "TotalAmount", 100000m);
        // No SalesmanId, no ReferralProductId
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var result = await _service.CreateCommissionAsync(orderId);

        Assert.Null(result);
    }

    // === T12: GetCommissions_AggregatesBothSources ===
    [Fact(DisplayName = "T12: GetCommissions_AggregatesBothSources")]
    public async Task GetCommissions_AggregatesBothSources()
    {
        await SeedSalesmanRoleAsync();
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var orderId = Guid.NewGuid();
        await SeedOrderWithSalesmanAsync(orderId, SalesmanId, ProductId, 100000);

        await _service.CreateCommissionAsync(orderId);

        // Add an app-install attribution
        var attribution = new AppInstallAttribution(new TenantId(TenantId), Guid.NewGuid(), SalesmanId, ProductId, 10000);
        _context.AppInstallAttributions.Add(attribution);
        await _context.SaveChangesAsync();

        var summary = await _service.GetCommissionsAsync(SalesmanId);

        Assert.Equal(5000, summary.TotalCommission);
        Assert.Single(summary.CommissionRecords);
        Assert.Single(summary.AppInstallBonusRecords);
        Assert.Equal(10000, summary.TotalAppInstallBonus);
    }
}
