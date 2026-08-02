using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Common;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community;

/// <summary>
/// Sprint 7 — CommerceModeService unit tests (T1-T7) + Order snapshot tests (T16-T18).
/// Tests global mode, tenant override, resolve, order snapshot, marketplace null fields, reseller pricing.
/// </summary>
public class CommerceModeServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly CommerceModeService _service;
    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly TenantId _tenantId = new(TenantGuid);

    public CommerceModeServiceTests()
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
        _service = new CommerceModeService(_context, NullLogger<CommerceModeService>.Instance);

        SeedTenant();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void SeedTenant()
    {
        var tenant = Tenant.CreateCompany(_tenantId, "Test Tenant", TenantSettings.Empty());
        _context.Tenants.Add(tenant);
        _context.SaveChanges();
    }

    // T1: Default global mode = Marketplace
    [Fact]
    public async Task GetGlobalMode_Default_ReturnsMarketplace()
    {
        var settings = await _service.GetSettingsAsync();
        Assert.Equal(CommerceMode.Marketplace, settings.GlobalMode);
    }

    // T2: Set global mode changes mode
    [Fact]
    public async Task SetGlobalMode_ChangesMode()
    {
        await _service.SetGlobalModeAsync(CommerceMode.Reseller, 0.30m, 0.05m, 15000m, AdminId);
        var settings = await _service.GetSettingsAsync();
        Assert.Equal(CommerceMode.Reseller, settings.GlobalMode);
        Assert.Equal(0.30m, settings.DefaultPlatformFeeRate);
        Assert.Equal(0.05m, settings.DefaultCommunityFundRate);
        Assert.Equal(15000m, settings.DefaultDeliveryFee);
    }

    // T3: Tenant with Inherit → resolves to global
    [Fact]
    public async Task GetTenantMode_Inherit_ReturnsGlobal()
    {
        await _service.SetGlobalModeAsync(CommerceMode.Reseller, 0.30m, 0.05m, 15000m, AdminId);
        var mode = await _service.ResolveModeForTenantAsync(TenantGuid);
        Assert.Equal(CommerceMode.Reseller, mode);
    }

    // T4: Tenant with override → returns override
    [Fact]
    public async Task GetTenantMode_Override_ReturnsOverride()
    {
        await _service.SetGlobalModeAsync(CommerceMode.Reseller, 0.30m, 0.05m, 15000m, AdminId);
        await _service.SetTenantOverrideAsync(TenantGuid, CommerceMode.Marketplace, AdminId);
        var mode = await _service.ResolveModeForTenantAsync(TenantGuid);
        Assert.Equal(CommerceMode.Marketplace, mode);
    }

    // T5: Set tenant override persists
    [Fact]
    public async Task SetTenantOverride_Persists()
    {
        await _service.SetTenantOverrideAsync(TenantGuid, CommerceMode.Reseller, AdminId);
        var settings = await _service.GetSettingsAsync();
        var overrideRow = settings.TenantOverrides.FirstOrDefault(t => t.TenantId == TenantGuid);
        Assert.NotNull(overrideRow);
        Assert.Equal(CommerceMode.Reseller, overrideRow!.Override);
        Assert.Equal(CommerceMode.Reseller, overrideRow.ResolvedMode);
    }

    // T6: Resolve for order — Inherit uses global
    [Fact]
    public async Task ResolveModeForOrder_Inherit_UsesGlobal()
    {
        await _service.SetGlobalModeAsync(CommerceMode.Reseller, 0.30m, 0.05m, 15000m, AdminId);
        var mode = await _service.ResolveModeForTenantAsync(TenantGuid);
        Assert.Equal(CommerceMode.Reseller, mode); // Inherit → global = Reseller
    }

    // T7: Resolve for order — Override uses tenant
    [Fact]
    public async Task ResolveModeForOrder_Override_UsesTenant()
    {
        await _service.SetGlobalModeAsync(CommerceMode.Marketplace, 0.30m, 0.05m, 15000m, AdminId);
        await _service.SetTenantOverrideAsync(TenantGuid, CommerceMode.Reseller, AdminId);
        var mode = await _service.ResolveModeForTenantAsync(TenantGuid);
        Assert.Equal(CommerceMode.Reseller, mode); // Override = Reseller (not global Marketplace)
    }

    // T16: Order snapshots commerce mode at creation
    [Fact]
    public void Order_SnapshotsCommerceMode_AtCreation()
    {
        var order = new Order(_tenantId, null, 100000);
        Assert.Equal(CommerceMode.Marketplace, order.CommerceMode); // default

        order.SetResellerPricing(80000, 100000, 20000, 15000, 0.30m, 0.05m);
        Assert.Equal(CommerceMode.Reseller, order.CommerceMode);
    }

    // T17: Marketplace order has null cost price
    [Fact]
    public void Order_Marketplace_NullCostPrice()
    {
        var order = new Order(_tenantId, null, 100000);
        Assert.Equal(CommerceMode.Marketplace, order.CommerceMode);
        Assert.Null(order.CostPrice);
        Assert.Null(order.SellPrice);
        Assert.Null(order.PlatformMargin);
        Assert.Null(order.DeliveryFee);
        Assert.Null(order.PlatformFeeRate);
        Assert.Null(order.CommunityFundRate);
    }

    // T18: Reseller order has all pricing fields
    [Fact]
    public void Order_Reseller_HasAllPricingFields()
    {
        var order = new Order(_tenantId, null, 100000);
        order.SetResellerPricing(80000, 100000, 20000, 15000, 0.30m, 0.05m);
        Assert.Equal(CommerceMode.Reseller, order.CommerceMode);
        Assert.Equal(80000m, order.CostPrice);
        Assert.Equal(100000m, order.SellPrice);
        Assert.Equal(20000m, order.PlatformMargin);
        Assert.Equal(15000m, order.DeliveryFee);
        Assert.Equal(0.30m, order.PlatformFeeRate);
        Assert.Equal(0.05m, order.CommunityFundRate);
    }

    // Validation: PlatformFeeRate + CommunityFundRate > 1 should throw
    [Fact]
    public async Task SetGlobalMode_RatesExceed100Percent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SetGlobalModeAsync(CommerceMode.Reseller, 0.80m, 0.30m, 15000m, AdminId));
    }

    // Validation: Global mode cannot be Inherit
    [Fact]
    public async Task SetGlobalMode_Inherit_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SetGlobalModeAsync(CommerceMode.Inherit, 0.30m, 0.05m, 15000m, AdminId));
    }
}
