using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Infrastructure.Seed;

/// <summary>
/// VAS Wave 1 — Sample data seeder for Enterprise (DN vừa, TT 133/2016) tenant.
/// Seeds opening balances + ~22 journal entries (2 months) + corresponding AccountingEntries.
/// Uses fixed W0 writer patterns: VAT split (511 net + 3331), COGS (632), PaymentMethod mapping (111/112).
/// </summary>
public static class VasSampleDataSeeder
{
    // Fixed tenant ID for deterministic seeding (DN vừa — Enterprise, TT 133)
    public static readonly Guid VasEnterpriseTenantGuid = Guid.Parse("a5b6c7d8-1234-5678-9abc-def012345678");
    public static readonly TenantId VasEnterpriseTenantId = new(VasEnterpriseTenantGuid);

    /// <summary>
    /// Seeds VAS sample data. Idempotent — skips if tenant already exists.
    /// </summary>
    public static async Task<SeedResult> SeedAsync(VanAnDbContext dbContext, ILogger? logger = null, CancellationToken ct = default)
    {
        logger?.LogInformation("VAS W1: Starting sample data seed...");

        // Check idempotency — skip if tenant already exists
        bool tenantExists = await dbContext.Tenants
            .AnyAsync(t => t.Id == VasEnterpriseTenantId, ct);
        if (tenantExists)
        {
            logger?.LogInformation("VAS W1: Tenant already exists, skipping seed.");
            return new SeedResult(0, 0, 0, Skipped: true);
        }

        // 1. Create Enterprise tenant (DN vừa, TT 133/2016)
        var settings = new TenantSettings(
            contactEmail: "contact@vanan-enterprise.vn",
            contactPhone: "028-1234-5678",
            address: "123 Le Loi, Q.1, TP.HCM",
            taxCode: "0301234567");
        var tenant = TenantAggregate.CreateCompany(VasEnterpriseTenantId, "Vạn An Trading Co. (DN vừa TT 133)", settings);
        // W8: Classify tenant as Enterprise_SME with TT 133 standard (for feature flag routing)
        tenant.SetTenantType(TenantType.Enterprise_SME, AccountingStandard.TT133_2016);
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(ct);
        logger?.LogInformation("VAS W1: Created Enterprise tenant {TenantId}", VasEnterpriseTenantGuid);

        // 2. Seed opening balances + transactions
        int journalCount = 0;
        int accountingCount = 0;
        int lineCount = 0;

        // Opening balances (as of 2026-05-01)
        var openingDate = new DateTime(2026, 5, 1);
        var openingPeriod = AccountingPeriod.FromDateTime(openingDate);
        journalCount += await AddOpeningBalancesAsync(dbContext, openingDate, openingPeriod, ct);
        accountingCount += 7; // 7 opening balance AccountingEntries

        // Month 1: 2026-05
        var month1Date = new DateTime(2026, 5, 15);
        var month1Period = new AccountingPeriod(2026, 5);
        (int j1, int a1, int l1) = await AddMonth1TransactionsAsync(dbContext, month1Date, month1Period, ct);
        journalCount += j1;
        accountingCount += a1;
        lineCount += l1;

        // Month 2: 2026-06
        var month2Date = new DateTime(2026, 6, 15);
        var month2Period = new AccountingPeriod(2026, 6);
        (int j2, int a2, int l2) = await AddMonth2TransactionsAsync(dbContext, month2Date, month2Period, ct);
        journalCount += j2;
        accountingCount += a2;
        lineCount += l2;

        await dbContext.SaveChangesAsync(ct);
        logger?.LogInformation("VAS W1: Seed complete — {JournalCount} journal entries, {AccountingCount} accounting entries, {LineCount} lines",
            journalCount, accountingCount, lineCount);

        return new SeedResult(journalCount, accountingCount, lineCount, Skipped: false);
    }

