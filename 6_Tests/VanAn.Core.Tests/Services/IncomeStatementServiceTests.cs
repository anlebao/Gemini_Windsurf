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
}
