using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community;

/// <summary>
/// Sprint 7 Q3 — CommunityFundService unit tests (T22-T24).
/// Insufficient balance rejection, valid spend creates tx + record, history paginated.
/// </summary>
public class CommunityFundServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _context;
    private readonly WalletService _walletService;
    private readonly CommunityFundService _service;
    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly TenantId _tenantId = new(Guid.Empty);

    public CommunityFundServiceTests()
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

        var tenantProvider = new StubTenantProvider(Guid.Empty);
        _walletService = new WalletService(_context, tenantProvider, NullLogger<WalletService>.Instance);
        _service = new CommunityFundService(_context, _walletService, NullLogger<CommunityFundService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void SeedFundBalance(decimal amount)
    {
        // Create a CommunityFund tx to give the fund some balance (via WalletService for atomicity)
        _walletService.CreateTransactionAsync(
            SystemWalletIds.CommunityFund,
            WalletTransactionType.CommunityFund,
            amount,
            "Seed fund").GetAwaiter().GetResult();
    }

    // T22: Insufficient balance rejected
    [Fact]
    public async Task CommunityFundSpend_InsufficientBalance_Rejected()
    {
        SeedFundBalance(50000); // fund has 50K
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SpendAsync(100000, "Test spend", "Test recipient", AdminId));
    }

    // T23: Valid spend creates tx + audit record
    [Fact]
    public async Task CommunityFundSpend_Valid_CreatesTxAndRecord()
    {
        SeedFundBalance(200000); // fund has 200K

        var result = await _service.SpendAsync(50000, "Tài trợ sự kiện cộng đồng X", "Ban tổ chức X", AdminId);

        Assert.NotEqual(Guid.Empty, result.TransactionId);
        Assert.NotEqual(Guid.Empty, result.SpendRecordId);
        Assert.Equal(150000m, result.BalanceAfter);

        // Verify wallet tx created
        var tx = await _context.WalletTransactions.FindAsync(result.TransactionId);
        Assert.NotNull(tx);
        Assert.Equal(WalletTransactionType.CommunityFundSpend, tx!.Type);
        Assert.Equal(-50000m, tx.Amount); // negative = money out
        Assert.Equal(SystemWalletIds.CommunityFund, tx.OwnerId);

        // Verify audit record created
        var record = await _context.CommunityFundSpendRecords.FindAsync(result.SpendRecordId);
        Assert.NotNull(record);
        Assert.Equal(50000m, record!.Amount);
        Assert.Equal("Tài trợ sự kiện cộng đồng X", record.Reason);
        Assert.Equal("Ban tổ chức X", record.Recipient);
        Assert.Equal(AdminId, record.ApprovedBy);
        Assert.Equal(result.TransactionId, record.WalletTransactionId);
    }

    // T24: History paginated
    [Fact]
    public async Task CommunityFundSpend_History_Paginated()
    {
        SeedFundBalance(500000); // fund has 500K

        await _service.SpendAsync(50000, "Spend 1", "Recipient 1", AdminId);
        await _service.SpendAsync(50000, "Spend 2", "Recipient 2", AdminId);
        await _service.SpendAsync(50000, "Spend 3", "Recipient 3", AdminId);

        var page1 = await _service.GetHistoryAsync(1, 2);
        Assert.Equal(3, page1.Total);
        Assert.Equal(2, page1.Items.Count);

        var page2 = await _service.GetHistoryAsync(2, 2);
        Assert.Equal(3, page2.Total);
        Assert.Single(page2.Items);

        // Verify newest first (descending by SpentAt)
        Assert.Equal("Spend 3", page1.Items[0].Reason);
        Assert.Equal("Spend 2", page1.Items[1].Reason);
        Assert.Equal("Spend 1", page2.Items[0].Reason);
    }

    // Balance check: empty fund has 0 balance
    [Fact]
    public async Task GetBalance_Empty_ReturnsZero()
    {
        var balance = await _service.GetBalanceAsync();
        Assert.Equal(0m, balance.Balance);
        Assert.Equal(0m, balance.TotalCollected);
        Assert.Equal(0m, balance.TotalSpent);
    }

    // Balance check: collected minus spent
    [Fact]
    public async Task GetBalance_AfterSpend_ReturnsNet()
    {
        SeedFundBalance(300000);
        await _service.SpendAsync(100000, "Test", "Recipient", AdminId);

        var balance = await _service.GetBalanceAsync();
        Assert.Equal(200000m, balance.Balance);
        Assert.Equal(300000m, balance.TotalCollected);
        Assert.Equal(100000m, balance.TotalSpent);
    }

    private class StubTenantProvider : VanAn.Shared.Domain.Common.ITenantProvider
    {
        private readonly Guid _tenantId;
        public StubTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid TenantId => _tenantId;
        public string? CurrentUser => "test";
        public bool HasTenant => true;
        public void SetTenant(Guid tenantId) { /* no-op */ }
    }
}