    // ── Opening Balances ──────────────────────────────────────────────────
    private static async Task<int> AddOpeningBalancesAsync(VanAnDbContext db, DateTime date, AccountingPeriod period, CancellationToken ct)
    {
        // Single balanced journal entry for all opening balances
        var je = new JournalEntry(VasEnterpriseTenantId, date, "Số dư đầu kỳ 2026-05-01", "OpeningBalance", null);
        // Debits (assets)
        je.AddLine("111", 50_000_000m, 0, "Tiền mặt đầu kỳ");
        je.AddLine("112", 100_000_000m, 0, "Tiền gửi ngân hàng đầu kỳ");
        je.AddLine("156", 80_000_000m, 0, "Hàng hóa đầu kỳ");
        je.AddLine("211", 200_000_000m, 0, "TSCĐ đầu kỳ");
        // Credits (liabilities + equity)
        je.AddLine("411", 0, 350_000_000m, "Vốn chủ sở hữu đầu kỳ");
        je.AddLine("331", 0, 50_000_000m, "Nợ nhà cung cấp đầu kỳ");
        je.AddLine("3331", 0, 30_000_000m, "Thuế GTGT đầu kỳ");
        db.JournalEntries.Add(je);

        // AccountingEntries for opening balances (Adjustment type, individual accounts)
        db.AccountingEntries.AddRange(
            AccountingEntry.CreateRevenue(VasEnterpriseTenantId, period, new Money(50_000_000m), "Đầu kỳ: Tiền mặt (111)", accountCode: "111"),
            AccountingEntry.CreateRevenue(VasEnterpriseTenantId, period, new Money(100_000_000m), "Đầu kỳ: Tiền gửi NH (112)", accountCode: "112"),
            AccountingEntry.CreateRevenue(VasEnterpriseTenantId, period, new Money(80_000_000m), "Đầu kỳ: Hàng hóa (156)", accountCode: "156"),
            AccountingEntry.CreateRevenue(VasEnterpriseTenantId, period, new Money(200_000_000m), "Đầu kỳ: TSCĐ (211)", accountCode: "211"),
            AccountingEntry.CreateExpense(VasEnterpriseTenantId, period, new Money(350_000_000m), "Đầu kỳ: Vốn CSH (411)", accountCode: "411"),
            AccountingEntry.CreateExpense(VasEnterpriseTenantId, period, new Money(50_000_000m), "Đầu kỳ: NCC (331)", accountCode: "331"),
            AccountingEntry.CreateExpense(VasEnterpriseTenantId, period, new Money(30_000_000m), "Đầu kỳ: VAT (3331)", accountCode: "3331")
        );
        await db.SaveChangesAsync(ct);
        return 1; // 1 journal entry
    }

    // ── Month 1 Transactions (2026-05) ────────────────────────────────────
    private static async Task<(int journals, int accounting, int lines)> AddMonth1TransactionsAsync(
        VanAnDbContext db, DateTime baseDate, AccountingPeriod period, CancellationToken ct)
    {
        int journals = 0;
        int accounting = 0;
        int lines = 0;

        // T1: Bán hàng CASH — 11M (net 10M + VAT 1M)
        (int j, int a, int l) = AddSaleEntry(db, baseDate.AddDays(1), period, "Sale #001 CASH", cashAccount: "111", netRevenue: 10_000_000m, vat: 1_000_000m, cogs: 7_000_000m);
        journals += j; accounting += a; lines += l;

        // T2: Bán hàng VIETQR — 22M (net 20M + VAT 2M)
        (j, a, l) = AddSaleEntry(db, baseDate.AddDays(3), period, "Sale #002 VIETQR", cashAccount: "112", netRevenue: 20_000_000m, vat: 2_000_000m, cogs: 14_000_000m);
        journals += j; accounting += a; lines += l;

        // T3: CP bán hàng — 2M (TT 133: 6421 = Chi phí bán hàng, thay cho 641 của TT 200)
        (j, a, l) = AddSimpleExpense(db, baseDate.AddDays(5), period, "CP bán hàng tháng 5", "6421", "111", 2_000_000m, category: "SellingExpense");
        journals += j; accounting += a; lines += l;

        // T4: CP QLDN — 3M (TT 133: 6422 = Chi phí QLDN)
        (j, a, l) = AddSimpleExpense(db, baseDate.AddDays(6), period, "CP QLDN tháng 5", "6422", "111", 3_000_000m, category: "AdminExpense");
        journals += j; accounting += a; lines += l;

        // T5: Thu tiền công nợ — 5M (debit 111, credit 131)
        (j, a, l) = AddSimpleEntry(db, baseDate.AddDays(7), period, "Thu tiền công nợ", debitAccount: "111", creditAccount: "131", amount: 5_000_000m);
        journals += j; accounting += a; lines += l;

        // T6: Trả NCC — 10M (debit 331, credit 111)
        (j, a, l) = AddSimpleEntry(db, baseDate.AddDays(8), period, "Trả NCC", debitAccount: "331", creditAccount: "111", amount: 10_000_000m);
        journals += j; accounting += a; lines += l;

        // T7: Khấu hao TSCĐ — 5M (Nợ 6422 / Có 214 — Hao mòn lũy kế, KHÔNG giảm Nguyên giá 211)
        (j, a, l) = AddSimpleExpense(db, baseDate.AddDays(10), period, "Khấu hao TSCĐ tháng 5", "6422", "214", 5_000_000m, category: "Depreciation");
        journals += j; accounting += a; lines += l;

        // T8: Lương nhân viên — 8M (TT 133: 6421 = CP bán hàng, thay cho 641)
        (j, a, l) = AddSimpleExpense(db, baseDate.AddDays(12), period, "Lương nhân viên bán hàng tháng 5", "6421", "334", 8_000_000m, category: "Salary");
        journals += j; accounting += a; lines += l;

        // T9: Chiết khấu bán hàng — 1M (TT 133: KHÔNG có TK 521, ghi giảm Nợ 511 trực tiếp — W0 Option A)
        (j, a, l) = AddSimpleEntry(db, baseDate.AddDays(14), period, "Chiết khấu bán hàng (ghi giảm 511)", debitAccount: "511", creditAccount: "111", amount: 1_000_000m);
        journals += j; accounting += a; lines += l;

        // T10: Thu phí vận chuyển — 500K (5113 = Doanh thu cung cấp dịch vụ, KHÔNG phải 515 tài chính)
        (j, a, l) = AddSimpleEntry(db, baseDate.AddDays(15), period, "Thu phí vận chuyển (dịch vụ)", debitAccount: "111", creditAccount: "5113", amount: 500_000m);
        journals += j; accounting += a; lines += l;

        await db.SaveChangesAsync(ct);
        return (journals, accounting, lines);
    }

