using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 4 — Cash Flow Statement service implementation (Mẫu B03-DN / B03-DNN).
/// Direct method (R4): for each JournalEntry touching cash accounts (111/112), the offsetting line
/// determines the activity classification:
///   - Operating: 5xx (revenue), 6xx (expenses), 521 (contra-revenue), 7xx (other income), 8xx (other expense),
///                331 (payables), 3331 (VAT), 138/338 (other payables), 141/142 (advances).
///   - Investing: 211/213/217 (fixed assets / intangibles / long-term investments).
///   - Financing: 311/341 (long-term borrowings), 411 (equity contributions/dividends).
/// NetChange = ClosingCash - OpeningCash. Detail lines per activity returned (no per-activity totals — W2 design).
/// </summary>
public class CashFlowStatementService : ICashFlowStatementService
{
    private const string CashAccountPrefix1 = "111";
    private const string CashAccountPrefix2 = "112";

    private readonly IAccountingDbContext _dbContext;
    private readonly IAccountChartService _accountChart;
    private readonly ILogger<CashFlowStatementService> _logger;

    public CashFlowStatementService(IAccountingDbContext dbContext, IAccountChartService accountChart, ILogger<CashFlowStatementService> logger)
    {
        _dbContext = dbContext;
        _accountChart = accountChart;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CashFlowStatement> GenerateAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating Cash Flow Statement (direct method) for tenant {TenantId}, period {Period}, standard {Standard}",
            tenantId.Value, period.ToString(), standard);

        DateTime periodStart = period.StartDate;
        DateTime periodEnd = period.StartDate.AddMonths(1);

