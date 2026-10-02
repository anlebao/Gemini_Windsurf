using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Seed;

/// <summary>
/// W3 FIX-7: Tests for AccountChartSeeder.
/// Verifies: account counts per standard, cleanup+reseed idempotency, TT 133 priority, no duplicates.
/// </summary>
public class AccountChartSeederTests
{
    // W3-SE1: Seed creates expected counts
    // Counts verified against TT 133/2016 Phụ lục II (baocaotaichinh.vn):
    //   TT 133 level-1 = 49 (23 Asset + 10 Liability + 5 Equity + 5 Doanh thu+Thu nhập khác + 7 Chi phí + 1 XĐKQ)
    //   + 2 level-2 (3331, 1331) = 51 total
    //   TT 99 level-1 = 71 + 2 level-2 (3331, 1331) + 2 BĐSĐT sub-accounts (5117, 6327) = 75 total
    //   TT 58 = 0 (no chart of accounts — FIX-5)
    //   TT 71/2024 (HTX) = 43 level-1 + 46 level-2/3 = 89 (off-balance 001-008 defer — như TT 133/99)
    //   Grand total = 215
    [Fact]
    public async Task W3_SE1_SeedAsync_CreatesExpectedAccountCounts()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;

        int total = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);

        int tt133 = await db.AccountCharts.CountAsync(e => e.Standard == AccountingStandard.TT133_2016);
        int tt99 = await db.AccountCharts.CountAsync(e => e.Standard == AccountingStandard.TT99_2025);
        int tt58 = await db.AccountCharts.CountAsync(e => e.Standard == AccountingStandard.TT58_2026);
        int tt71 = await db.AccountCharts.CountAsync(e => e.Standard == AccountingStandard.TT71_2024);

        Assert.Equal(51, tt133); // 49 level-1 + 2 level-2 (3331, 1331)
        Assert.Equal(75, tt99);  // 71 level-1 + 2 level-2 (3331, 1331) + 2 BĐSĐT (5117, 6327)
        Assert.Equal(0, tt58);   // FIX-5: TT 58 has no chart of accounts
        Assert.Equal(89, tt71);  // TT 71/2024 HTX: 43 level-1 + 46 level-2/3
        Assert.Equal(215, total);
    }

    // TT71-SE5 (2026-10-01): TT 71 chart — type mapping + đặc thù HTX
    [Fact]
    public async Task Tt71_SE5_Chart_HtxSpecificAccountsAndTypes()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;

        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);

        async Task<AccountChartEntity> Acct(string code)
            => await db.AccountCharts.SingleAsync(e => e.Standard == AccountingStandard.TT71_2024 && e.AccountCode == code);

        // Doanh thu nội bộ/ngoài + Thu nhập khác 558 (KHÔNG dùng 711)
        Assert.Equal("Doanh thu từ giao dịch bên ngoài", (await Acct("511")).AccountName);
        Assert.Equal(AccountType.Revenue, (await Acct("511")).Type);
        Assert.Equal("Doanh thu từ giao dịch nội bộ", (await Acct("512")).AccountName);
        Assert.Equal("Thu nhập khác", (await Acct("558")).AccountName);
        Assert.Equal("Chi phí khác", (await Acct("658")).AccountName);
        Assert.Equal("Chi phí thuế thu nhập doanh nghiệp", (await Acct("659")).AccountName);
        Assert.Equal(AccountType.Expense, (await Acct("658")).Type);

        // 521 giảm trừ doanh thu — normal debit (contra-revenue)
        Assert.False((await Acct("521")).IsNormalCredit);

        // 214/229 contra-asset — normal credit
        Assert.True((await Acct("214")).IsNormalCredit);
        Assert.True((await Acct("229")).IsNormalCredit);
        Assert.Equal(AccountType.Asset, (await Acct("212")).Type); // Tài sản chung không chia (trùng mã TT 99 — key riêng)

        // Đặc thù HTX: 442 Quỹ chung không chia + 136/336 nội bộ HTX + 132/332 tín dụng nội bộ
        Assert.Equal("Quỹ chung không chia của HTX", (await Acct("442")).AccountName);
        Assert.Equal(AccountType.Equity, (await Acct("442")).Type);
        Assert.Equal("Phải thu giữa các đơn vị nội bộ trong HTX", (await Acct("136")).AccountName);
        Assert.Equal("Phải trả của hoạt động tín dụng nội bộ", (await Acct("332")).AccountName);

        // KHÔNG có TK DN không tồn tại trong TT 71
        foreach (var forbidden in new[] { "515", "711", "621", "622", "627", "641", "632", "811", "821" })
        {
            Assert.False(await db.AccountCharts.AnyAsync(e => e.Standard == AccountingStandard.TT71_2024 && e.AccountCode == forbidden),
                $"TT 71 KHÔNG được có TK {forbidden}");
        }

        // TT 133/99 không bị đụng (không regress)
        Assert.Equal(51, await db.AccountCharts.CountAsync(e => e.Standard == AccountingStandard.TT133_2016));
        Assert.Equal(75, await db.AccountCharts.CountAsync(e => e.Standard == AccountingStandard.TT99_2025));
    }

    // W3-SE2: Cleanup + Reseed is idempotent (clear+reseed produces same count)
    [Fact]
    public async Task W3_SE2_CleanupAndReseed_IsIdempotent()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;

        int first = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);
        await AccountChartSeeder.CleanupAsync(db);
        int second = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);

        Assert.Equal(first, second);
        Assert.Equal(215, second);
    }

    // W3-SE3: TT 133 seeded first (R3 priority — verified by checking first inserted row)
    [Fact]
    public async Task W3_SE3_TT133SeededFirst_R3Priority()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;

        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);

        // First row by CreatedAt should be TT 133 (seeded before TT 99)
        var firstRow = await db.AccountCharts
            .OrderBy(e => e.CreatedAt)
            .FirstAsync();
        Assert.Equal(AccountingStandard.TT133_2016, firstRow.Standard);
    }

    // W3-SE4: No duplicate AccountCode per Standard (client-side eval — SQLite can't translate GroupBy+Where)
    [Fact]
    public async Task W3_SE4_NoDuplicateAccountCodePerStandard()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;

        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);

        // Materialize then group client-side (SQLite LINQ translation limitation)
        var all = await db.AccountCharts.ToListAsync();
        var duplicates = all
            .GroupBy(e => new { e.Standard, e.AccountCode })
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);
    }
}