    // ── Month 2 Transactions (2026-06) ────────────────────────────────────
    private static async Task<(int journals, int accounting, int lines)> AddMonth2TransactionsAsync(
        VanAnDbContext db, DateTime baseDate, AccountingPeriod period, CancellationToken ct)
    {
        int journals = 0;
        int accounting = 0;
        int lines = 0;

        // T11: Bán hàng CASH — 16.5M (net 15M + VAT 1.5M)
        (int j, int a, int l) = AddSaleEntry(db, baseDate.AddDays(1), period, "Sale #003 CASH", cashAccount: "111", netRevenue: 15_000_000m, vat: 1_500_000m, cogs: 10_500_000m);
        journals += j; accounting += a; lines += l;

        // T12: Bán hàng VIETQR — 33M (net 30M + VAT 3M)
        (j, a, l) = AddSaleEntry(db, baseDate.AddDays(3), period, "Sale #004 VIETQR", cashAccount: "112", netRevenue: 30_000_000m, vat: 3_000_000m, cogs: 21_000_000m);
        journals += j; accounting += a; lines += l;

        // T13: CP bán hàng — 2.5M (TT 133: 6421)
        (j, a, l) = AddSimpleExpense(db, baseDate.AddDays(5), period, "CP bán hàng tháng 6", "6421", "111", 2_500_000m, category: "SellingExpense");
        journals += j; accounting += a; lines += l;

        // T14: CP QLDN — 3.5M (TT 133: 6422)
        (j, a, l) = AddSimpleExpense(db, baseDate.AddDays(6), period, "CP QLDN tháng 6", "6422", "111", 3_500_000m, category: "AdminExpense");
        journals += j; accounting += a; lines += l;

        // T15: Trả NCC — 15M (debit 331, credit 111)
        (j, a, l) = AddSimpleEntry(db, baseDate.AddDays(8), period, "Trả NCC tháng 6", debitAccount: "331", creditAccount: "111", amount: 15_000_000m);
        journals += j; accounting += a; lines += l;

        // T16: Khấu hao TSCĐ — 5M (Nợ 6422 / Có 214 — Hao mòn lũy kế)
        (j, a, l) = AddSimpleExpense(db, baseDate.AddDays(10), period, "Khấu hao TSCĐ tháng 6", "6422", "214", 5_000_000m, category: "Depreciation");
        journals += j; accounting += a; lines += l;

        // T17: Lương nhân viên — 9M (TT 133: 6421 = CP bán hàng)
        (j, a, l) = AddSimpleExpense(db, baseDate.AddDays(12), period, "Lương nhân viên bán hàng tháng 6", "6421", "334", 9_000_000m, category: "Salary");
        journals += j; accounting += a; lines += l;

        // T18: Thu tiền công nợ qua NH — 8M (debit 112, credit 131)
        (j, a, l) = AddSimpleEntry(db, baseDate.AddDays(14), period, "Thu tiền công nợ qua NH", debitAccount: "112", creditAccount: "131", amount: 8_000_000m);
        journals += j; accounting += a; lines += l;

        // T19: Bán hàng CASH nhỏ — 5.5M (net 5M + VAT 0.5M)
        (j, a, l) = AddSaleEntry(db, baseDate.AddDays(16), period, "Sale #005 CASH", cashAccount: "111", netRevenue: 5_000_000m, vat: 500_000m, cogs: 3_500_000m);
        journals += j; accounting += a; lines += l;

        // T20: CP điện nước — 1.5M (TT 133: 6422 = CP QLDN)
        (j, a, l) = AddSimpleExpense(db, baseDate.AddDays(18), period, "CP điện nước tháng 6", "6422", "111", 1_500_000m, category: "Utilities");
        journals += j; accounting += a; lines += l;

        await db.SaveChangesAsync(ct);
        return (journals, accounting, lines);
    }

