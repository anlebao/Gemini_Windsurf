using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Seed;

/// <summary>
/// VAS Wave 1 — Tests for VasSampleDataSeeder.
/// Verifies: seed counts, debit=credit balance, VAT split, opening balances, multi-period data.
/// </summary>
public class VasSampleDataSeederTests
{
    private TestContextScope CreateScopeWithVasTenant()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        // Set tenant provider to VAS Enterprise tenant for query filter
        scope.TenantProvider?.SetTenant(VasSampleDataSeeder.VasEnterpriseTenantGuid);
        return scope;
    }

    [Fact]
    public async Task SeedAsync_CreatesTenantAndEntries()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        VasSampleDataSeeder.SeedResult result = await VasSampleDataSeeder.SeedAsync(db);

        Assert.False(result.Skipped);
        // 1 opening + 10 month-1 + 10 month-2 = 21 journal entries (sales create 2 each: sale + COGS)
        // Opening: 1, Month1: T1-T10 (5 sales×2 + 5 simple×1 = 10+5=15), Month2: T11-T20 (5 sales×2 + 5 simple×1 = 10+5=15)
        // Total: 1 + 15 + 15 = 31 journal entries
        Assert.True(result.JournalEntries >= 20, $"Expected >= 20 journal entries, got {result.JournalEntries}");
        Assert.True(result.AccountingEntries >= 20, $"Expected >= 20 accounting entries, got {result.AccountingEntries}");
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);
        VasSampleDataSeeder.SeedResult secondResult = await VasSampleDataSeeder.SeedAsync(db);

        Assert.True(secondResult.Skipped);
    }

    [Fact]
    public async Task SeedAsync_JournalEntriesHaveBalancedDebitCredit()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // Load all journal entries with lines (need to include owned Lines)
        List<JournalEntry> entries = await db.JournalEntries
            .Include(j => j.Lines)
            .ToListAsync();

        Assert.NotEmpty(entries);

        foreach (JournalEntry entry in entries)
        {
            decimal totalDebit = entry.Lines.Sum(l => l.DebitAmount);
            decimal totalCredit = entry.Lines.Sum(l => l.CreditAmount);
            Assert.Equal(totalDebit, totalCredit);
        }
    }

    [Fact]
    public async Task SeedAsync_OpeningBalanceEntryExists()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        JournalEntry? openingEntry = await db.JournalEntries
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Description.Contains("Số dư đầu kỳ"));

        Assert.NotNull(openingEntry);
        // Opening balance has 7 lines: 4 debit (111, 112, 156, 211) + 3 credit (311, 331, 3331)
        Assert.Equal(7, openingEntry!.Lines.Count);

        // Verify specific accounts
        Assert.Contains(openingEntry.Lines, l => l.AccountNumber == "111" && l.DebitAmount > 0);
        Assert.Contains(openingEntry.Lines, l => l.AccountNumber == "112" && l.DebitAmount > 0);
        Assert.Contains(openingEntry.Lines, l => l.AccountNumber == "156" && l.DebitAmount > 0);
        Assert.Contains(openingEntry.Lines, l => l.AccountNumber == "211" && l.DebitAmount > 0);
        Assert.Contains(openingEntry.Lines, l => l.AccountNumber == "411" && l.CreditAmount > 0);
        Assert.Contains(openingEntry.Lines, l => l.AccountNumber == "331" && l.CreditAmount > 0);
        Assert.Contains(openingEntry.Lines, l => l.AccountNumber == "3331" && l.CreditAmount > 0);
    }

    [Fact]
    public async Task SeedAsync_VatSplitCorrect_511Net_3331Vat()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // Find a sale journal entry with VAT (3 lines: debit cash, credit 511, credit 3331)
        List<JournalEntry> saleEntries = await db.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.Description.Contains("Sale") && !j.Description.Contains("COGS"))
            .ToListAsync();

        Assert.NotEmpty(saleEntries);

        foreach (JournalEntry sale in saleEntries)
        {
            // Should have 511 (net revenue) and 3331 (VAT) credit lines
            decimal netRevenue = sale.Lines.Where(l => l.AccountNumber == "511").Sum(l => l.CreditAmount);
            decimal vat = sale.Lines.Where(l => l.AccountNumber == "3331").Sum(l => l.CreditAmount);
            decimal cashDebit = sale.Lines
                .Where(l => l.AccountNumber == "111" || l.AccountNumber == "112")
                .Sum(l => l.DebitAmount);

            Assert.True(netRevenue > 0, $"Sale {sale.Description} should have net revenue (511) > 0");
            Assert.True(vat > 0, $"Sale {sale.Description} should have VAT (3331) > 0");
            Assert.Equal(netRevenue + vat, cashDebit);
        }
    }

    [Fact]
    public async Task SeedAsync_MultiPeriodData_2Months()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // JournalEntries should span 2 months (2026-05 and 2026-06)
        // EntryDate is now persisted (W1 schema fix)
        List<DateTime> entryDates = await db.JournalEntries
            .Select(j => j.EntryDate)
            .ToListAsync();

        Assert.NotEmpty(entryDates);
        var months = entryDates.Select(d => (d.Year, d.Month)).Distinct().ToList();
        Assert.True(months.Count >= 2, $"Expected >= 2 distinct months, got {months.Count}");
        Assert.Contains((2026, 5), months);
        Assert.Contains((2026, 6), months);
    }

    [Fact]
    public async Task SeedAsync_MultiPaymentMethod_CashAndBank()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // Verify both 111 (cash) and 112 (bank) are used in sale entries
        List<JournalEntry> saleEntries = await db.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.Description.Contains("CASH") || j.Description.Contains("VIETQR"))
            .ToListAsync();

        Assert.NotEmpty(saleEntries);

        bool hasCash = saleEntries.Any(e => e.Lines.Any(l => l.AccountNumber == "111" && l.DebitAmount > 0));
        bool hasBank = saleEntries.Any(e => e.Lines.Any(l => l.AccountNumber == "112" && l.DebitAmount > 0));

        Assert.True(hasCash, "Should have CASH (111) sale entries");
        Assert.True(hasBank, "Should have VIETQR (112) sale entries");
    }

    [Fact]
    public async Task SeedAsync_AccountingEntriesHaveCorrectAccountCodes()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // AccountingEntries should have various AccountCodes (511, 3331, 632, 6421, 6422, etc.)
        // TT 133: 641 không tồn tại, dùng 6421 (CP bán hàng) + 6422 (CP QLDN)
        List<string?> accountCodes = await db.AccountingEntries
            .Where(e => e.TenantId == VasSampleDataSeeder.VasEnterpriseTenantId)
            .Select(e => e.AccountCode)
            .Distinct()
            .ToListAsync();

        Assert.Contains("511", accountCodes);
        Assert.Contains("3331", accountCodes);
        Assert.Contains("632", accountCodes);
        Assert.Contains("6421", accountCodes); // TT 133: CP bán hàng (thay 641)
        Assert.Contains("6422", accountCodes); // TT 133: CP QLDN
    }

    [Fact]
    public async Task SeedAsync_CogsUsesAccount632()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // COGS journal entries should debit 632 (not 621 — W0 B3 fix)
        List<JournalEntry> cogsEntries = await db.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.Description.Contains("COGS"))
            .ToListAsync();

        Assert.NotEmpty(cogsEntries);
        foreach (JournalEntry cogs in cogsEntries)
        {
            Assert.Contains(cogs.Lines, l => l.AccountNumber == "632" && l.DebitAmount > 0);
            Assert.Contains(cogs.Lines, l => l.AccountNumber == "156" && l.CreditAmount > 0);
        }
    }

    [Fact]
    public async Task SeedAsync_DepreciationUsesAccount214_Not211()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // Khấu hao: Nợ 6422 / Có 214 (Hao mòn lũy kế) — KHÔNG giảm Nguyên giá 211
        List<JournalEntry> depreciationEntries = await db.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.Description.Contains("Khấu hao"))
            .ToListAsync();

        Assert.NotEmpty(depreciationEntries);
        foreach (JournalEntry dep in depreciationEntries)
        {
            // Debit 6422 (CP QLDN — TT 133)
            Assert.Contains(dep.Lines, l => l.AccountNumber == "6422" && l.DebitAmount > 0);
            // Credit 214 (Hao mòn lũy kế) — NOT 211 (Nguyên giá)
            Assert.Contains(dep.Lines, l => l.AccountNumber == "214" && l.CreditAmount > 0);
            // Must NOT credit 211 (Nguyên giá chỉ giảm khi thanh lý/nhượng bán)
            Assert.DoesNotContain(dep.Lines, l => l.AccountNumber == "211" && l.CreditAmount > 0);
        }
    }

    [Fact]
    public async Task SeedAsync_DiscountReduces511_Not521_TT133()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // TT 133: KHÔNG có TK 521. Chiết khấu = ghi giảm Nợ 511 (W0 Option A)
        List<JournalEntry> discountEntries = await db.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.Description.Contains("Chiết khấu"))
            .ToListAsync();

        Assert.NotEmpty(discountEntries);
        foreach (JournalEntry disc in discountEntries)
        {
            // Debit 511 (ghi giảm doanh thu) — NOT 521 (không tồn tại trong TT 133)
            Assert.Contains(disc.Lines, l => l.AccountNumber == "511" && l.DebitAmount > 0);
            Assert.DoesNotContain(disc.Lines, l => l.AccountNumber == "521");
        }
    }

    [Fact]
    public async Task SeedAsync_ShippingUses5113_Not515()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // Phí vận chuyển = 5113 (Doanh thu CCDV) — NOT 515 (Doanh thu HĐ tài chính)
        List<JournalEntry> shippingEntries = await db.JournalEntries
            .Include(j => j.Lines)
            .Where(j => j.Description.Contains("vận chuyển"))
            .ToListAsync();

        Assert.NotEmpty(shippingEntries);
        foreach (JournalEntry ship in shippingEntries)
        {
            Assert.Contains(ship.Lines, l => l.AccountNumber == "5113" && l.CreditAmount > 0);
            Assert.DoesNotContain(ship.Lines, l => l.AccountNumber == "515");
        }
    }

    [Fact]
    public async Task SeedAsync_TT133_NoAccount641_Uses6421()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // TT 133: KHÔNG có TK 641. CP bán hàng = 6421, CP QLDN = 6422
        List<JournalEntry> allEntries = await db.JournalEntries
            .Include(j => j.Lines)
            .ToListAsync();

        // No entry should use 641 (TT 200 code, not in TT 133)
        Assert.DoesNotContain(allEntries, e => e.Lines.Any(l => l.AccountNumber == "641"));
        // No entry should use 521 (TT 200 code, not in TT 133)
        Assert.DoesNotContain(allEntries, e => e.Lines.Any(l => l.AccountNumber == "521"));
        // No entry should use 311 (removed in TT 200, not in TT 133)
        Assert.DoesNotContain(allEntries, e => e.Lines.Any(l => l.AccountNumber == "311"));
        // No depreciation should credit 211 (must use 214)
        var depEntries = allEntries.Where(e => e.Description.Contains("Khấu hao"));
        Assert.DoesNotContain(depEntries, e => e.Lines.Any(l => l.AccountNumber == "211" && l.CreditAmount > 0));
    }

    [Fact]
    public async Task SeedAsync_EntryDatePersistedCorrectly()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);

        // Verify EntryDate is persisted (not default DateTime)
        List<JournalEntry> entries = await db.JournalEntries.ToListAsync();
        Assert.NotEmpty(entries);

        foreach (JournalEntry entry in entries)
        {
            Assert.True(entry.EntryDate > new DateTime(2026, 1, 1),
                $"Entry {entry.Description} has EntryDate {entry.EntryDate} — should be in 2026");
            Assert.False(entry.EntryDate == default,
                $"Entry {entry.Description} has default EntryDate — column not persisted");
        }
    }

    [Fact]
    public async Task CleanupAsync_RemovesAllSeededData()
    {
        using TestContextScope scope = CreateScopeWithVasTenant();
        VanAnDbContext db = scope.Context;

        _ = await VasSampleDataSeeder.SeedAsync(db);
        await VasSampleDataSeeder.CleanupAsync(db);

        int journalCount = await db.JournalEntries.CountAsync();
        int accountingCount = await db.AccountingEntries
            .CountAsync(e => e.TenantId == VasSampleDataSeeder.VasEnterpriseTenantId);
        int tenantCount = await db.Tenants
            .CountAsync(t => t.Id == VasSampleDataSeeder.VasEnterpriseTenantId);

        Assert.Equal(0, journalCount);
        Assert.Equal(0, accountingCount);
        Assert.Equal(0, tenantCount);
    }
}
