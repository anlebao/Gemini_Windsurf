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

        // May: revenue 29.5M (511 net 30M + 5113 0.5M − chiết khấu 1M) − COGS 21M − OpEx 18M
        // (6421: 2+8, 6422: 3+5) = −9.5M. Trước fix BUG-1a: 6421/6422 bị drop → lợi nhuận ảo dương.
        Assert.Equal(-9_500_000m, is_.NetProfitEnding, precision: 0);
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

        // TT133 template path (P1): trả về đủ dòng mẫu B02-DNN với giá trị 0 (không rỗng).
        Assert.All(is_.Lines, l => Assert.Equal(0m, l.EndingAmount));
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

    // W7-IS3: NetProfitEnding = −6.5M for 2026-06 (Revenue 45M − COGS 31.5M − OpEx 20M = −6.5M).
    // RV fix 2026-11-03 (BUG-1a): 6421/6422 giờ map vào mã 24 (đã seed vào TT133 chart) — trước
    // bị drop âm thầm → OpEx = 0, lợi nhuận phóng đại (cùng pattern BUG-2 của 611).
    // OpEx T6 = 2.5+3.5+5+9 = 20M (T20 1.5M rơi sang 2026-07 — seed baseDate 2026-06-15 +18d).
    [Fact]
    public async Task W7_IS3_NetProfitEnding_EqualsNegative8M()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        Assert.Equal(-6_500_000m, is_.NetProfitEnding, precision: 0);
    }

    // W7-IS4: NetProfit formula — NetProfit == Revenue − COGS − OpEx (mã 11 + mã 24).
    [Fact]
    public async Task W7_IS4_NetProfitMatchesFormula_RevenueMinusCOGSMinusOpEx()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        // B02-DNN (P1): giá vốn 632+611 gộp vào Mã 11 · CP QLKD 642x gộp vào Mã 24 (dương — đã negate).
        decimal cogs = is_.Lines.First(l => l.ReportItemCode == "11").EndingAmount;
        decimal opex = is_.Lines.First(l => l.ReportItemCode == "24").EndingAmount;
        decimal expectedNetProfit = is_.TotalRevenueEnding - cogs - opex;

        Assert.Equal(20_000_000m, opex, precision: 0); // 6421/6422 June: 2.5+3.5+5+9 = 20M (T20 1.5M sang T7)
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

        // B02-DNN (P1): chi phí nằm ở Mã 11 (giá vốn) / Mã 24 (CP QLKD) — không còn mã TK 632/642.
        int expenseCount = is_.Lines.Count(l => l.ReportItemCode is "11" or "24");
        Assert.True(expenseCount >= 1, $"Expected >= 1 expense line, got {expenseCount}");
    }

    // ── RV findings fix (2026-11-03): BUG-2 + BUG-1a ─────────────────────

    // BUG-2: phát sinh TK 611 "Mua hàng" phải map vào mã 11 (giá vốn) — trước không có
    // trong AccountCodes của line nào → rơi khỏi B02, lợi nhuận phóng đại 7,5tr (Cafe Tân Quy).
    [Fact]
    public async Task RV_IS_Account611_MapsIntoCogsLine11()
    {
        var (db, svc) = await SetupAsync();
        var je = new JournalEntry(VasSampleDataSeeder.VasEnterpriseTenantId,
            new DateTime(2026, 6, 20), "Mua hàng KKĐK", "Purchase", null);
        je.AddLine("611", 7_501_000m, 0, "Mua hàng");
        je.AddLine("156", 0, 7_501_000m, "Nhập kho");
        db.JournalEntries.Add(je);
        await db.SaveChangesAsync();

        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        decimal m11 = is_.Lines.First(l => l.ReportItemCode == "11").EndingAmount;
        Assert.Equal(31_500_000m + 7_501_000m, m11, precision: 0); // 632 31.5M + 611 7.501M
    }

    // BUG-1a (income): TK chi tiết không có trong chart cũ (6421/6422 giờ đã seed —
    // regression guard: chúng phải được cộng vào mã 24, không drop).
    [Fact]
    public async Task RV_IS_SubAccounts6421_6422_IncludedInOpexLine24()
    {
        var (_, svc) = await SetupAsync();
        IncomeStatement is_ = await svc.GenerateAsync(
            VasSampleDataSeeder.VasEnterpriseTenantId,
            new AccountingPeriod(2026, 6),
            AccountingStandard.TT133_2016);

        decimal m24 = is_.Lines.First(l => l.ReportItemCode == "24").EndingAmount;
        Assert.Equal(20_000_000m, m24, precision: 0); // 2.5+3.5+5+9 = 20M (T20 1.5M sang T7)
    }

    // ── TT 71 Phase 3 (T3): B02-HTX ──────────────────────────────────────

    private async Task<(VanAnDbContext db, IncomeStatementService svc)> SetupTt71Async()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider?.SetTenant(Tt71SampleDataSeeder.Tt71TenantGuid);
        VanAnDbContext db = scope.Context;
        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);
        await Tt71SampleDataSeeder.SeedAsync(db);
        var chartSvc = new AccountChartService(db, NullLogger<AccountChartService>.Instance);
        var svc = new IncomeStatementService(db, chartSvc, NullLogger<IncomeStatementService>.Instance);
        return (db, svc);
    }

    // T3: Generate với standard TT71 → cấu trúc B02-HTX + số liệu giả đúng (spec PL IV).
    [Fact]
    public async Task T3_TT71_GenerateAsync_B02Htx_StructureAndNumbers()
    {
        var (_, svc) = await SetupTt71Async();
        IncomeStatement is_ = await svc.GenerateAsync(
            Tt71SampleDataSeeder.Tt71TenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT71_2024);

        Assert.Equal(24, is_.Lines.Count());

        decimal Amount(string code) => is_.Lines.First(l => l.ReportItemCode == code).EndingAmount;

        // Tách nội bộ/ngoài (511 vs 512)
        Assert.Equal(10_000_000m, Amount("01a"));
        Assert.Equal(5_000_000m, Amount("01b"));
        Assert.Equal(15_000_000m, Amount("01"));

        // 521 (giảm trừ) trình bày DƯƠNG theo bên bình thường → 10 = 01 - 02 đúng
        Assert.Equal(1_000_000m, Amount("02a"));
        Assert.Equal(0m, Amount("02b"));
        Assert.Equal(1_000_000m, Amount("02"));

        Assert.Equal(9_000_000m, Amount("10a"));
        Assert.Equal(5_000_000m, Amount("10b"));
        Assert.Equal(14_000_000m, Amount("10"));

        Assert.Equal(6_000_000m, Amount("11a"));
        Assert.Equal(3_000_000m, Amount("11b"));
        Assert.Equal(9_000_000m, Amount("11"));

        Assert.Equal(4_000_000m, Amount("12"));
        Assert.Equal(0m, Amount("12a"));
        Assert.Equal(0m, Amount("12b"));

        // 20a/20b không gồm 642 (không có dữ liệu phân bổ) → 20a + 20b ≠ 20 khi 12 > 0 (ghi chú tài liệu)
        Assert.Equal(3_000_000m, Amount("20a"));
        Assert.Equal(2_000_000m, Amount("20b"));
        Assert.Equal(1_000_000m, Amount("20"));

        Assert.Equal(2_000_000m, Amount("31"));
        Assert.Equal(1_000_000m, Amount("32"));
        Assert.Equal(1_000_000m, Amount("40"));
        Assert.Equal(2_000_000m, Amount("50"));
        Assert.Equal(500_000m, Amount("51"));
        Assert.Equal(1_500_000m, Amount("60"));

        // Tổng hợp
        Assert.Equal(15_000_000m, is_.TotalRevenueEnding);
        Assert.Equal(1_500_000m, is_.NetProfitEnding);
        Assert.Equal(9_000_000m, is_.TotalCogsEnding);
        Assert.Equal(4_000_000m, is_.TotalOpExEnding);
        Assert.Equal(0m, is_.TotalRevenueOpening); // prior year 2025-05 không có dữ liệu
    }
}
