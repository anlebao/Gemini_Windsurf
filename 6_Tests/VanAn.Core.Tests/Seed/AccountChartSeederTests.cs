using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
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
    //   TT 99 level-1 = 71 + 2 level-2 (3331, 1331) = 73 total
    //   TT 58 = 0 (no chart of accounts — FIX-5)
    //   Grand total = 124
    [Fact]
    public async Task W3_SE1_SeedAsync_CreatesExpectedAccountCounts()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;

        int total = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);

        int tt133 = await db.AccountCharts.CountAsync(e => e.Standard == AccountingStandard.TT133_2016);
        int tt99 = await db.AccountCharts.CountAsync(e => e.Standard == AccountingStandard.TT99_2025);
        int tt58 = await db.AccountCharts.CountAsync(e => e.Standard == AccountingStandard.TT58_2026);

        Assert.Equal(51, tt133); // 49 level-1 + 2 level-2 (3331, 1331)
        Assert.Equal(73, tt99);  // 71 level-1 + 2 level-2 (3331, 1331)
        Assert.Equal(0, tt58);   // FIX-5: TT 58 has no chart of accounts
        Assert.Equal(124, total);
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
        Assert.Equal(124, second);
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
