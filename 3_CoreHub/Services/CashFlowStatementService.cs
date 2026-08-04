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
    private readonly IBalanceSheetService _balanceSheetService;
    private readonly IIncomeStatementService _incomeStatementService;

    public CashFlowStatementService(
        IAccountingDbContext dbContext,
        IAccountChartService accountChart,
        ILogger<CashFlowStatementService> logger,
        IBalanceSheetService balanceSheetService,
        IIncomeStatementService incomeStatementService)
    {
        _dbContext = dbContext;
        _accountChart = accountChart;
        _logger = logger;
        _balanceSheetService = balanceSheetService;
        _incomeStatementService = incomeStatementService;
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
            Method: CashFlowMethod.Direct,
            OpeningCash: openingCash,
            ClosingCash: closingCash,
            NetChange: netChange,
            OperatingActivities: operating,
            InvestingActivities: investing,
            FinancingActivities: financing);
    }

    /// <summary>
    /// TT 99 B 03-DN indirect method: adjust NetProfit → Operating Cash Flow.
    /// Steps: (1) Get NetProfit from IncomeStatement, (2) add back depreciation/provisions,
    /// (3) adjust for working capital changes from BalanceSheet deltas,
    /// (4) Investing + Financing same as direct method.
    /// </summary>
    public async Task<CashFlowStatement> GenerateIndirectAsync(
        TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating Cash Flow Statement (indirect) for tenant {TenantId}, period {Period}", tenantId.Value, period);

        // Get NetProfit before tax from IncomeStatement (B 02-DN Mã 50).
        var incomeStmt = await _incomeStatementService.GenerateAsync(tenantId, period, standard, ct).ConfigureAwait(false);
        decimal netProfitBeforeTaxEnding = incomeStmt.NetProfitEnding; // Simplified: use NetProfit as proxy for LNST trước thuế
        decimal netProfitBeforeTaxOpening = incomeStmt.NetProfitOpening;

        // Get BalanceSheet for working capital deltas.
        var balanceSheet = await _balanceSheetService.GenerateAsync(tenantId, period, standard, ct).ConfigureAwait(false);

        // Build indirect adjustments per TT 99 B 03-DN Mã 01-17.
        var operating = new List<FinancialStatementLine>();

        // Mã 01: Lợi nhuận trước thuế
        operating.Add(new("01", "Lợi nhuận trước thuế", netProfitBeforeTaxEnding, netProfitBeforeTaxOpening, 1, netProfitBeforeTaxEnding < 0));

        // Mã 02: Khấu hao TSCĐ và BĐSĐT — from JournalEntry lines where account starts with "214".
        decimal depreciationEnding = await GetAccountMovementAsync(tenantId, period, "214", ct).ConfigureAwait(false);
        decimal depreciationOpening = 0; // Simplified for MVP
        operating.Add(new("02", "Khấu hao TSCĐ và BĐSĐT", depreciationEnding, depreciationOpening, 2, false));

        // Mã 03-07: Other adjustments (provisions, FX, investment income/loss, interest, other) — placeholder 0 for MVP.
        operating.Add(new("03", "Các khoản dự phòng", 0, 0, 2, false));
        operating.Add(new("04", "Lãi, lỗ chênh lệch tỷ giá hối đoái", 0, 0, 2, false));
        operating.Add(new("05", "Lãi, lỗ từ hoạt động đầu tư, tài chính", 0, 0, 2, false));
        operating.Add(new("06", "Chi phí đi vay", 0, 0, 2, false));
        operating.Add(new("07", "Các khoản điều chỉnh khác", 0, 0, 2, false));

        // Mã 08: Lợi nhuận từ HĐKD trước thay đổi vốn lưu động (subtotal = 01 + 02 + 03 + 04 + 05 + 06 + 07).
        decimal subtotal08 = netProfitBeforeTaxEnding + depreciationEnding;
        decimal subtotal08Opening = netProfitBeforeTaxOpening + depreciationOpening;
        operating.Add(new("08", "Lợi nhuận từ HĐKD trước thay đổi vốn lưu động", subtotal08, subtotal08Opening, 1, subtotal08 < 0));

        // Mã 09-13: Working capital changes from BalanceSheet deltas (Ending - Opening).
        // Δ must be calculated from BS lines. For MVP, use simple delta from balance sheet Assets/Liabilities.
        decimal deltaReceivables = GetBalanceSheetDelta(balanceSheet, new[]{"131", "136", "138"});
        decimal deltaInventory = GetBalanceSheetDelta(balanceSheet, new[]{"152", "155", "156"});
        decimal deltaPayables = GetBalanceSheetDelta(balanceSheet, new[]{"331"});
        decimal deltaPrepaid = GetBalanceSheetDelta(balanceSheet, new[]{"242"});
        decimal deltaSecurities = GetBalanceSheetDelta(balanceSheet, new[]{"121"});

        operating.Add(new("09", "Tăng, giảm các khoản phải thu", -deltaReceivables, 0, 2, false));
        operating.Add(new("10", "Tăng, giảm hàng tồn kho", -deltaInventory, 0, 2, false));
        operating.Add(new("11", "Tăng, giảm các khoản phải trả", deltaPayables, 0, 2, false));
        operating.Add(new("12", "Tăng, giảm chi phí chờ phân bổ", -deltaPrepaid, 0, 2, false));
        operating.Add(new("13", "Tăng, giảm chứng khoán kinh doanh", -deltaSecurities, 0, 2, false));

        // Mã 14-17: Other operating cash flows — placeholder 0 for MVP.
        operating.Add(new("14", "Chi phí đi vay đã trả", 0, 0, 2, false));
        operating.Add(new("15", "Thuế thu nhập doanh nghiệp đã nộp", 0, 0, 2, false));
        operating.Add(new("16", "Tiền thu khác từ hoạt động kinh doanh", 0, 0, 2, false));
        operating.Add(new("17", "Tiền chi khác cho hoạt động kinh doanh", 0, 0, 2, false));

        // Mã 20: Lưu chuyển tiền thuần từ HĐKD (indirect total).
        decimal operatingTotal = subtotal08 - deltaReceivables - deltaInventory + deltaPayables - deltaPrepaid - deltaSecurities;
        operating.Add(new("20", "Lưu chuyển tiền thuần từ HĐKD", operatingTotal, 0, 1, operatingTotal < 0));

        // Investing + Financing: same as direct method (reuse logic).
        // Re-run direct method to get investing + financing lines.
        var directReport = await GenerateAsync(tenantId, period, standard, ct).ConfigureAwait(false);
        var investing = directReport.InvestingActivities.ToList();
        var financing = directReport.FinancingActivities.ToList();

        // Cash totals.
        decimal openingCash = directReport.OpeningCash;
        decimal closingCash = directReport.ClosingCash;
        decimal netChange = closingCash - openingCash;

        return new CashFlowStatement(
            tenantId, period, DateTime.UtcNow,
            Method: CashFlowMethod.Indirect,
            OpeningCash: openingCash,
            ClosingCash: closingCash,
            NetChange: netChange,
            OperatingActivities: operating,
            InvestingActivities: investing,
            FinancingActivities: financing);
    }

    /// <summary>
    /// Get total movement for accounts matching a prefix in the period.
    /// </summary>
    private async Task<decimal> GetAccountMovementAsync(TenantId tenantId, AccountingPeriod period, string accountPrefix, CancellationToken ct)
    {
        DateTime periodStart = period.StartDate;
        DateTime periodEnd = period.StartDate.AddMonths(1);

        var entries = await _dbContext.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.TenantId == tenantId && e.EntryDate >= periodStart && e.EntryDate < periodEnd)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        decimal total = 0;
        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines)
            {
                if (line.AccountNumber.StartsWith(accountPrefix, StringComparison.Ordinal))
                {
                    // Depreciation (TK 214) is credit-normal → debit balance = negative signed.
                    total += line.CreditAmount - line.DebitAmount;
                }
            }
        }
        return Math.Abs(total); // Depreciation is presented as positive adjustment
    }

    /// <summary>
    /// Calculate delta (Ending - Opening) for specific account codes from BalanceSheet lines.
    /// </summary>
    private static decimal GetBalanceSheetDelta(BalanceSheet bs, string[] accountCodes)
    {
        decimal delta = 0;
        foreach (var line in bs.Assets.Concat(bs.Liabilities).Concat(bs.Equity))
        {
            foreach (string code in accountCodes)
            {
                if (line.ReportItemCode.StartsWith(code, StringComparison.Ordinal) || line.ReportItemCode == code)
                {
                    delta += line.EndingAmount - line.OpeningAmount;
                }
            }
        }
        return delta;
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
