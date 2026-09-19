using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community;

/// <summary>
/// CC-S4 (Sprint 4): ProductReferralConfigService unit tests — admin CRUD.
/// 4 test cases per detailed plan. Uses SQLite in-memory.
/// Tenant-aware: CreateAsync requires explicit tenantId.
/// </summary>
public class ProductReferralConfigServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly ProductReferralConfigService _service;
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ProductReferralConfigServiceTests()
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
        _service = new ProductReferralConfigService(_context, NullLogger<ProductReferralConfigService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // === T17: Create_ValidFields_ReturnsConfig ===
    [Fact(DisplayName = "T17: Create_ValidFields_ReturnsConfig")]
    public async Task Create_ValidFields_ReturnsConfig()
    {
        var result = await _service.CreateAsync(ProductId, TenantId, 0.05m, 10000, "TR-001");

        Assert.NotNull(result);
        Assert.Equal(ProductId, result.ProductId);
        Assert.Equal(TenantId, result.TenantId);
        Assert.Equal(0.05m, result.CommissionRate);
        Assert.Equal(10000, result.AppInstallBonus);
        Assert.Equal("TR-001", result.ProductShortCode);
        Assert.True(result.IsActive);
    }

    // === T18: Create_InvalidRate_Throws ===
    // Issue #178: range widened 0.02-0.05 → 0-0.5 (Free/Charity products use rate=0).
    [Fact(DisplayName = "T18: Create_InvalidRate_Throws")]
    public async Task Create_InvalidRate_Throws()
    {
        // Rate < 0
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.CreateAsync(ProductId, TenantId, -0.01m, 10000, "TR-001"));

        // Rate > 0.5
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.CreateAsync(ProductId, TenantId, 0.51m, 10000, "TR-001"));
    }

    // === T18b: Create_ZeroRate_Accepted (Issue #178 — Free/Charity) ===
    [Fact(DisplayName = "T18b: Create_ZeroRate_Accepted")]
    public async Task Create_ZeroRate_Accepted()
    {
        var result = await _service.CreateAsync(ProductId, TenantId, 0m, 0, "TR-FREE");

        Assert.NotNull(result);
        Assert.Equal(0m, result.CommissionRate);
        Assert.Equal(0, result.AppInstallBonus);
        Assert.True(result.IsActive);
    }

    // === T19: Update_ModifiesFields ===
    [Fact(DisplayName = "T19: Update_ModifiesFields")]
    public async Task Update_ModifiesFields()
    {
        await _service.CreateAsync(ProductId, TenantId, 0.03m, 5000, "TR-001");

        var updated = await _service.UpdateAsync(ProductId, 0.05m, 15000, "TR-002", true);

        Assert.Equal(0.05m, updated.CommissionRate);
        Assert.Equal(15000, updated.AppInstallBonus);
        Assert.Equal("TR-002", updated.ProductShortCode);
    }

    // === T20: Deactivate_SetsIsActiveFalse ===
    [Fact(DisplayName = "T20: Deactivate_SetsIsActiveFalse")]
    public async Task Deactivate_SetsIsActiveFalse()
    {
        await _service.CreateAsync(ProductId, TenantId, 0.05m, 10000, "TR-001");

        await _service.DeactivateAsync(ProductId);

        var config = await _context.ProductReferralConfigs.IgnoreQueryFilters().FirstAsync();
        Assert.False(config.IsActive);
    }

    // === T21: ShortCode_PerTenant_Uniqueness ===
    [Fact(DisplayName = "T21: ShortCode_PerTenant_Uniqueness")]
    public async Task ShortCode_PerTenant_Uniqueness()
    {
        // Same short code, different tenant → both succeed (per-tenant uniqueness)
        var tenant2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var product2 = Guid.NewGuid();

        await _service.CreateAsync(ProductId, TenantId, 0.03m, 5000, "TR-001");
        await _service.CreateAsync(product2, tenant2, 0.03m, 5000, "TR-001");

        // Same short code, same tenant → conflict
        var product3 = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(product3, TenantId, 0.03m, 5000, "TR-001"));
    }
}
