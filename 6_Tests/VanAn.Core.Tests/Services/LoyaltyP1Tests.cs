using Microsoft.Data.Sqlite;
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
/// Loyalty P1 features — audit §6 Q3/Q4/Q6 decisions (docs/AI/tasks/loyalty_alliance_p1/).
///
/// P1.1: ExitMemberSettlementAsync — settle-then-burn: burns points attributed to an
///       exited member via ComputeNetEarnByTenantAsync, idempotent per wallet.
/// P1.2: GetSettlementReportAsync — read-only per-tenant earn/redeem/adjust aggregation
///       for off-system HTX settlement.
/// P1.3: AdjustPointsAsync — signed manual adjustment writing ADJUST ledger rows
///       (earn/redeem settlement math undistorted), atomic + idempotent.
/// </summary>
public class LoyaltyP1Tests
{
    private static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private sealed class WalletPair : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required VanAnDbContext Db1 { get; init; }
        public required AllianceWalletService Svc { get; init; }

        public async ValueTask DisposeAsync()
        {
            await Db1.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private static AllianceWalletService BuildWalletService(VanAnDbContext db, bool isAllianceMember = false)
    {
        var modeResolverMock = new Mock<ILoyaltyModeResolver>();
        modeResolverMock
            .Setup(m => m.GetEffectiveMaxWalletPointsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(int.MaxValue);
        modeResolverMock
            .Setup(m => m.IsAllianceMemberAsync(It.IsAny<Guid>()))
            .ReturnsAsync(isAllianceMember);
        return new AllianceWalletService(
            db,
            modeResolverMock.Object,
            natsEventPublisher: null,
            logger: NullLogger<AllianceWalletService>.Instance);
    }

    private static async Task<WalletPair> BuildAsync(bool isAllianceMember = false)
    {
        var conn = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        await conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VanAnDbContext>().UseSqlite(conn).Options;
        var db = new VanAnDbContext(options, new TestTenantProvider());
        _ = await db.Database.EnsureCreatedAsync();
        return new WalletPair { Connection = conn, Db1 = db, Svc = BuildWalletService(db, isAllianceMember) };
    }

    // ═══════════════════════════════════════════════════════════
    // P1.3 — AdjustPointsAsync
    // ═══════════════════════════════════════════════════════════

    [Fact(DisplayName = "P1.3-1: Adjust +points → ADJUST row, balance credited")]
    public async Task Adjust_Positive_Credits_AdjustRow()
    {
        await using var p = await BuildAsync();
        var deviceId = Guid.NewGuid();
        _ = await p.Svc.AddPointsAsync(deviceId, TenantA, 100, "seed");

        var r = await p.Svc.AdjustPointsAsync(deviceId, TenantA, 40, "complaint:ticket1", "adj:1");

        Assert.True(r.Success);
        Assert.Equal(140, r.NewBalance);
        var tx = await p.Db1.AllianceTransactions.SingleAsync(t => t.IdempotencyKey == "adj:1");
        Assert.Equal(AllianceTransactionType.ADJUST, tx.Type);
        Assert.Equal(40, tx.Points);
        Assert.Equal(140, tx.BalanceAfter);
    }

    [Fact(DisplayName = "P1.3-2: Adjust −points → ADJUST row negative, balance debited")]
    public async Task Adjust_Negative_Debits()
    {
        await using var p = await BuildAsync();
        var deviceId = Guid.NewGuid();
        _ = await p.Svc.AddPointsAsync(deviceId, TenantA, 100, "seed");

        var r = await p.Svc.AdjustPointsAsync(deviceId, TenantA, -30, "data-fix:double-earn", "adj:2");

        Assert.True(r.Success);
        Assert.Equal(70, r.NewBalance);
        var tx = await p.Db1.AllianceTransactions.SingleAsync(t => t.IdempotencyKey == "adj:2");
        Assert.Equal(-30, tx.Points);
    }

    [Fact(DisplayName = "P1.3-3: Adjust −over balance → rejected, no ledger row")]
    public async Task Adjust_Insufficient_Rejected_NoRow()
    {
        await using var p = await BuildAsync();
        var deviceId = Guid.NewGuid();
        _ = await p.Svc.AddPointsAsync(deviceId, TenantA, 50, "seed");

        var r = await p.Svc.AdjustPointsAsync(deviceId, TenantA, -80, "data-fix:x", "adj:3");

        Assert.False(r.Success);
        Assert.Equal("Insufficient balance", r.Error);
        Assert.False(await p.Db1.AllianceTransactions.AnyAsync(t => t.IdempotencyKey == "adj:3"));
        Assert.Equal(50, (await p.Svc.GetWalletByDeviceIdAsync(deviceId))!.TotalPointBalance);
    }

    [Fact(DisplayName = "P1.3-4: Adjust same IdempotencyKey twice → single ADJUST row")]
    public async Task Adjust_SameKey_Idempotent()
    {
        await using var p = await BuildAsync();
        var deviceId = Guid.NewGuid();
        _ = await p.Svc.AddPointsAsync(deviceId, TenantA, 100, "seed");

        var r1 = await p.Svc.AdjustPointsAsync(deviceId, TenantA, 25, "complaint:x", "adj:4");
        var r2 = await p.Svc.AdjustPointsAsync(deviceId, TenantA, 25, "complaint:x", "adj:4");

        Assert.True(r1.Success && r2.Success);
        Assert.Equal(125, r2.NewBalance);
        Assert.Single(await p.Db1.AllianceTransactions.Where(t => t.IdempotencyKey == "adj:4").ToListAsync());
        Assert.Equal(125, (await p.Svc.GetWalletByDeviceIdAsync(deviceId))!.TotalPointBalance);
    }

    [Fact(DisplayName = "P1.3-5: Adjust 0 points → rejected")]
    public async Task Adjust_Zero_Rejected()
    {
        await using var p = await BuildAsync();
        var r = await p.Svc.AdjustPointsAsync(Guid.NewGuid(), TenantA, 0, "noop", "adj:5");
        Assert.False(r.Success);
    }

    // ═══════════════════════════════════════════════════════════
    // P1.1 — ExitMemberSettlementAsync
    // ═══════════════════════════════════════════════════════════

    [Fact(DisplayName = "P1.1-1: Exit burns ONLY the points attributed to the exiting tenant")]
    public async Task Exit_BurnsOnlyAttributed()
    {
        await using var p = await BuildAsync();
        var deviceId = Guid.NewGuid();
        _ = await p.Svc.AddPointsAsync(deviceId, TenantA, 100, "earn A");
        _ = await p.Svc.AddPointsAsync(deviceId, TenantB, 50, "earn B");
        // Redeem 30 AT TenantB — consumes B's own pool first.
        _ = await p.Svc.DeductPointsAsync(deviceId, TenantB, 30, "redeem", "V1", "red:1");
        // Now: balance 120, netEarn {A:100, B:20}

        var result = await p.Svc.ExitMemberSettlementAsync(TenantA, "đã chuyển khoản chốt số", "sysadmin");

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.CustomersProcessed);
        Assert.Equal(100, result.TotalPointsTransferred);

        var wallet = await p.Db1.AllianceWallets.AsNoTracking()
            .SingleAsync(w => w.CustomerDeviceId == deviceId);
        Assert.Equal(20, wallet.TotalPointBalance);

        var burn = await p.Db1.AllianceTransactions.SingleAsync(t => t.IdempotencyKey == $"exit:{TenantA}:{wallet.Id}");
        Assert.Equal(AllianceTransactionType.ADJUST, burn.Type);
        Assert.Equal(-100, burn.Points);
        Assert.Equal(TenantA, burn.TransactionTenantId);
        Assert.Equal(20, burn.BalanceAfter);
    }

    [Fact(DisplayName = "P1.1-2: Exit re-run is a no-op (idempotent per wallet)")]
    public async Task Exit_Rerun_Idempotent()
    {
        await using var p = await BuildAsync();
        var deviceId = Guid.NewGuid();
        _ = await p.Svc.AddPointsAsync(deviceId, TenantA, 100, "earn A");

        var r1 = await p.Svc.ExitMemberSettlementAsync(TenantA, "settle", "admin");
        var r2 = await p.Svc.ExitMemberSettlementAsync(TenantA, "settle", "admin");

        Assert.Equal(1, r1.CustomersProcessed);
        Assert.Equal(0, r2.CustomersProcessed);
        Assert.Equal(0, r2.TotalPointsTransferred);
        Assert.Equal(0, (await p.Svc.GetWalletByDeviceIdAsync(deviceId))!.TotalPointBalance);
        Assert.Single(await p.Db1.AllianceTransactions
            .Where(t => t.IdempotencyKey != null && t.IdempotencyKey.StartsWith($"exit:{TenantA}:")).ToListAsync());
    }

    [Fact(DisplayName = "P1.1-3: Exit rejected while tenant is still an Alliance member")]
    public async Task Exit_StillMember_Rejected()
    {
        await using var p = await BuildAsync(isAllianceMember: true);
        var deviceId = Guid.NewGuid();
        _ = await p.Svc.AddPointsAsync(deviceId, TenantA, 100, "earn A");

        var result = await p.Svc.ExitMemberSettlementAsync(TenantA, "settle", "admin");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(0, result.CustomersProcessed);
        Assert.Equal(100, (await p.Svc.GetWalletByDeviceIdAsync(deviceId))!.TotalPointBalance);
        Assert.False(await p.Db1.AllianceTransactions.AnyAsync(t => t.Type == AllianceTransactionType.ADJUST));
    }

    [Fact(DisplayName = "P1.1-4: Exit with no attributed wallets → zero result, no writes")]
    public async Task Exit_NoAttribution_Noop()
    {
        await using var p = await BuildAsync();
        var result = await p.Svc.ExitMemberSettlementAsync(TenantA, "settle", "admin");

        Assert.True(result.Success);
        Assert.Equal(0, result.CustomersProcessed);
        Assert.Equal(0, result.TotalPointsTransferred);
    }

    // ═══════════════════════════════════════════════════════════
    // P1.2 — GetSettlementReportAsync
    // ═══════════════════════════════════════════════════════════

    [Fact(DisplayName = "P1.2-1: Report aggregates earn/redeem/adjust per tenant")]
    public async Task Report_AggregatesPerTenant()
    {
        await using var p = await BuildAsync();
        var deviceId = Guid.NewGuid();
        _ = await p.Svc.AddPointsAsync(deviceId, TenantA, 100, "earn A");
        _ = await p.Svc.AddPointsAsync(deviceId, TenantB, 50, "earn B");
        _ = await p.Svc.DeductPointsAsync(deviceId, TenantB, 30, "redeem", "V1", "red:1");
        _ = await p.Svc.AdjustPointsAsync(deviceId, TenantA, -10, "data-fix:x", "adj:x");

        var rows = await p.Svc.GetSettlementReportAsync(null, null);

        var rowA = rows.Single(r => r.TenantId == TenantA);
        Assert.Equal(100, rowA.EarnCredit);
        Assert.Equal(0, rowA.RedeemCost);
        Assert.Equal(-10, rowA.AdjustNet);
        Assert.Equal(90, rowA.NetSettlement);

        var rowB = rows.Single(r => r.TenantId == TenantB);
        Assert.Equal(50, rowB.EarnCredit);
        Assert.Equal(30, rowB.RedeemCost);
        Assert.Equal(0, rowB.AdjustNet);
        Assert.Equal(20, rowB.NetSettlement);
    }

    [Fact(DisplayName = "P1.2-2: Report date range filters transactions")]
    public async Task Report_DateRange_Filters()
    {
        await using var p = await BuildAsync();
        var deviceId = Guid.NewGuid();
        _ = await p.Svc.AddPointsAsync(deviceId, TenantA, 100, "earn A");

        var future = await p.Svc.GetSettlementReportAsync(DateTime.UtcNow.AddHours(1), null);
        Assert.Empty(future);

        var all = await p.Svc.GetSettlementReportAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
        Assert.Single(all);
        Assert.Equal(100, all[0].EarnCredit);
    }
}
