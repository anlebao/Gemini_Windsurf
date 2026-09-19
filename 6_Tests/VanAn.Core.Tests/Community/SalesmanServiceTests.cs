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

    /// <summary>Orders.CustomerId has an FK to Customers — seed a Customer row with the matching Id.</summary>
    private async Task SeedCustomerAsync(Guid customerId)
    {
        var customer = new Customer(new TenantId(TenantId), "Buyer", "+84900000000");
        SetProp(customer, "Id", customerId);
        SetProp(customer, "CustomerId", new CustomerId(customerId));
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
    }

    /// <summary>OrderItem.ProductId has an FK to Products — seed a Product row with the matching Id.</summary>
    private async Task SeedProductAsync(Guid productId)
    {
        if (await _context.Products.IgnoreQueryFilters().AnyAsync(p => p.Id == productId))
            return;

        var product = new Product(new TenantId(TenantId), "P-" + productId.ToString()[..8], 100m, "Test");
        SetProp(product, "Id", productId);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a completed order attributed to a salesman. The order MUST contain a line for the
    /// referred product — commission is per-product (referred line SubTotal), not per-order.
    /// </summary>
    private async Task SeedOrderWithSalesmanAsync(Guid orderId, Guid salesmanId, Guid productId, decimal total,
        Guid? buyerCustomerId = null, params (Guid ProductId, decimal UnitPrice, int Quantity, decimal VatRate)[] extraItems)
    {
        await SeedProductAsync(productId);
        foreach (var extra in extraItems)
            await SeedProductAsync(extra.ProductId);

        var items = new List<OrderItem>
        {
            // Referred line — vatRate 0 so SubTotal == total (keeps assertions readable).
            OrderItem.Create(Guid.NewGuid(), new TenantId(TenantId), orderId, productId,
                quantity: 1, unitPrice: total, productName: "Referred", vatRate: 0m)
        };
        foreach (var extra in extraItems)
        {
            items.Add(OrderItem.Create(Guid.NewGuid(), new TenantId(TenantId), orderId, extra.ProductId,
                quantity: extra.Quantity, unitPrice: extra.UnitPrice, productName: "Other", vatRate: extra.VatRate));
        }

        var order = Order.Create(orderId, new TenantId(TenantId), buyerCustomerId, items);
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
        // Referral QR opens the /scan page with the (escaped) composite code as a query param —
        // '|' is not safe in a URL path, and /r/{code} was never a real route.
        Assert.Contains("/scan?ref=", result.QrUrl);
        Assert.DoesNotContain("/r/", result.QrUrl);
    }

    // === T14 (CC-S4 fix): GetCompositeSalesmanQr uses the caller-supplied KhachLink host ===
    [Fact(DisplayName = "T14: GetCompositeSalesmanQr_UsesCallerSuppliedHost")]
    public async Task GetCompositeSalesmanQr_UsesCallerSuppliedHost()
    {
        await SeedSalesmanRoleAsync();
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var result = await _service.GetCompositeSalesmanQrAsync(SalesmanId, ProductId, "diemthuong2.khachvip.online");

        Assert.NotNull(result);
        Assert.StartsWith("https://diemthuong2.khachvip.online/scan?ref=", result!.QrUrl);
        // '|' must be percent-encoded, and the composite code must round-trip
        Assert.Contains("%7C", result.QrUrl);
        var encoded = result.QrUrl.Substring(result.QrUrl.IndexOf("ref=", StringComparison.Ordinal) + 4);
        Assert.Equal(result.CompositeCode, Uri.UnescapeDataString(encoded));
    }

    // === T14b (CC-S4 fix): full origin is accepted as-is (no double scheme) ===
    [Fact(DisplayName = "T14b: GetCompositeSalesmanQr_AcceptsFullOrigin")]
    public async Task GetCompositeSalesmanQr_AcceptsFullOrigin()
    {
        await SeedSalesmanRoleAsync();
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var result = await _service.GetCompositeSalesmanQrAsync(SalesmanId, ProductId, "https://commienphi.timlathay.com/");

        Assert.NotNull(result);
        Assert.StartsWith("https://commienphi.timlathay.com/scan?ref=", result!.QrUrl);
    }

    // === T15 (CC-S4 fix): ResolveReferralForScan returns the referred product ===
    [Fact(DisplayName = "T15: ResolveReferralForScan_ReturnsProduct")]
    public async Task ResolveReferralForScan_ReturnsProduct()
    {
        await SeedSalesmanRoleAsync();
        await SeedFeaturedProductAsync(ProductId, TenantId, "Product 1", 50000);
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var role = await _context.CommunityRoles.IgnoreQueryFilters().FirstAsync();
        var compositeCode = $"{role.SalesmanCode}|TR-001";

        var result = await _service.ResolveReferralForScanAsync(compositeCode);

        Assert.NotNull(result);
        Assert.Equal(SalesmanId, result!.SalesmanId);
        Assert.Equal(ProductId, result.ProductId);
        Assert.Equal(TenantId, result.TenantId);
        Assert.Equal("Product 1", result.Name);
        Assert.Equal(50000, result.Price);
        Assert.Equal("TR-001", result.ProductShortCode);
    }

    // === T16 (CC-S4 fix): ResolveReferralForScan unknown code → null ===
    [Fact(DisplayName = "T16: ResolveReferralForScan_InvalidCode_ReturnsNull")]
    public async Task ResolveReferralForScan_InvalidCode_ReturnsNull()
    {
        var result = await _service.ResolveReferralForScanAsync("NOPE|XXXX");

        Assert.Null(result);
    }

    // === T17 (CC-S4 fix): commission base = REFERRED LINE only, not the whole order ===
    [Fact(DisplayName = "T17: CreateCommission_UsesReferredLineOnly_NotWholeOrder")]
    public async Task CreateCommission_UsesReferredLineOnly_NotWholeOrder()
    {
        await SeedSalesmanRoleAsync();
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var orderId = Guid.NewGuid();
        // Referred line 100,000 + another product 900,000 → order total 1,000,000.
        await SeedOrderWithSalesmanAsync(orderId, SalesmanId, ProductId, 100000,
            null, (Guid.NewGuid(), 900000m, 1, 0m));

        var referral = await _service.CreateCommissionAsync(orderId);

        Assert.NotNull(referral);
        Assert.Equal(100000m, referral!.CommissionBaseAmount);   // referred line only
        Assert.Equal(5000m, referral.CommissionAmount);          // 100000 × 0.05, NOT 1000000 × 0.05
    }

    // === T18 (CC-S4 fix): referred product not in the order → no commission ===
    [Fact(DisplayName = "T18: CreateCommission_ReferredProductNotInOrder_ReturnsNull")]
    public async Task CreateCommission_ReferredProductNotInOrder_ReturnsNull()
    {
        await SeedSalesmanRoleAsync();
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var orderId = Guid.NewGuid();
        // Order carries the referral attribution but only contains a DIFFERENT product.
        var otherProduct = Guid.NewGuid();
        await SeedProductAsync(otherProduct);
        var item = OrderItem.Create(Guid.NewGuid(), new TenantId(TenantId), orderId, otherProduct,
            quantity: 1, unitPrice: 200000m, productName: "Other", vatRate: 0m);
        var order = Order.Create(orderId, new TenantId(TenantId), null, [item]);
        SetProp(order, "Id", orderId);
        SetProp(order, "OrderId", new OrderId(orderId));
        SetProp(order, "OrderType", "DELIVERY");
        SetProp(order, "Status", new OrderStatusId("completed"));
        SetProp(order, "TotalAmount", 200000m);
        SetProp(order, "SalesmanId", SalesmanId);
        SetProp(order, "ReferralProductId", ProductId);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var referral = await _service.CreateCommissionAsync(orderId);

        Assert.Null(referral);
        Assert.Empty(_context.SalesReferrals.IgnoreQueryFilters().ToList());
    }

    // === T19 (CC-S4 fix): self-referral (buyer == salesman) → Rejected, not Pending ===
    [Fact(DisplayName = "T19: CreateCommission_SelfReferral_IsRejected")]
    public async Task CreateCommission_SelfReferral_IsRejected()
    {
        await SeedSalesmanRoleAsync();
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var orderId = Guid.NewGuid();
        // Buyer customer id == salesman id → self-referral (needs a Customer row for the FK)
        await SeedCustomerAsync(SalesmanId);
        await SeedOrderWithSalesmanAsync(orderId, SalesmanId, ProductId, 100000, buyerCustomerId: SalesmanId);

        var referral = await _service.CreateCommissionAsync(orderId);

        Assert.NotNull(referral);
        Assert.Equal(CommissionStatus.Rejected, referral!.CommissionStatus);
        Assert.Equal(100, referral.RiskScore);            // SelfReferral weight 100
        Assert.Contains("SelfReferral", referral.RiskFactors);
    }

    // === T20 (CC-S4 fix): base excludes VAT (calculator) ===
    [Fact(DisplayName = "T20: ReferralCommissionCalculator_ExcludesVat")]
    public async Task ReferralCommissionCalculator_ExcludesVat()
    {
        var orderId = Guid.NewGuid();
        var item = OrderItem.Create(Guid.NewGuid(), new TenantId(TenantId), orderId, ProductId,
            quantity: 2, unitPrice: 50000m, productName: "Referred", vatRate: 0.10m); // SubTotal 100k, VAT 10k
        var order = Order.Create(orderId, new TenantId(TenantId), null, [item]);
        SetProp(order, "ReferralProductId", ProductId);

        var config = new ProductReferralConfig(new TenantId(TenantId), ProductId, 0.05m, 0, "TR-001");

        Assert.Equal(100000m, ReferralCommissionCalculator.ComputeBase(order, config)); // not 110000
        Assert.Equal(100000m, ReferralCommissionCalculator.ReferredSubTotal(order));
    }

    // === T21 (CC-S4 fix): base is 0 when the referred product is absent (calculator) ===
    [Fact(DisplayName = "T21: ReferralCommissionCalculator_MissingProduct_ReturnsZero")]
    public async Task ReferralCommissionCalculator_MissingProduct_ReturnsZero()
    {
        var orderId = Guid.NewGuid();
        var item = OrderItem.Create(Guid.NewGuid(), new TenantId(TenantId), orderId, Guid.NewGuid(),
            quantity: 1, unitPrice: 50000m, productName: "Other", vatRate: 0m);
        var order = Order.Create(orderId, new TenantId(TenantId), null, [item]);
        SetProp(order, "ReferralProductId", ProductId);

        var config = new ProductReferralConfig(new TenantId(TenantId), ProductId, 0.05m, 0, "TR-001");

        Assert.Equal(0m, ReferralCommissionCalculator.ComputeBase(order, config));
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

    // === T13 (Issue #175): GetCompositeSalesmanQr_BackfillsNullSalesmanCode ===
    // Legacy roles created before the constructor assigned a code (or inserted via raw SQL)
    // have a NULL SalesmanCode. GetCompositeSalesmanQrAsync must backfill it instead of
    // returning null ("Không thể tạo mã QR").
    [Fact(DisplayName = "T13 (Issue #175): GetCompositeSalesmanQr_BackfillsNullSalesmanCode")]
    public async Task GetCompositeSalesmanQr_BackfillsNullSalesmanCode()
    {
        // Seed a Salesman role, then null out SalesmanCode to simulate a legacy row.
        var role = new CommunityRole(new TenantId(TenantId), SalesmanId, CommunityRoleType.Salesman, Guid.NewGuid());
        SetProp(role, "SalesmanCode", null);
        _context.CommunityRoles.Add(role);
        await _context.SaveChangesAsync();

        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var result = await _service.GetCompositeSalesmanQrAsync(SalesmanId, ProductId);

        // Backfill should have generated a code + persisted it + returned a QR.
        Assert.NotNull(result);
        Assert.Contains("|", result!.CompositeCode);
        Assert.Contains("TR-001", result.CompositeCode);
        Assert.False(string.IsNullOrEmpty(result.SalesmanCode));

        // Verify the code was persisted to the DB.
        var reloaded = await _context.CommunityRoles.IgnoreQueryFilters()
            .FirstAsync(r => r.CustomerId == SalesmanId && r.RoleType == CommunityRoleType.Salesman);
        Assert.False(string.IsNullOrEmpty(reloaded.SalesmanCode));
        Assert.Equal(reloaded.SalesmanCode, result.SalesmanCode);
    }

    // === Issue #178 ph2 — "Gian hàng của tôi" ===

    // T19: GetSalesmanStore returns configured products (composite QR + live price) and available-to-add.
    [Fact(DisplayName = "T19 (Issue #178): GetSalesmanStore_ReturnsConfiguredAndAddableProducts")]
    public async Task GetSalesmanStore_ReturnsConfiguredAndAddableProducts()
    {
        await SeedTenantAsync(TenantId, "Shop A", 10.8, 106.7);
        await SeedSalesmanRoleAsync();
        await SeedFeaturedProductAsync(ProductId, TenantId, "Product 1", 50000);
        var addableId = Guid.NewGuid();
        await SeedFeaturedProductAsync(addableId, TenantId, "Product 2", 30000);
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var store = await _service.GetSalesmanStoreAsync(SalesmanId);

        // Configured product → Products with deterministic composite QR + live catalog price.
        Assert.Single(store.Products);
        var product = store.Products[0];
        Assert.Equal(ProductId, product.ProductId);
        Assert.Equal("Product 1", product.Name);
        Assert.Equal(50000m, product.Price);
        Assert.Equal("Shop A", product.ShopName);
        Assert.Equal(0.05m, product.CommissionRate);
        Assert.Equal(10000m, product.AppInstallBonus);
        Assert.Equal("TR-001", product.ProductShortCode);
        Assert.Contains("|TR-001", product.CompositeCode);
        Assert.StartsWith("https://diemthuong.khachvip.online/scan?ref=", product.QrUrl);

        // Non-configured product → AvailableForAdd.
        Assert.Single(store.AvailableForAdd);
        Assert.Equal(addableId, store.AvailableForAdd[0].ProductId);
        Assert.Equal("Product 2", store.AvailableForAdd[0].Name);
        Assert.Equal(30000m, store.AvailableForAdd[0].Price);
    }

    // T20: Not a salesman → empty store (no crash).
    [Fact(DisplayName = "T20 (Issue #178): GetSalesmanStore_NoSalesmanRole_ReturnsEmpty")]
    public async Task GetSalesmanStore_NoSalesmanRole_ReturnsEmpty()
    {
        var store = await _service.GetSalesmanStoreAsync(Guid.NewGuid());
        Assert.Empty(store.Products);
        Assert.Empty(store.AvailableForAdd);
    }

    // T21: AddProductToStore creates config with Issue #178 defaults (0.01 / 1000).
    [Fact(DisplayName = "T21 (Issue #178): AddProductToStore_CreatesConfigWithDefaults")]
    public async Task AddProductToStore_CreatesConfigWithDefaults()
    {
        await SeedTenantAsync(TenantId, "Shop A", 10.8, 106.7);
        await SeedSalesmanRoleAsync();
        await SeedFeaturedProductAsync(ProductId, TenantId, "Product 1", 50000);

        var product = await _service.AddProductToStoreAsync(SalesmanId, ProductId, "diemthuong2.khachvip.online");

        Assert.NotNull(product);
        Assert.Equal(ProductId, product!.ProductId);
        Assert.Equal(0.01m, product.CommissionRate);
        Assert.Equal(1000m, product.AppInstallBonus);
        Assert.False(string.IsNullOrEmpty(product.ProductShortCode));
        Assert.Contains("|", product.CompositeCode);
        Assert.StartsWith("https://diemthuong2.khachvip.online/scan?ref=", product.QrUrl);

        var config = await _context.ProductReferralConfigs.IgnoreQueryFilters()
            .FirstAsync(c => c.ProductId == ProductId);
        Assert.True(config.IsActive);
        Assert.Equal(0.01m, config.CommissionRate);
    }

    // T22: Add a product that already has a config → null.
    [Fact(DisplayName = "T22 (Issue #178): AddProductToStore_AlreadyConfigured_ReturnsNull")]
    public async Task AddProductToStore_AlreadyConfigured_ReturnsNull()
    {
        await SeedTenantAsync(TenantId, "Shop A", 10.8, 106.7);
        await SeedSalesmanRoleAsync();
        await SeedFeaturedProductAsync(ProductId, TenantId, "Product 1", 50000);
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var product = await _service.AddProductToStoreAsync(SalesmanId, ProductId);

        Assert.Null(product);
    }

    // T23: RemoveProductFromStore soft-deactivates the config.
    [Fact(DisplayName = "T23 (Issue #178): RemoveProductFromStore_DeactivatesConfig")]
    public async Task RemoveProductFromStore_DeactivatesConfig()
    {
        await SeedSalesmanRoleAsync();
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var removed = await _service.RemoveProductFromStoreAsync(SalesmanId, ProductId);

        Assert.True(removed);
        var config = await _context.ProductReferralConfigs.IgnoreQueryFilters()
            .FirstAsync(c => c.ProductId == ProductId);
        Assert.False(config.IsActive);

        // Second removal → config exists but already inactive — still returns true (idempotent soft remove).
        var removedAgain = await _service.RemoveProductFromStoreAsync(SalesmanId, ProductId);
        Assert.True(removedAgain);
    }

    // T24: GetSalesmanStore backfills a NULL SalesmanCode (legacy role) like GetCompositeSalesmanQr.
    [Fact(DisplayName = "T24 (Issue #178): GetSalesmanStore_BackfillsNullSalesmanCode")]
    public async Task GetSalesmanStore_BackfillsNullSalesmanCode()
    {
        await SeedTenantAsync(TenantId, "Shop A", 10.8, 106.7);
        var role = new CommunityRole(new TenantId(TenantId), SalesmanId, CommunityRoleType.Salesman, Guid.NewGuid());
        SetProp(role, "SalesmanCode", null);
        _context.CommunityRoles.Add(role);
        await _context.SaveChangesAsync();
        await SeedFeaturedProductAsync(ProductId, TenantId, "Product 1", 50000);
        await SeedProductReferralConfigAsync(ProductId, 0.05m, 10000, "TR-001");

        var store = await _service.GetSalesmanStoreAsync(SalesmanId);

        Assert.Single(store.Products);
        Assert.False(string.IsNullOrEmpty(store.Products[0].CompositeCode));
        Assert.Contains("|TR-001", store.Products[0].CompositeCode);

        var reloaded = await _context.CommunityRoles.IgnoreQueryFilters()
            .FirstAsync(r => r.CustomerId == SalesmanId && r.RoleType == CommunityRoleType.Salesman);
        Assert.False(string.IsNullOrEmpty(reloaded.SalesmanCode));
    }
}
