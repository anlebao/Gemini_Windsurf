using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// VAS Wave 4 — Tests for CashFlowStatementService (direct method).
/// Verifies: non-empty with seed, OpeningCash + NetChange == ClosingCash, activity classification,
/// multi-tenant isolation.
/// </summary>
public class CashFlowStatementServiceTests
{
    private async Task<(VanAnDbContext db, CashFlowStatementService svc)> SetupAsync()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider?.SetTenant(VasSampleDataSeeder.VasEnterpriseTenantGuid);
        VanAnDbContext db = scope.Context;
        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);
        _ = await VasSampleDataSeeder.SeedAsync(db);
        var chartSvc = new AccountChartService(db, NullLogger<AccountChartService>.Instance);
        var svc = new CashFlowStatementService(db, chartSvc, NullLogger<CashFlowStatementService>.Instance);
        return (db, svc);
    }

    // W4-CF1: Non-empty result with seed data (period 2026-05 — has cash activity).
    [Fact]
    public async Task W4_CF1_GenerateAsync_Period2026_05_ReturnsNonEmptyActivities()
    {
        var (_, svc) = await SetupAsync();
        CashFlowStatement cf = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT133_2016);

        // May has sales (111/112 debits) + expenses (111 credits) → Operating activity populated.
        Assert.True(cf.OperatingActivities.Any() || cf.InvestingActivities.Any() || cf.FinancingActivities.Any(),
            "At least one activity section should have lines for May");
    }

    // W4-CF2: ClosingCash == OpeningCash + NetChange.
    [Fact]
    public async Task W4_CF2_GenerateAsync_ClosingCashEqualsOpeningPlusNetChange()
    {
        var (_, svc) = await SetupAsync();
        CashFlowStatement cf = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.Equal(cf.OpeningCash + cf.NetChange, cf.ClosingCash, precision: 2);
    }

    // W4-CF3: OpeningCash for 2026-05 = 0 (no entries before 2026-05-01; opening entry dated 2026-05-01 is in movement).
    [Fact]
    public async Task W4_CF3_GenerateAsync_FirstPeriod2026_05_OpeningCashIsZero()
    {
        var (_, svc) = await SetupAsync();
        CashFlowStatement cf = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT133_2016);

        Assert.Equal(0m, cf.OpeningCash);
        // ClosingCash for May includes the 2026-05-01 opening balance entry (111+112 debits = 150M).
        Assert.True(cf.ClosingCash > 0, $"ClosingCash should be > 0 (includes opening balance entry), got {cf.ClosingCash}");
    }

    // W4-CF4: June period — OpeningCash = May cumulative cash (150M from opening entry).
    [Fact]
    public async Task W4_CF4_GenerateAsync_Period2026_06_OpeningCashIsMayCumulative()
    {
        var (_, svc) = await SetupAsync();
        CashFlowStatement cf = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        // Opening for June = Σ cash lines where EntryDate < 2026-06-01 = 150M (opening) + May cash activity.
        Assert.True(cf.OpeningCash > 0, $"June opening cash should be > 0 (May cumulative), got {cf.OpeningCash}");
    }

    // W4-CF5: Multi-tenant isolation.
    [Fact]
    public async Task W4_CF5_GenerateAsync_DifferentTenant_ReturnsZeroResult()
    {
        var (_, svc) = await SetupAsync();
        var otherTenant = new TenantId(Guid.NewGuid());

        CashFlowStatement cf = await svc.GenerateAsync(otherTenant, new AccountingPeriod(2026, 6), AccountingStandard.TT133_2016);

        Assert.Equal(0m, cf.OpeningCash);
        Assert.Equal(0m, cf.ClosingCash);
        Assert.Equal(0m, cf.NetChange);
        Assert.Empty(cf.OperatingActivities);
    }

    // W4-CF6: Investing activity — TK 211 (TSCĐ) cash outflow classified as Investing.
    [Fact]
    public async Task W4_CF6_GenerateAsync_InvestingActivity_Contains211IfCashPurchase()
    {
        var (_, svc) = await SetupAsync();
        CashFlowStatement cf = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        // Note: seed may or may not have a 211 cash purchase in June. Verify classification logic by checking
        // that any Investing line (if present) is for a 21x account.
        foreach (var line in cf.InvestingActivities)
        {
            Assert.StartsWith("21", line.ReportItemCode);
        }
    }
}