    // ── Helper: Sale entry (revenue + VAT + COGS) ─────────────────────────
    private static (int journals, int accounting, int lines) AddSaleEntry(
        VanAnDbContext db, DateTime date, AccountingPeriod period, string description,
        string cashAccount, decimal netRevenue, decimal vat, decimal cogs)
    {
        // JournalEntry: 3 lines (debit cash, credit 511, credit 3331) + COGS (debit 632, credit 156)
        var saleJe = new JournalEntry(VasEnterpriseTenantId, date, description, "Sale", null);
        saleJe.AddLine(cashAccount, netRevenue + vat, 0, "Tiền thu từ bán hàng");
        saleJe.AddLine("511", 0, netRevenue, "Doanh thu bán hàng (net)");
        if (vat > 0)
            saleJe.AddLine("3331", 0, vat, "Thuế GTGT đầu ra");
        db.JournalEntries.Add(saleJe);

        var cogsJe = new JournalEntry(VasEnterpriseTenantId, date, $"COGS — {description}", "COGS", null);
        cogsJe.AddLine("632", cogs, 0, "Giá vốn hàng bán");
        cogsJe.AddLine("156", 0, cogs, "Xuất hàng hóa");
        db.JournalEntries.Add(cogsJe);

        // AccountingEntries: revenue (511) + VAT (3331) + COGS (632)
        db.AccountingEntries.Add(AccountingEntry.CreateRevenue(
            VasEnterpriseTenantId, period, new Money(netRevenue), $"Doanh thu — {description}", accountCode: "511"));
        if (vat > 0)
            db.AccountingEntries.Add(AccountingEntry.CreateRevenue(
                VasEnterpriseTenantId, period, new Money(vat), $"VAT — {description}", accountCode: "3331"));
        db.AccountingEntries.Add(AccountingEntry.CreateExpense(
            VasEnterpriseTenantId, period, new Money(cogs), $"COGS — {description}", accountCode: "632"));

        int accountingCount = 2 + (vat > 0 ? 1 : 0);
        int lineCount = saleJe.Lines.Count + cogsJe.Lines.Count;
        return (2, accountingCount, lineCount);
    }

    // ── Helper: Simple expense (debit expenseAccount, credit cashAccount) ──
    private static (int journals, int accounting, int lines) AddSimpleExpense(
        VanAnDbContext db, DateTime date, AccountingPeriod period, string description,
        string expenseAccount, string creditAccount, decimal amount, string category)
    {
        var je = new JournalEntry(VasEnterpriseTenantId, date, description, "Expense", null);
        je.AddLine(expenseAccount, amount, 0, description);
        je.AddLine(creditAccount, 0, amount, description);
        db.JournalEntries.Add(je);

        db.AccountingEntries.Add(AccountingEntry.CreateExpense(
            VasEnterpriseTenantId, period, new Money(amount), description, accountCode: expenseAccount, category: category));

        return (1, 1, 2);
    }

    // ── Helper: Simple double-entry (no AccountingEntry — non-revenue/expense) ──
    private static (int journals, int accounting, int lines) AddSimpleEntry(
        VanAnDbContext db, DateTime date, AccountingPeriod period, string description,
        string debitAccount, string creditAccount, decimal amount)
    {
        var je = new JournalEntry(VasEnterpriseTenantId, date, description, "Adjustment", null);
        je.AddLine(debitAccount, amount, 0, description);
        je.AddLine(creditAccount, 0, amount, description);
        db.JournalEntries.Add(je);

        // For non-revenue/expense entries (e.g., debt payment, discount), create AccountingEntry
        // with the debit account as AccountCode for traceability
        db.AccountingEntries.Add(AccountingEntry.CreateRevenue(
            VasEnterpriseTenantId, period, new Money(amount), description, accountCode: debitAccount));

        return (1, 1, 2);
    }

    /// <summary>
    /// Cleans up all VAS sample data for the seeded tenant.
    /// </summary>
    public static async Task CleanupAsync(VanAnDbContext db, CancellationToken ct = default)
    {
        var journalEntries = await db.JournalEntries
            .Where(j => j.TenantId == VasEnterpriseTenantId)
            .ToListAsync(ct);
        db.JournalEntries.RemoveRange(journalEntries);

        var accountingEntries = await db.AccountingEntries
            .Where(e => e.TenantId == VasEnterpriseTenantId)
            .ToListAsync(ct);
        db.AccountingEntries.RemoveRange(accountingEntries);

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Id == VasEnterpriseTenantId, ct);
        if (tenant != null)
            db.Tenants.Remove(tenant);

        await db.SaveChangesAsync(ct);
    }

    public record SeedResult(int JournalEntries, int AccountingEntries, int JournalLines, bool Skipped);
}
