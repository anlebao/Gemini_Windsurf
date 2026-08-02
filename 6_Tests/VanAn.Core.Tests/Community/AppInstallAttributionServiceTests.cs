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
/// CC-S4 (Sprint 4): AppInstallAttributionService unit tests.
/// 4 test cases per detailed plan. Uses SQLite in-memory.
/// </summary>
public class AppInstallAttributionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly AppInstallAttributionService _service;
    private static readonly Guid SalesmanId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.NewGuid();

    public AppInstallAttributionServiceTests()
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
        _service = new AppInstallAttributionService(_context, riskService, fraudFlagService, NullLogger<AppInstallAttributionService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task SeedSalesmanAndConfigAsync(decimal bonus = 10000)
    {
        var role = new CommunityRole(new TenantId(TenantId), SalesmanId, CommunityRoleType.Salesman, Guid.NewGuid());
        _context.CommunityRoles.Add(role);

        var config = new ProductReferralConfig(new TenantId(TenantId), ProductId, 0.05m, bonus, "TR-001");
        _context.ProductReferralConfigs.Add(config);

        await _context.SaveChangesAsync();
    }

    private string GetCompositeCode()
    {
        var role = _context.CommunityRoles.IgnoreQueryFilters().First();
        return $"{role.SalesmanCode}|TR-001";
    }

    // === T13: AttributeInstall_Valid_CreatesAttributionNoWallet ===
    [Fact(DisplayName = "T13: AttributeInstall_Valid_CreatesAttributionNoWallet")]
    public async Task AttributeInstall_Valid_CreatesAttributionNoWallet()
    {
        await SeedSalesmanAndConfigAsync();
        var code = GetCompositeCode();

        var result = await _service.AttributeInstallAsync(CustomerId, code);

        Assert.NotNull(result);
        Assert.Equal(CustomerId, result!.CustomerId);
        Assert.Equal(SalesmanId, result.SalesmanId);
        Assert.Equal(10000, result.BonusAmount);
        // v1.4: No WalletTransaction created (cooling period)
        var walletCount = await _context.WalletTransactions.IgnoreQueryFilters().CountAsync();
        Assert.Equal(0, walletCount);
    }

    // === T14: AttributeInstall_DoubleAttribute_ThrowsConflict ===
    [Fact(DisplayName = "T14: AttributeInstall_DoubleAttribute_ThrowsConflict")]
    public async Task AttributeInstall_DoubleAttribute_ThrowsConflict()
    {
        await SeedSalesmanAndConfigAsync();
        var code = GetCompositeCode();

        await _service.AttributeInstallAsync(CustomerId, code);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AttributeInstallAsync(CustomerId, code));
    }

    // === T15: AttributeInstall_NoBonus_ZeroWalletTransaction ===
    [Fact(DisplayName = "T15: AttributeInstall_NoBonus_ZeroWalletTransaction")]
    public async Task AttributeInstall_NoBonus_ZeroWalletTransaction()
    {
        await SeedSalesmanAndConfigAsync(bonus: 0);
        var code = GetCompositeCode();

        var result = await _service.AttributeInstallAsync(CustomerId, code);

        Assert.NotNull(result);
        Assert.Equal(0, result!.BonusAmount);
        var walletCount = await _context.WalletTransactions.IgnoreQueryFilters().CountAsync();
        Assert.Equal(0, walletCount);
    }

    // === T16: AttributeInstall_InvalidCode_ReturnsNull ===
    [Fact(DisplayName = "T16: AttributeInstall_InvalidCode_ReturnsNull")]
    public async Task AttributeInstall_InvalidCode_ReturnsNull()
    {
        await SeedSalesmanAndConfigAsync();

        var result = await _service.AttributeInstallAsync(CustomerId, "INVALID|CODE");

        Assert.Null(result);
    }
}
