using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// VAS Wave 4 — Tests for TrialBalanceService.
/// Verifies: non-empty with seed, TotalDebit == TotalCredit (IsBalanced), opening balance per account,
/// multi-tenant isolation, account names populated via AccountChartService.
/// </summary>
public class TrialBalanceServiceTests
{
    private async Task<(VanAnDbContext db, TrialBalanceService svc)> SetupAsync()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider?.SetTenant(VasSampleDataSeeder.VasEnterpriseTenantGuid);
        VanAnDbContext db = scope.Context;
        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);
        _ = await VasSampleDataSeeder.SeedAsync(db);
        var chartSvc = new AccountChartService(db, NullLogger<AccountChartService>.Instance);
        var svc = new TrialBalanceService(db, chartSvc, NullLogger<TrialBalanceService>.Instance);
        return (db, svc);
    }

    // W4-TB1: Non-empty result with seed data.
    [Fact]
    public async Task W4_TB1_GenerateAsync_Period2026_05_ReturnsNonEmptyAccounts()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT133_2016);

        Assert.NotEmpty(tb.Accounts);
        // Seed uses accounts 111, 112, 156, 211, 411, 331, 3331, 511, 632, 6421, 6422, 214, 521, 5113.
        Assert.Contains(tb.Accounts, a => a.AccountNumber == "111");
        Assert.Contains(tb.Accounts, a => a.AccountNumber == "511");
    }

    // W4-TB2: TotalDebit == TotalCredit (double-entry invariant).
    [Fact]
    public async Task W4_TB2_GenerateAsync_TotalDebitEqualsTotalCredit_IsBalancedTrue()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT133_2016);

        Assert.Equal(tb.TotalDebit, tb.TotalCredit, precision: 2);
        Assert.True(tb.IsBalanced, "Trial balance should be balanced (seed data is double-entry)");
    }

    // W4-TB3: Account names populated via AccountChartService (not fallback "Tài khoản {code}").
    [Fact]
    public async Task W4_TB3_GenerateAsync_AccountNamesPopulatedFromChart()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT133_2016);

        TrialBalanceAccount? account111 = tb.Accounts.FirstOrDefault(a => a.AccountNumber == "111");
        Assert.NotNull(account111);
        Assert.Equal("Tiền mặt", account111!.AccountName);
    }

    // W4-TB4: Opening balance per account — June period has opening (May cumulative).
    [Fact]
    public async Task W4_TB4_GenerateAsync_Period2026_06_BalanceIncludesOpening()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        // 111 opening = 50M (from 2026-05-01 opening entry) + May cash sales - May cash expenses.
        TrialBalanceAccount? account111 = tb.Accounts.FirstOrDefault(a => a.AccountNumber == "111");
        Assert.NotNull(account111);
        // Balance = opening + movement. For 111, opening (May cumulative) should make balance > 0.
        Assert.True(account111!.Balance > 0, $"111 balance should be > 0 (opening + June activity), got {account111.Balance}");
    }

    // W4-TB5: First period (2026-05) — Balance = movement only (no opening before 2026-05-01).
    [Fact]
    public async Task W4_TB5_GenerateAsync_FirstPeriod2026_05_BalanceIsMovementOnly()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT133_2016);

        // 111 movement in May: opening entry (50M debit) + sales (11M debit) - expenses (2M+1M credit).
        TrialBalanceAccount? account111 = tb.Accounts.FirstOrDefault(a => a.AccountNumber == "111");
        Assert.NotNull(account111);
        Assert.True(account111!.DebitTotal > 0, "111 should have debit movement in May");
    }

    // W4-TB6: Multi-tenant isolation.
    [Fact]
    public async Task W4_TB6_GenerateAsync_DifferentTenant_ReturnsEmptyResult()
    {
        var (_, svc) = await SetupAsync();
        var otherTenant = new TenantId(Guid.NewGuid());

        TrialBalance tb = await svc.GenerateAsync(otherTenant, new AccountingPeriod(2026, 5), AccountingStandard.TT133_2016);

        Assert.Empty(tb.Accounts);
        Assert.Equal(0m, tb.TotalDebit);
        Assert.Equal(0m, tb.TotalCredit);
        Assert.True(tb.IsBalanced); // 0 == 0.
    }

    // W4-TB7: Standard param — TT 99 returns result with account names from TT 99 chart.
    [Fact]
    public async Task W4_TB7_GenerateAsync_TT99Standard_ReturnsNonEmptyAccountsWithNames()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT99_2025);

        Assert.NotEmpty(tb.Accounts);
        TrialBalanceAccount? account111 = tb.Accounts.FirstOrDefault(a => a.AccountNumber == "111");
        Assert.NotNull(account111);
        Assert.Equal("Tiền mặt", account111!.AccountName); // 111 = "Tiền mặt" in both TT 133 and TT 99.
    }

    // ── W7: Numeric Assertions ─────────────────────────────────────────────

    // W7-TB1: TotalDebit == TotalCredit == 124M for 2026-06 movement.
    // June movement: 8 journal entries (T11-T18; T19/T20 overflow to July).
    [Fact]
    public async Task W7_TB1_TotalDebitAndCredit_Equals124M()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.Equal(124_000_000m, tb.TotalDebit, precision: 0);
        Assert.Equal(124_000_000m, tb.TotalCredit, precision: 0);
    }

    // W7-TB2: IsBalanced == true for 2026-06.
    [Fact]
    public async Task W7_TB2_IsBalanced_True()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.True(tb.IsBalanced, "Trial Balance should be balanced for 2026-06");
    }

    // W7-TB3: Account count >= 10 for 2026-06 (111, 112, 131, 156, 211, 214, 331, 334, 3331, 511, 632, 6421, 6422).
    [Fact]
    public async Task W7_TB3_AccountCount_AtLeast10()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.True(tb.Accounts.Count() >= 10,
            $"Expected >= 10 accounts, got {tb.Accounts.Count()}");
    }

    // W7-TB4: Account 511 movement credit = 45M (15M + 30M sales in June; T19's 5M is in July).
    [Fact]
    public async Task W7_TB4_Account511_CreditTotal_Equals45M()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        TrialBalanceAccount? acc511 = tb.Accounts.FirstOrDefault(a => a.AccountNumber == "511");
        Assert.NotNull(acc511);
        Assert.Equal(45_000_000m, acc511!.CreditTotal, precision: 0);
    }

    // W7-TB5: Account 632 movement debit = 31.5M (10.5M + 21M COGS in June; T19's 3.5M is in July).
    [Fact]
    public async Task W7_TB5_Account632_DebitTotal_Equals31_5M()
    {
        var (_, svc) = await SetupAsync();
        TrialBalance tb = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        TrialBalanceAccount? acc632 = tb.Accounts.FirstOrDefault(a => a.AccountNumber == "632");
        Assert.NotNull(acc632);
        Assert.Equal(31_500_000m, acc632!.DebitTotal, precision: 0);
    }
}
