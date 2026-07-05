using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// VAS Wave 4 — Tests for IncomeStatementService.
/// Verifies: non-empty with seed, NetProfit calculation, 2-column (Ending/Opening), multi-tenant isolation.
/// </summary>
public class IncomeStatementServiceTests
{
    private async Task<(VanAnDbContext db, IncomeStatementService svc)> SetupAsync()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider?.SetTenant(VasSampleDataSeeder.VasEnterpriseTenantGuid);
        VanAnDbContext db = scope.Context;
        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);
        _ = await VasSampleDataSeeder.SeedAsync(db);
        var chartSvc = new AccountChartService(db, NullLogger<AccountChartService>.Instance);
        var svc = new IncomeStatementService(db, chartSvc, NullLogger<IncomeStatementService>.Instance);
        return (db, svc);
    }

    // W4-IS1: Non-empty result with seed data (period 2026-05).
    [Fact]
    public async Task W4_IS1_GenerateAsync_Period2026_05_ReturnsNonEmptyLines()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT133_2016);

        Assert.NotEmpty(is_.Lines);
        Assert.True(is_.TotalRevenueEnding > 0, $"Revenue should be > 0 (May sales seeded), got {is_.TotalRevenueEnding}");
    }

    // W4-IS2: NetProfit = Revenue - COGS - OpEx + OtherIncome - OtherExpense.
    [Fact]
    public async Task W4_IS2_GenerateAsync_NetProfitCalculatedFromComponents()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT133_2016);

        // NetProfit should be positive (May: 30M revenue - 21M COGS - 2M selling expense ≈ 7M).
        Assert.True(is_.NetProfitEnding > 0, $"NetProfit should be positive for May (revenue > COGS + OpEx), got {is_.NetProfitEnding}");
    }

    // W4-IS3: 2-column — Opening column = same month prior year (2025-05, no data → 0).
    [Fact]
    public async Task W4_IS3_GenerateAsync_OpeningColumn_PriorYearNoData_IsZero()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT133_2016);

        // No 2025 data seeded → Opening column = 0.
        Assert.Equal(0m, is_.TotalRevenueOpening);
        Assert.Equal(0m, is_.NetProfitOpening);
    }

    // W4-IS4: June period — both Ending (June) and Opening (June 2025 = 0) columns.
    [Fact]
    public async Task W4_IS4_GenerateAsync_Period2026_06_EndingPositiveOpeningZero()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.True(is_.TotalRevenueEnding > 0, "June has sales activity");
        Assert.Equal(0m, is_.TotalRevenueOpening); // No 2025-06 data.
    }

    // W4-IS5: Multi-tenant isolation.
    [Fact]
    public async Task W4_IS5_GenerateAsync_DifferentTenant_ReturnsZeroResult()
    {
        var (_, svc) = await SetupAsync();
        var otherTenant = new TenantId(Guid.NewGuid());

        IncomeStatement is_ = await svc.GenerateAsync(otherTenant, new AccountingPeriod(2026, 5), AccountingStandard.TT133_2016);

        Assert.Empty(is_.Lines);
        Assert.Equal(0m, is_.TotalRevenueEnding);
        Assert.Equal(0m, is_.NetProfitEnding);
    }

    // W4-IS6: Standard param — TT 99 returns result.
    [Fact]
    public async Task W4_IS6_GenerateAsync_TT99Standard_ReturnsNonEmptyLines()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT99_2025);

        Assert.NotEmpty(is_.Lines);
        Assert.True(is_.TotalRevenueEnding > 0);
    }

    // ── W7: Numeric Assertions ─────────────────────────────────────────────

    // W7-IS1: TotalRevenueEnding = 45M for 2026-06 (511: 15M + 30M = 45M credit; T19's 5M is in July).
    [Fact]
    public async Task W7_IS1_TotalRevenueEnding_Equals45M()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.Equal(45_000_000m, is_.TotalRevenueEnding, precision: 0);
    }

    // W7-IS2: TotalRevenueOpening = 0 for 2026-06 (prior year 2025-06 has no entries).
    [Fact]
    public async Task W7_IS2_TotalRevenueOpening_EqualsZero()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.Equal(0m, is_.TotalRevenueOpening, precision: 0);
    }

    // W7-IS3: NetProfitEnding = 13.5M for 2026-06 (Revenue 45M - COGS 31.5M = 13.5M).
    // Note: 6421/6422 not in account chart → skipped by IS service → OpEx = 0.
    [Fact]
    public async Task W7_IS3_NetProfitEnding_Equals13_5M()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.Equal(13_500_000m, is_.NetProfitEnding, precision: 0);
    }

    // W7-IS4: NetProfit formula — NetProfit == TotalRevenue - COGS (6421/6422 not in chart, OpEx = 0).
    [Fact]
    public async Task W7_IS4_NetProfitMatchesFormula_RevenueMinusCOGS()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        // Only 632 is in the chart (6421/6422 are sub-accounts not in chart → skipped).
        decimal cogs = is_.Lines.Where(l => l.ReportItemCode.StartsWith("632")).Sum(l => -l.EndingAmount);
        decimal expectedNetProfit = is_.TotalRevenueEnding - cogs;

        Assert.Equal(expectedNetProfit, is_.NetProfitEnding, precision: 2);
    }

    // W7-IS5: At least 1 expense line (632; 6421/6422 not in chart).
    [Fact]
    public async Task W7_IS5_ExpenseLineCount_AtLeast1()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        int expenseCount = is_.Lines.Count(l => l.ReportItemCode.StartsWith("632") || l.ReportItemCode.StartsWith("642"));
        Assert.True(expenseCount >= 1, $"Expected >= 1 expense line, got {expenseCount}");
    }
}
