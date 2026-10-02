using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Data;
using VanAn.Shared.Domain;
using DomainAccountType = VanAn.Shared.Domain.AccountType;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 4 + TT 99 Phase 4 — Balance Sheet service (Mẫu B01-DN).
/// TT 99: uses template structure with hierarchical Mã số (100/110/111...).
/// Other standards: flat account list (backward compatible).
/// Enforces W2 invariant: throws if TotalAssetsEnding != TotalLiabilitiesAndEquityEnding.
/// </summary>
public class BalanceSheetService : IBalanceSheetService
{
    private readonly IAccountingDbContext _dbContext;
    private readonly IAccountChartService _accountChart;
    private readonly ILogger<BalanceSheetService> _logger;

    public BalanceSheetService(IAccountingDbContext dbContext, IAccountChartService accountChart, ILogger<BalanceSheetService> logger)
    {
        _dbContext = dbContext;
        _accountChart = accountChart;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BalanceSheet> GenerateAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating Balance Sheet for tenant {TenantId}, period {Period}, standard {Standard}",
            tenantId.Value, period.ToString(), standard);

        DateTime periodStart = period.StartDate;
        DateTime periodEnd = period.StartDate.AddMonths(1);

        // Pattern #1 + #5 fix: direct TenantId comparison + EntryDate range.
        List<JournalEntry> entries = await _dbContext.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.TenantId == tenantId && e.EntryDate < periodEnd)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Aggregate per account code: opening (EntryDate < periodStart) + movement (periodStart <= EntryDate < periodEnd).
        var openingByAccount = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var movementByAccount = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (JournalEntry entry in entries)
        {
            bool isOpening = entry.EntryDate < periodStart;
            foreach (JournalEntryLine line in entry.Lines)
            {
                decimal signed = line.DebitAmount - line.CreditAmount;
                var target = isOpening ? openingByAccount : movementByAccount;
                target.TryGetValue(line.AccountNumber, out decimal current);
                target[line.AccountNumber] = current + signed;
            }
        }

        // TT 99: use template structure with hierarchical Mã số.
        // Other standards: keep flat account list (backward compatible).
        if (standard == AccountingStandard.TT99_2025)
        {
            return await GenerateWithTemplateAsync(tenantId, period, standard, openingByAccount, movementByAccount, ct).ConfigureAwait(false);
        }

        // TT 71/2024 (HTX) — B01-HTX: template riêng (Tiền 110... Tổng tài sản 200 / Nợ 300 / VCSH 400 / Tổng nguồn 500).
        if (standard == AccountingStandard.TT71_2024)
        {
            return await GenerateTt71Async(tenantId, period, openingByAccount, movementByAccount, ct).ConfigureAwait(false);
        }

        return await GenerateFlatAsync(tenantId, period, standard, openingByAccount, movementByAccount, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// TT 99 template-based generation: groups accounts into hierarchical Mã số indicators.
    /// </summary>
    private async Task<BalanceSheet> GenerateWithTemplateAsync(
        TenantId tenantId, AccountingPeriod period, AccountingStandard standard,
        Dictionary<string, decimal> openingByAccount, Dictionary<string, decimal> movementByAccount,
        CancellationToken ct)
    {
        var template = Tt99Templates.BalanceSheetTt99;

        // Step 1: Calculate amounts for each template line (direct lines only).
        // Sign convention dùng chung: contra/credit-normal (IsNormalCredit) → negate.
        var lineAmounts = await ComputeBalanceSheetDirectLinesAsync(
            template, openingByAccount, movementByAccount, standard, ct).ConfigureAwait(false);

        // Step 2: Calculate calculated lines (section/group headers) by summing their children.
        // Children = all non-calculated lines with Level > current line's Level, until next line with same or lower Level.
        for (int i = 0; i < template.Lines.Count; i++)
        {
            var line = template.Lines[i];
            if (!line.IsCalculated) continue;

            decimal ending = 0, opening = 0;
            for (int j = i + 1; j < template.Lines.Count; j++)
            {
                var child = template.Lines[j];
                if (child.Level <= line.Level) break;
                if (child.IsCalculated) continue; // Skip intermediate calculated lines (they're sums of their own children)

                var (childEnding, childOpening) = lineAmounts[child.ReportItemCode];
                ending += childEnding;
                opening += childOpening;
            }
            lineAmounts[line.ReportItemCode] = (ending, opening);
        }

        // Step 3: NetIncome plug (residual): Mã 420 = TotalAssets - TotalLiabilities - TotalEquity (before plug).
        // Template Mã 280 = Total Assets, Mã 300 = Total Liabilities, Mã 400 = Total Equity.
        var (totalAssetsEnding, totalAssetsOpening) = lineAmounts["280"];
        var (totalLiabEnding, totalLiabOpening) = lineAmounts.GetValueOrDefault("300");
        var (totalEquityEnding, totalEquityOpening) = lineAmounts.GetValueOrDefault("400");

        // Calculate equity before plug (subtract existing 420 amount).
        var (ln420Ending, ln420Opening) = lineAmounts.GetValueOrDefault("420");
        decimal equityBeforePlugEnding = totalEquityEnding - ln420Ending;
        decimal equityBeforePlugOpening = totalEquityOpening - ln420Opening;

        decimal netIncomeEnding = totalAssetsEnding - totalLiabEnding - equityBeforePlugEnding;
        decimal netIncomeOpening = totalAssetsOpening - totalLiabOpening - equityBeforePlugOpening;

        if (Math.Abs(netIncomeEnding) > 0.005m || Math.Abs(netIncomeOpening) > 0.005m)
        {
            // Add NetIncome to Mã 420 (LNST chưa phân phối).
            lineAmounts["420"] = (ln420Ending + netIncomeEnding, ln420Opening + netIncomeOpening);
            // Recalculate Mã 400 total.
            lineAmounts["400"] = (totalEquityEnding + netIncomeEnding, totalEquityOpening + netIncomeOpening);
            // Recalculate Mã 440 total.
            lineAmounts["440"] = (totalLiabEnding + totalEquityEnding + netIncomeEnding,
                                   totalLiabOpening + totalEquityOpening + netIncomeOpening);
        }

        // Step 4: Build FinancialStatementLine list and split into Assets / Liabilities / Equity.
        var assetLines = new List<FinancialStatementLine>();
        var liabilityLines = new List<FinancialStatementLine>();
        var equityLines = new List<FinancialStatementLine>();

        foreach (var templateLine in template.Lines)
        {
            var (ending, opening) = lineAmounts[templateLine.ReportItemCode];
            var fsLine = new FinancialStatementLine(
                templateLine.ReportItemCode,
                templateLine.ReportItemName,
                ending, opening,
                templateLine.Level,
                templateLine.IsNormalNegative && ending < 0);

            // Classify by Mã số range:
            // 100-274 + 280 → Assets
            // 300-344 → Liabilities
            // 400-420 + 440 → Equity
            int code = int.TryParse(templateLine.ReportItemCode, out int c) ? c : 0;
            if (code is >= 100 and <= 280)
                assetLines.Add(fsLine);
            else if (code is >= 300 and <= 344)
                liabilityLines.Add(fsLine);
            else if (code is >= 400 and <= 440)
                equityLines.Add(fsLine);
        }

        // Final totals.
        decimal finalTotalAssetsEnding = lineAmounts["280"].Ending;
        decimal finalTotalAssetsOpening = lineAmounts["280"].Opening;
        decimal finalTotalLiabEquityEnding = lineAmounts["440"].Ending;
        decimal finalTotalLiabEquityOpening = lineAmounts["440"].Opening;

        // W2 invariant.
        const decimal tolerance = 0.01m;
        if (Math.Abs(finalTotalAssetsEnding - finalTotalLiabEquityEnding) > tolerance)
        {
            throw new InvalidOperationException(
                $"Balance Sheet invariant violated: TotalAssetsEnding ({finalTotalAssetsEnding}) != TotalLiabilitiesAndEquityEnding ({finalTotalLiabEquityEnding}) " +
                $"for tenant {tenantId.Value}, period {period}. Check JournalEntry double-entry integrity.");
        }

        return new BalanceSheet(
            tenantId, period, DateTime.UtcNow,
            Assets: assetLines,
            Liabilities: liabilityLines,
            Equity: equityLines,
            TotalAssetsEnding: finalTotalAssetsEnding,
            TotalAssetsOpening: finalTotalAssetsOpening,
            TotalLiabilitiesAndEquityEnding: finalTotalLiabEquityEnding,
            TotalLiabilitiesAndEquityOpening: finalTotalLiabEquityOpening);
    }

    /// <summary>
    /// Computes direct (non-calculated) template lines from aggregated account balances.
    /// Sign convention: normal-credit accounts (liability/equity/contra-asset) → negate to present positive.
    /// Dùng chung cho B01-DN (TT 99) và B01-HTX (TT 71).
    /// </summary>
    private async Task<Dictionary<string, (decimal Ending, decimal Opening)>> ComputeBalanceSheetDirectLinesAsync(
        Tt99ReportTemplate template,
        Dictionary<string, decimal> openingByAccount, Dictionary<string, decimal> movementByAccount,
        AccountingStandard standard, CancellationToken ct)
    {
        var allAccounts = openingByAccount.Keys.Concat(movementByAccount.Keys).Distinct().ToHashSet(StringComparer.Ordinal);
        var lineAmounts = new Dictionary<string, (decimal Ending, decimal Opening)>(StringComparer.Ordinal);

        foreach (var templateLine in template.Lines)
        {
            if (templateLine.IsCalculated || templateLine.AccountCodes.Length == 0)
            {
                lineAmounts[templateLine.ReportItemCode] = (0, 0);
                continue;
            }

            decimal ending = 0, opening = 0;
            foreach (string code in templateLine.AccountCodes)
            {
                // Match by prefix: "111" matches "111", "1111", "1112" etc.
                foreach (string acct in allAccounts)
                {
                    if (acct.StartsWith(code, StringComparison.Ordinal) || acct == code)
                    {
                        decimal op = openingByAccount.GetValueOrDefault(acct);
                        decimal mv = movementByAccount.GetValueOrDefault(acct);
                        decimal end = op + mv;

                        // Check if this account is a contra account (credit-normal) for sign adjustment.
                        AccountChartEntry? chart = await _accountChart.GetAccountAsync(acct, standard, ct).ConfigureAwait(false);
                        if (chart is not null && chart.IsNormalCredit)
                        {
                            op = -op;
                            end = -end;
                        }
                        ending += end;
                        opening += op;
                    }
                }
            }
            lineAmounts[templateLine.ReportItemCode] = (ending, opening);
        }

        return lineAmounts;
    }

    /// <summary>
    /// TT 71 (HTX) — B01-HTX template-based generation theo Phụ lục IV TT 71/2024/TT-BTC (Mục I.1).
    /// Totals: 150=151+152 · 160=161+162 · 200=110+120+130+140+150+160+170+180
    ///         300=310+...+380 · 400=410+420+430+440 · 500=300+400
    /// Mã 137 ("Trong đó: Phải thu từ hoạt động cho vay nội bộ") chỉ thuyết minh — KHÔNG cộng vào tổng.
    /// NetIncome plug vào Mã 420 (giống B01-DN) → W2 invariant: 200 == 500.
    /// </summary>
    private async Task<BalanceSheet> GenerateTt71Async(
        TenantId tenantId, AccountingPeriod period,
        Dictionary<string, decimal> openingByAccount, Dictionary<string, decimal> movementByAccount,
        CancellationToken ct)
    {
        var template = Tt71Templates.BalanceSheetTt71;
        var lineAmounts = await ComputeBalanceSheetDirectLinesAsync(
            template, openingByAccount, movementByAccount, AccountingStandard.TT71_2024, ct).ConfigureAwait(false);

        void Set(string code, decimal ending, decimal opening) => lineAmounts[code] = (ending, opening);
        decimal SumEnding(params string[] codes) => codes.Sum(c => lineAmounts[c].Ending);
        decimal SumOpening(params string[] codes) => codes.Sum(c => lineAmounts[c].Opening);

        // Step 2: B01-HTX formula lines.
        Set("150", SumEnding("151", "152"), SumOpening("151", "152"));
        Set("160", SumEnding("161", "162"), SumOpening("161", "162"));
        Set("200", SumEnding("110", "120", "130", "140", "150", "160", "170", "180"),
                   SumOpening("110", "120", "130", "140", "150", "160", "170", "180"));
        Set("300", SumEnding("310", "320", "330", "340", "350", "360", "370", "380"),
                   SumOpening("310", "320", "330", "340", "350", "360", "370", "380"));
        Set("400", SumEnding("410", "420", "430", "440"),
                   SumOpening("410", "420", "430", "440"));
        Set("500", SumEnding("300", "400"), SumOpening("300", "400"));

        // Step 3: NetIncome plug (residual): Mã 420 = 200 - 300 - equity-before-plug (giống B01-DN).
        var (totalAssetsEnding, totalAssetsOpening) = lineAmounts["200"];
        var (totalLiabEnding, totalLiabOpening) = lineAmounts["300"];
        var (totalEquityEnding, totalEquityOpening) = lineAmounts["400"];
        var (ln420Ending, ln420Opening) = lineAmounts["420"];

        decimal equityBeforePlugEnding = totalEquityEnding - ln420Ending;
        decimal equityBeforePlugOpening = totalEquityOpening - ln420Opening;

        decimal netIncomeEnding = totalAssetsEnding - totalLiabEnding - equityBeforePlugEnding;
        decimal netIncomeOpening = totalAssetsOpening - totalLiabOpening - equityBeforePlugOpening;

        if (Math.Abs(netIncomeEnding) > 0.005m || Math.Abs(netIncomeOpening) > 0.005m)
        {
            // Add NetIncome to Mã 420 (LNST chưa phân phối).
            Set("420", ln420Ending + netIncomeEnding, ln420Opening + netIncomeOpening);
            // Recalculate Mã 400 + Mã 500.
            Set("400", totalEquityEnding + netIncomeEnding, totalEquityOpening + netIncomeOpening);
            Set("500", totalLiabEnding + totalEquityEnding + netIncomeEnding,
                       totalLiabOpening + totalEquityOpening + netIncomeOpening);
        }

        // Step 4: Build FinancialStatementLine list and split into Assets / Liabilities / Equity.
        var assetLines = new List<FinancialStatementLine>();
        var liabilityLines = new List<FinancialStatementLine>();
        var equityLines = new List<FinancialStatementLine>();

        foreach (var templateLine in template.Lines)
        {
            var (ending, opening) = lineAmounts[templateLine.ReportItemCode];
            var fsLine = new FinancialStatementLine(
                templateLine.ReportItemCode,
                templateLine.ReportItemName,
                ending, opening,
                templateLine.Level,
                templateLine.IsNormalNegative && ending < 0);

            // Classify by Mã số range (B01-HTX):
            // 110-200 → Tài sản (gồm Mã 200 tổng) · 300-380 → Nợ phải trả · 400-500 → VCSH (gồm Mã 500 tổng).
            int code = int.TryParse(templateLine.ReportItemCode, out int c) ? c : 0;
            if (code is >= 110 and <= 200)
                assetLines.Add(fsLine);
            else if (code is >= 300 and <= 380)
                liabilityLines.Add(fsLine);
            else if (code is >= 400 and <= 500)
                equityLines.Add(fsLine);
        }

        // Final totals.
        decimal finalTotalAssetsEnding = lineAmounts["200"].Ending;
        decimal finalTotalAssetsOpening = lineAmounts["200"].Opening;
        decimal finalTotalLiabEquityEnding = lineAmounts["500"].Ending;
        decimal finalTotalLiabEquityOpening = lineAmounts["500"].Opening;

        // W2 invariant.
        const decimal tolerance = 0.01m;
        if (Math.Abs(finalTotalAssetsEnding - finalTotalLiabEquityEnding) > tolerance)
        {
            throw new InvalidOperationException(
                $"Balance Sheet invariant violated: TotalAssetsEnding ({finalTotalAssetsEnding}) != TotalLiabilitiesAndEquityEnding ({finalTotalLiabEquityEnding}) " +
                $"for tenant {tenantId.Value}, period {period}. Check JournalEntry double-entry integrity.");
        }

        return new BalanceSheet(
            tenantId, period, DateTime.UtcNow,
            Assets: assetLines,
            Liabilities: liabilityLines,
            Equity: equityLines,
            TotalAssetsEnding: finalTotalAssetsEnding,
            TotalAssetsOpening: finalTotalAssetsOpening,
            TotalLiabilitiesAndEquityEnding: finalTotalLiabEquityEnding,
            TotalLiabilitiesAndEquityOpening: finalTotalLiabEquityOpening);
    }

    /// <summary>
    /// Flat account list generation (backward compatible for TT 133 and other standards).
    /// </summary>
    private async Task<BalanceSheet> GenerateFlatAsync(
        TenantId tenantId, AccountingPeriod period, AccountingStandard standard,
        Dictionary<string, decimal> openingByAccount, Dictionary<string, decimal> movementByAccount,
        CancellationToken ct)
    {
        var assetLines = new List<FinancialStatementLine>();
        var liabilityLines = new List<FinancialStatementLine>();
        var equityLines = new List<FinancialStatementLine>();
        decimal totalAssetsEnding = 0, totalAssetsOpening = 0;
        decimal totalLiabilitiesEnding = 0, totalLiabilitiesOpening = 0;
        decimal totalEquityEnding = 0, totalEquityOpening = 0;

        var allAccounts = openingByAccount.Keys.Concat(movementByAccount.Keys).Distinct().OrderBy(a => a, StringComparer.Ordinal).ToList();
        foreach (string accountCode in allAccounts)
        {
            decimal opening = openingByAccount.GetValueOrDefault(accountCode);
            decimal movement = movementByAccount.GetValueOrDefault(accountCode);
            decimal ending = opening + movement;
            AccountChartEntry? chart = await _accountChart.GetAccountAsync(accountCode, standard, ct).ConfigureAwait(false);
            if (chart is null)
            {
                _logger.LogWarning("BS: account {AccountCode} not found in standard {Standard} — skipped (absorbed by NetIncome plug)", accountCode, standard);
                continue;
            }

            if (chart.Type is DomainAccountType.Revenue or DomainAccountType.Expense)
                continue;

            string name = chart.AccountName;
            decimal openingPresented = chart.IsNormalCredit ? -opening : opening;
            decimal endingPresented = chart.IsNormalCredit ? -ending : ending;

            var line = new FinancialStatementLine(accountCode, name, endingPresented, openingPresented, Level: 1, IsNormalNegative: chart.IsNormalCredit && endingPresented < 0);

            switch (chart.Type)
            {
                case DomainAccountType.Asset:
                    assetLines.Add(line);
                    totalAssetsEnding += endingPresented;
                    totalAssetsOpening += openingPresented;
                    break;
                case DomainAccountType.Liability:
                    liabilityLines.Add(line);
                    totalLiabilitiesEnding += endingPresented;
                    totalLiabilitiesOpening += openingPresented;
                    break;
                case DomainAccountType.Equity:
                    equityLines.Add(line);
                    totalEquityEnding += endingPresented;
                    totalEquityOpening += openingPresented;
                    break;
            }
        }

        // NetIncome plug (residual approach).
        decimal netIncomeEnding = totalAssetsEnding - totalLiabilitiesEnding - totalEquityEnding;
        decimal netIncomeOpening = totalAssetsOpening - totalLiabilitiesOpening - totalEquityOpening;
        if (Math.Abs(netIncomeEnding) > 0.005m || Math.Abs(netIncomeOpening) > 0.005m)
        {
            equityLines.Add(new FinancialStatementLine(
                ReportItemCode: "421",
                ReportItemName: "Lợi nhuận sau thuế chưa phân phối (kết quả kỳ này)",
                EndingAmount: netIncomeEnding,
                OpeningAmount: netIncomeOpening,
                Level: 1,
                IsNormalNegative: netIncomeEnding < 0));
            totalEquityEnding += netIncomeEnding;
            totalEquityOpening += netIncomeOpening;
        }

        decimal totalLiabAndEquityEnding = totalLiabilitiesEnding + totalEquityEnding;
        decimal totalLiabAndEquityOpening = totalLiabilitiesOpening + totalEquityOpening;

        const decimal tolerance = 0.01m;
        if (Math.Abs(totalAssetsEnding - totalLiabAndEquityEnding) > tolerance)
        {
            throw new InvalidOperationException(
                $"Balance Sheet invariant violated: TotalAssetsEnding ({totalAssetsEnding}) != TotalLiabilitiesAndEquityEnding ({totalLiabAndEquityEnding}) " +
                $"for tenant {tenantId.Value}, period {period}. Check JournalEntry double-entry integrity.");
        }

        return new BalanceSheet(
            tenantId, period, DateTime.UtcNow,
            Assets: assetLines,
            Liabilities: liabilityLines,
            Equity: equityLines,
            TotalAssetsEnding: totalAssetsEnding,
            TotalAssetsOpening: totalAssetsOpening,
            TotalLiabilitiesAndEquityEnding: totalLiabAndEquityEnding,
            TotalLiabilitiesAndEquityOpening: totalLiabAndEquityOpening);
    }
}