        // Pattern #1 + #5 fix.
        List<JournalEntry> entries = await _dbContext.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.TenantId == tenantId && e.EntryDate < periodEnd)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // 1. Compute Opening + Closing cash balances (Σ debit - credit for 111/112).
        decimal openingCash = 0;
        decimal closingCash = 0;
        foreach (JournalEntry entry in entries)
        {
            foreach (JournalEntryLine line in entry.Lines)
            {
                if (!IsCashAccount(line.AccountNumber)) continue;
                decimal signed = line.DebitAmount - line.CreditAmount;
                closingCash += signed;
                if (entry.EntryDate < periodStart)
                {
                    openingCash += signed;
                }
            }
        }

        // 2. For the period movement, classify each cash-touching JE by its offsetting account.
        var operating = new List<FinancialStatementLine>();
        var investing = new List<FinancialStatementLine>();
        var financing = new List<FinancialStatementLine>();

        var periodEntries = entries.Where(e => e.EntryDate >= periodStart && e.EntryDate < periodEnd).ToList();
        var activitySums = new Dictionary<(Activity, string), decimal>(); // (activity, offsetAccount) → net cash flow

        foreach (JournalEntry entry in periodEntries)
        {
            var cashLines = entry.Lines.Where(l => IsCashAccount(l.AccountNumber)).ToList();
            if (cashLines.Count == 0) continue;

            // Cash flow direction: cash IN = debit to cash (positive); cash OUT = credit to cash (negative).
            decimal cashDelta = cashLines.Sum(l => l.DebitAmount - l.CreditAmount);

            // Find the offsetting (non-cash) line(s) — classify by the largest offsetting account.
            var offsetLines = entry.Lines.Where(l => !IsCashAccount(l.AccountNumber)).ToList();
            if (offsetLines.Count == 0) continue;

            // Distribute cashDelta across offsetting lines proportionally by absolute amount.
            decimal totalOffsetAbs = offsetLines.Sum(l => l.DebitAmount + l.CreditAmount);
            if (totalOffsetAbs == 0) continue;

            foreach (JournalEntryLine offset in offsetLines)
            {
                decimal share = (offset.DebitAmount + offset.CreditAmount) / totalOffsetAbs;
                decimal lineCashFlow = cashDelta * share;
                Activity activity = ClassifyAccount(offset.AccountNumber);
                var key = (activity, offset.AccountNumber);
                activitySums.TryGetValue(key, out decimal current);
                activitySums[key] = current + lineCashFlow;
            }
        }

        // 3. Build FinancialStatementLines per activity (grouped by offset account).
        decimal operatingTotal = 0, investingTotal = 0, financingTotal = 0;
        foreach (var kvp in activitySums.OrderBy(k => k.Key.Item1).ThenBy(k => k.Key.Item2))
        {
            (Activity activity, string accountCode) = kvp.Key;
            decimal amount = kvp.Value;
            string name = await _accountChart.GetAccountNameAsync(accountCode, standard, ct).ConfigureAwait(false);
            var line = new FinancialStatementLine(accountCode, name, EndingAmount: amount, OpeningAmount: 0, Level: 2, IsNormalNegative: amount < 0);
            switch (activity)
            {
                case Activity.Operating: operating.Add(line); operatingTotal += amount; break;
                case Activity.Investing: investing.Add(line); investingTotal += amount; break;
                case Activity.Financing: financing.Add(line); financingTotal += amount; break;
            }
        }

        // TT 99 Phase 4: Add subtotal lines with Mã số for template structure.
        if (standard == AccountingStandard.TT99_2025)
        {
            operating.Add(new FinancialStatementLine("20", "Lưu chuyển tiền thuần từ HĐKD", operatingTotal, 0, 1, operatingTotal < 0));
            investing.Add(new FinancialStatementLine("30", "Lưu chuyển tiền thuần từ HĐ đầu tư", investingTotal, 0, 1, investingTotal < 0));
            financing.Add(new FinancialStatementLine("40", "Lưu chuyển tiền thuần từ HĐ tài chính", financingTotal, 0, 1, financingTotal < 0));
        }

        decimal netChange = closingCash - openingCash;

        return new CashFlowStatement(
            tenantId, period, DateTime.UtcNow,
            OpeningCash: openingCash,
            ClosingCash: closingCash,
            NetChange: netChange,
            OperatingActivities: operating,
            InvestingActivities: investing,
            FinancingActivities: financing);
    }

    private static bool IsCashAccount(string accountCode) =>
        accountCode.StartsWith(CashAccountPrefix1, StringComparison.Ordinal) ||
        accountCode.StartsWith(CashAccountPrefix2, StringComparison.Ordinal);

    private static Activity ClassifyAccount(string accountCode)
    {
        // Investing: long-term assets (TSCĐ, BĐS đầu tư, đầu tư dài hạn).
        // TT 99 B 03-DN: BĐSĐT (TK 217) cash flows → Investing (Mã 21/22).
        // BĐSĐT revenue (TK 5117) + cost (TK 6327) → Operating (revenue/expense, 5xx/6xx).
        // BĐSĐT depreciation (TK 214) → Indirect method Mã 02 adjustment.
        if (accountCode.StartsWith("211", StringComparison.Ordinal)
            || accountCode.StartsWith("213", StringComparison.Ordinal)
            || accountCode.StartsWith("217", StringComparison.Ordinal) // BĐS đầu tư → Investing (verified TT 99)
            || accountCode.StartsWith("21", StringComparison.Ordinal)) // catch-all 21x (TSCĐ)
        {
            return Activity.Investing;
        }

        // Financing: long-term debt + equity.
        if (accountCode.StartsWith("311", StringComparison.Ordinal)
            || accountCode.StartsWith("341", StringComparison.Ordinal)
            || accountCode.StartsWith("411", StringComparison.Ordinal))
        {
            return Activity.Financing;
        }

        // Everything else → Operating (5xx, 6xx, 7xx, 8xx, 331, 3331, 138, 338, 141, 142, 15x inventory, etc.).
        return Activity.Operating;
    }

    private enum Activity { Operating, Investing, Financing }
}
