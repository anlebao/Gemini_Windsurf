using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Alliance Phase 4 — tests for mode switch migration (Silo→Alliance + Alliance→Silo).
/// ConsolidateWalletsAsync: merges per-tenant SQLite balances into PG AllianceWallets.
/// SplitWalletsAsync: distributes PG AllianceWallet balances back to per-tenant proportionally.
/// Uses SQLite in-memory via VanAnDbContextTestFactory.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0 (Q1: chia theo nguồn).
/// </summary>
public class ConsolidateWalletsTests : IDisposable
{
    private readonly TestContextScope _scope;
    private readonly VanAnDbContext _db;
    private readonly Mock<ILoyaltyModeResolver> _modeResolverMock;
    private readonly AllianceWalletService _sut;

    private static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public ConsolidateWalletsTests()
    {
        _scope = VanAnDbContextTestFactory.Create();
        _db = _scope.Context;

        _modeResolverMock = new Mock<ILoyaltyModeResolver>();
        _modeResolverMock
            .Setup(m => m.GetEffectiveMaxWalletPointsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(1_000_000); // high cap for migration

        _sut = new AllianceWalletService(
            _db,
            _modeResolverMock.Object,
            natsEventPublisher: null,
            NullLogger<AllianceWalletService>.Instance);
    }

    public void Dispose() => _scope.Dispose();

    // ──────────────────────────────────────────────────────────
    // ConsolidateWalletsAsync (Silo → Alliance)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-MG-1: ConsolidateWallets — creates wallets + ADJUST transactions for each customer")]
    public async Task ConsolidateWallets_MultipleCustomers_CreatesWalletsAndTransactions()
    {
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();

        var inputs = new List<CustomerBalanceInput>
        {
            new(device1, 500, "0901111111"),
            new(device2, 300, "0902222222")
        };

        var result = await _sut.ConsolidateWalletsAsync(TenantA, inputs, "admin");

        result.Success.Should().BeTrue();
        result.CustomersProcessed.Should().Be(2);
        result.TotalPointsTransferred.Should().Be(800);

        // Verify wallets created
        var wallet1 = await _sut.GetWalletByDeviceIdAsync(device1);
        wallet1.Should().NotBeNull();
        wallet1!.TotalPointBalance.Should().Be(500);

        var wallet2 = await _sut.GetWalletByDeviceIdAsync(device2);
        wallet2.Should().NotBeNull();
        wallet2!.TotalPointBalance.Should().Be(300);

        // Verify ADJUST transactions
        var txs1 = await _sut.GetTransactionsByTenantAsync(wallet1.Id, TenantA);
        txs1.Should().HaveCount(1);
        txs1[0].Type.Should().Be(AllianceTransactionType.ADJUST);
        txs1[0].Points.Should().Be(500);
        txs1[0].Reason.Should().Contain("Silo→Alliance migration");
    }

    [Fact(DisplayName = "LA-MG-2: ConsolidateWallets — skips zero-balance customers")]
    public async Task ConsolidateWallets_ZeroBalance_SkipsCustomer()
    {
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();

        var inputs = new List<CustomerBalanceInput>
        {
            new(device1, 500, "0901111111"),
            new(device2, 0, "0902222222") // zero balance
        };

        var result = await _sut.ConsolidateWalletsAsync(TenantA, inputs, "admin");

        result.CustomersProcessed.Should().Be(1);
        result.TotalPointsTransferred.Should().Be(500);

        // device2 should NOT have a wallet
        var wallet2 = await _sut.GetWalletByDeviceIdAsync(device2);
        wallet2.Should().BeNull();
    }

    [Fact(DisplayName = "LA-MG-3: ConsolidateWallets — idempotent (second call skips already-migrated)")]
    public async Task ConsolidateWallets_SecondCall_IdempotentSkip()
    {
        var device = Guid.NewGuid();
        var inputs = new List<CustomerBalanceInput> { new(device, 500, "0901111111") };

        // First migration
        var result1 = await _sut.ConsolidateWalletsAsync(TenantA, inputs, "admin");
        result1.CustomersProcessed.Should().Be(1);

        // Second migration — should skip (idempotent)
        var result2 = await _sut.ConsolidateWalletsAsync(TenantA, inputs, "admin");
        result2.CustomersProcessed.Should().Be(0);
        result2.TotalPointsTransferred.Should().Be(0);

        // Wallet balance should NOT have doubled
        var wallet = await _sut.GetWalletByDeviceIdAsync(device);
        wallet!.TotalPointBalance.Should().Be(500); // not 1000
    }

    [Fact(DisplayName = "LA-MG-4: ConsolidateWallets — empty list returns empty result")]
    public async Task ConsolidateWallets_EmptyList_ReturnsEmpty()
    {
        var result = await _sut.ConsolidateWalletsAsync(TenantA, new List<CustomerBalanceInput>(), "admin");

        result.Success.Should().BeTrue();
        result.CustomersProcessed.Should().Be(0);
        result.TotalPointsTransferred.Should().Be(0);
    }

    // ──────────────────────────────────────────────────────────
    // SplitWalletsAsync (Alliance → Silo)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-MG-5: SplitWallets — distributes balance proportionally by net EARN")]
    public async Task SplitWallets_ProportionalDistribution_ByNetEarn()
    {
        // Setup: wallet with EARN from 2 tenants
        var device = Guid.NewGuid();
        var wallet = new AllianceWallet(device, "0901234567");
        _db.AllianceWallets.Add(wallet);
        await _db.SaveChangesAsync();

        // EARN 300 from TenantA, EARN 100 from TenantB → total 400
        await _sut.AddPointsAsync(device, TenantA, 300, "Order A1");
        await _sut.AddPointsAsync(device, TenantB, 100, "Order B1");

        // Verify balance
        var updatedWallet = await _sut.GetWalletByDeviceIdAsync(device);
        updatedWallet!.TotalPointBalance.Should().Be(400);

        // Split for TenantA
        var result = await _sut.SplitWalletsAsync(TenantA, "admin");

        result.Success.Should().BeTrue();
        result.CustomersProcessed.Should().Be(1);

        // Allocations: TenantA gets 300/400 * 400 = 300, TenantB gets 100/400 * 400 = 100
        result.Allocations.Should().HaveCount(2);
        var allocA = result.Allocations.First(a => a.TenantId == TenantA);
        allocA.Points.Should().Be(300);
        var allocB = result.Allocations.First(a => a.TenantId == TenantB);
        allocB.Points.Should().Be(100);

        // Wallet should be frozen
        var frozenWallet = await _sut.GetWalletByDeviceIdAsync(device);
        frozenWallet!.IsActive.Should().BeFalse();
        frozenWallet.TotalPointBalance.Should().Be(0); // deducted to 0 via ADJUST transactions
    }

    [Fact(DisplayName = "LA-MG-6: SplitWallets — tenant with net EARN ≤ 0 gets no allocation")]
    public async Task SplitWallets_NegativeNetEarn_NoAllocation()
    {
        var device = Guid.NewGuid();
        var wallet = new AllianceWallet(device, "0901234567");
        _db.AllianceWallets.Add(wallet);
        await _db.SaveChangesAsync();

        // EARN 200 from TenantA, REDEEM 50 at TenantA → netA = 150
        // EARN 100 from TenantB, REDEEM 150 at TenantB → netB = -50 (≤ 0)
        await _sut.AddPointsAsync(device, TenantA, 200, "Order A1");
        await _sut.AddPointsAsync(device, TenantB, 100, "Order B1");
        await _sut.DeductPointsAsync(device, TenantA, 50, "Redeem A1");
        await _sut.DeductPointsAsync(device, TenantB, 150, "Redeem B1");

        // Balance: 200 + 100 - 50 - 150 = 100
        var updatedWallet = await _sut.GetWalletByDeviceIdAsync(device);
        updatedWallet!.TotalPointBalance.Should().Be(100);

        // Split for TenantA
        var result = await _sut.SplitWalletsAsync(TenantA, "admin");

        // Only TenantA should get allocation (netB ≤ 0 → no allocation)
        result.Allocations.Should().HaveCount(1);
        result.Allocations[0].TenantId.Should().Be(TenantA);
        result.Allocations[0].Points.Should().Be(100); // 100% to TenantA
    }

    [Fact(DisplayName = "LA-MG-7: SplitWallets — no wallets for tenant returns empty")]
    public async Task SplitWallets_NoWallets_ReturnsEmpty()
    {
        var result = await _sut.SplitWalletsAsync(TenantA, "admin");

        result.Success.Should().BeTrue();
        result.CustomersProcessed.Should().Be(0);
        result.Allocations.Should().BeEmpty();
    }

    [Fact(DisplayName = "LA-MG-8: SplitWallets — skips inactive/frozen wallets")]
    public async Task SplitWallets_InactiveWallet_Skips()
    {
        var device = Guid.NewGuid();
        var wallet = new AllianceWallet(device, "0901234567");
        wallet.AddPoints(500);
        wallet.Freeze(); // already frozen
        _db.AllianceWallets.Add(wallet);
        await _db.SaveChangesAsync();

        // Add a transaction so the wallet is found by the query
        _db.AllianceTransactions.Add(new AllianceTransaction(
            walletId: wallet.Id, transactionTenantId: TenantA,
            type: AllianceTransactionType.EARN, points: 500, balanceAfter: 500, reason: "Test"));
        await _db.SaveChangesAsync();

        var result = await _sut.SplitWalletsAsync(TenantA, "admin");

        result.CustomersProcessed.Should().Be(0); // skipped frozen wallet
        result.Allocations.Should().BeEmpty();
    }
}
