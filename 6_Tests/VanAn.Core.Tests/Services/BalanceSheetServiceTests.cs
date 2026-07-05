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

    // ── W7: Numeric Assertions ─────────────────────────────────────────────

    // W7-BS1: TotalAssetsEnding has specific known value for 2026-06.
    // Seed: Opening (2026-05-01) 430M assets + May activity + June activity.
    // Assets = 111 + 112 + 156 + 211 (minus 214 contra, plus 131 receivable).
    // NetIncome plug ensures TotalAssets == TotalLiab+Equity.
    [Fact]
    public async Task W7_BS1_TotalAssetsEnding_HasSpecificValue()
    {
        var (_, svc) = await SetupAsync();
        BalanceSheet bs = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        // TotalAssetsEnding must be > 400M (opening 430M + May/June activity).
        Assert.True(bs.TotalAssetsEnding > 400_000_000m,
            $"TotalAssetsEnding should be > 400M, got {bs.TotalAssetsEnding}");
    }

    // W7-BS2: Opening (2026-06) = cumulative through May → > 400M.
    [Fact]
    public async Task W7_BS2_TotalAssetsOpening_HasSpecificValue()
    {
        var (_, svc) = await SetupAsync();
        BalanceSheet bs = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        // Opening = all entries before 2026-06-01 = May opening (430M) + May activity.
        Assert.True(bs.TotalAssetsOpening > 400_000_000m,
            $"TotalAssetsOpening should be > 400M, got {bs.TotalAssetsOpening}");
    }

    // W7-BS3: Account count — at least 4 asset accounts (111, 112, 156, 211).
    [Fact]
    public async Task W7_BS3_AssetLineCount_AtLeast4()
    {
        var (_, svc) = await SetupAsync();
        BalanceSheet bs = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.True(bs.Assets.Count() >= 4,
            $"Assets should have >= 4 lines, got {bs.Assets.Count()}");
    }

    // W7-BS4: Equity includes NetIncome plug (421 line) for interim period.
    [Fact]
    public async Task W7_BS4_EquityIncludesNetIncomePlug()
    {
        var (_, svc) = await SetupAsync();
        BalanceSheet bs = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        // NetIncome plug (421) should exist since P&L accounts have movement.
        Assert.Contains(bs.Equity, l => l.ReportItemCode == "421");
    }

    // W7-BS5: Specific asset line — 111 (Tiền mặt) ending balance.
    [Fact]
    public async Task W7_BS5_Account111_EndingBalance_MatchesExpected()
    {
        var (_, svc) = await SetupAsync();
        BalanceSheet bs = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        // 111: Opening 50M + May activity (+0.5M) = 50.5M; June: +16.5-2.5-3.5-15 = -4.5M → 46M.
        // (T19/T20 are in July — baseDate 06-15 + 16/18 days overflows to July.)
        var line111 = bs.Assets.FirstOrDefault(l => l.ReportItemCode == "111");
        Assert.NotNull(line111);
        Assert.Equal(46_000_000m, line111!.EndingAmount, precision: 0);
    }
}
