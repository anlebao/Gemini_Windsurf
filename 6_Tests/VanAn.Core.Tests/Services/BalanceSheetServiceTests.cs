using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// VAS Wave 4 — Tests for BalanceSheetService.
/// Verifies: non-empty with seed, W2 invariant (TotalAssets == TotalLiab+Equity), 2-column opening,
/// multi-tenant isolation, standard param behavior.
/// </summary>
public class BalanceSheetServiceTests
{
    private async Task<(VanAnDbContext db, BalanceSheetService svc)> SetupAsync()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider?.SetTenant(VasSampleDataSeeder.VasEnterpriseTenantGuid);
        VanAnDbContext db = scope.Context;
        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);
        _ = await VasSampleDataSeeder.SeedAsync(db);
        var chartSvc = new AccountChartService(db, NullLogger<AccountChartService>.Instance);
        var svc = new BalanceSheetService(db, chartSvc, NullLogger<BalanceSheetService>.Instance);
        return (db, svc);
    }

    // W4-BS1: Non-empty result with seed data (period 2026-06 — has opening + movement).
    [Fact]
    public async Task W4_BS1_GenerateAsync_Period2026_06_ReturnsNonEmptyResult()
    {
        var (_, svc) = await SetupAsync();
        BalanceSheet bs = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.NotEmpty(bs.Assets);
        Assert.NotEmpty(bs.Liabilities);
        Assert.NotEmpty(bs.Equity);
    }

    // W4-BS2: W2 invariant — TotalAssets == TotalLiabilitiesAndEquity (no IsBalanced flag, throws if violated).
    [Fact]
    public async Task W4_BS2_GenerateAsync_TotalAssetsEqualsTotalLiabilitiesAndEquity()
    {
        var (_, svc) = await SetupAsync();
        BalanceSheet bs = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.Equal(bs.TotalAssetsEnding, bs.TotalLiabilitiesAndEquityEnding, precision: 2);
        Assert.Equal(bs.TotalAssetsOpening, bs.TotalLiabilitiesAndEquityOpening, precision: 2);
    }

    // W4-BS3: 2-column — Opening (2026-05 cumulative) > 0, Ending > Opening (June has activity).
    [Fact]
    public async Task W4_BS3_GenerateAsync_TwoColumn_OpeningAndEndingBothPositive()
    {
        var (_, svc) = await SetupAsync();
        BalanceSheet bs = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.True(bs.TotalAssetsOpening > 0, $"Opening should be > 0 (May opening balances seeded), got {bs.TotalAssetsOpening}");
        Assert.True(bs.TotalAssetsEnding > 0, $"Ending should be > 0, got {bs.TotalAssetsEnding}");
    }

    // W4-BS4: First period (2026-05) — Opening = 0 (no entries before 2026-05-01, R2 satisfied).
    [Fact]
    public async Task W4_BS4_GenerateAsync_FirstPeriod2026_05_OpeningIsZero()
    {
        var (_, svc) = await SetupAsync();
        BalanceSheet bs = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT133_2016);

        // Opening for May = entries before 2026-05-01. Seeder's opening entry is dated 2026-05-01 → in movement, not opening.
        // So opening = 0 (R2: start with 0 for first period).
        Assert.Equal(0m, bs.TotalAssetsOpening);
        Assert.Equal(0m, bs.TotalLiabilitiesAndEquityOpening);
        // Ending includes the 2026-05-01 opening balance entry + May activity → > 0.
        Assert.True(bs.TotalAssetsEnding > 0);
    }

    // W4-BS5: Multi-tenant isolation — different tenant returns empty result.
    [Fact]
    public async Task W4_BS5_GenerateAsync_DifferentTenant_ReturnsEmptyResult()
    {
        var (_, svc) = await SetupAsync();
        var otherTenant = new TenantId(Guid.NewGuid());

        BalanceSheet bs = await svc.GenerateAsync(otherTenant, new AccountingPeriod(2026, 6), AccountingStandard.TT133_2016);

        Assert.Empty(bs.Assets);
        Assert.Empty(bs.Liabilities);
        Assert.Empty(bs.Equity);
        Assert.Equal(0m, bs.TotalAssetsEnding);
        Assert.Equal(0m, bs.TotalLiabilitiesAndEquityEnding);
    }

    // W4-BS6: Standard param — TT 99 returns result (different account set, but seed accounts overlap).
    [Fact]
    public async Task W4_BS6_GenerateAsync_TT99Standard_ReturnsNonEmptyResult()
    {
        var (_, svc) = await SetupAsync();
        BalanceSheet bs = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT99_2025);

        // Seed uses TT 133 account codes (111, 112, 156, 211, 411, 331, 3331) — all also exist in TT 99.
        Assert.NotEmpty(bs.Assets);
        Assert.NotEmpty(bs.Equity);
    }
}
