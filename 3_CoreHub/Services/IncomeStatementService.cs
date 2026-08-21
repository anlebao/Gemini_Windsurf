using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Data;
using VanAn.Shared.Domain;
using DomainAccountType = VanAn.Shared.Domain.AccountType;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 4 + TT 99 Phase 4 — Income Statement service (Mẫu B02-DN).
/// TT 99: uses template structure with Mã số (01-60) and formula-based calculated lines.
/// Other standards: flat account list (backward compatible).
/// </summary>
public class IncomeStatementService : IIncomeStatementService
{
    private readonly IAccountingDbContext _dbContext;
    private readonly IAccountChartService _accountChart;
    private readonly ILogger<IncomeStatementService> _logger;

    public IncomeStatementService(IAccountingDbContext dbContext, IAccountChartService accountChart, ILogger<IncomeStatementService> logger)
    {
        _dbContext = dbContext;
        _accountChart = accountChart;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IncomeStatement> GenerateAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating Income Statement for tenant {TenantId}, period {Period}, standard {Standard}",
            tenantId.Value, period.ToString(), standard);

        DateTime periodStart = period.StartDate;
        DateTime periodEnd = period.StartDate.AddMonths(1);
        DateTime priorYearStart = periodStart.AddYears(-1);
        DateTime priorYearEnd = periodEnd.AddYears(-1);

        List<JournalEntry> entries = await _dbContext.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.TenantId == tenantId
                && ((e.EntryDate >= periodStart && e.EntryDate < periodEnd)
                    || (e.EntryDate >= priorYearStart && e.EntryDate < priorYearEnd)))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var endingByAccount = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var openingByAccount = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (JournalEntry entry in entries)
        {
            bool isEnding = entry.EntryDate >= periodStart && entry.EntryDate < periodEnd;
            var target = isEnding ? endingByAccount : openingByAccount;
            foreach (JournalEntryLine line in entry.Lines)
            {
                decimal signed = line.CreditAmount - line.DebitAmount;
                target.TryGetValue(line.AccountNumber, out decimal current);
                target[line.AccountNumber] = current + signed;
            }
        }

        if (standard == AccountingStandard.TT99_2025)
        {
            return await GenerateWithTemplateAsync(tenantId, period, standard, endingByAccount, openingByAccount, ct).ConfigureAwait(false);
        }

        return await GenerateFlatAsync(tenantId, period, standard, endingByAccount, openingByAccount, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// TT 99 template-based generation: Mã số 01-60 with formula-based calculated lines.
    /// </summary>
    private async Task<IncomeStatement> GenerateWithTemplateAsync(
        TenantId tenantId, AccountingPeriod period, AccountingStandard standard,
        Dictionary<string, decimal> endingByAccount, Dictionary<string, decimal> openingByAccount,
        CancellationToken ct)
    {
        var template = Tt99Templates.IncomeStatementTt99;
        var allAccounts = endingByAccount.Keys.Concat(openingByAccount.Keys).Distinct().ToHashSet(StringComparer.Ordinal);

        // Step 1: Calculate direct lines (non-calculated) from account codes.
        var amounts = new Dictionary<string, (decimal Ending, decimal Opening)>(StringComparer.Ordinal);

        foreach (var line in template.Lines)
        {
            if (line.IsCalculated || line.AccountCodes.Length == 0)
            {
                amounts[line.ReportItemCode] = (0, 0);
                continue;
            }

            decimal ending = 0, opening = 0;
            foreach (string code in line.AccountCodes)
            {
                foreach (string acct in allAccounts)
                {
                    if (acct.StartsWith(code, StringComparison.Ordinal) || acct == code)
                    {
                        decimal end = endingByAccount.GetValueOrDefault(acct);
                        decimal op = openingByAccount.GetValueOrDefault(acct);

                        // Determine sign convention from AccountChart.
                        AccountChartEntry? chart = await _accountChart.GetAccountAsync(acct, standard, ct).ConfigureAwait(false);
                        if (chart is not null && chart.Type is DomainAccountType.Expense)
                        {
                            // Expense accounts: debit balance = negative signed → negate to show as positive cost.
                            end = -end;
                            op = -op;
                        }
                        ending += end;
                        opening += op;
                    }
                }
            }
            amounts[line.ReportItemCode] = (ending, opening);
        }

        // Step 2: Calculate formula lines.
        // B 02-DN formulas (VERIFIED from Phụ lục IV TT 99):
        //   10 = 01 - 02
        //   20 = 10 - 11
        //   30 = 20 + 21 + 22 - (23 + 25 + 26)
        //   40 = 31 - 32
        //   50 = 30 + 40
        //   60 = 50 - 51 - 52
        decimal m01e = amounts["01"].Ending, m01o = amounts["01"].Opening;
        decimal m02e = amounts["02"].Ending, m02o = amounts["02"].Opening;
        decimal m11e = amounts["11"].Ending, m11o = amounts["11"].Opening;
        decimal m21e = amounts["21"].Ending, m21o = amounts["21"].Opening;
        decimal m22e = amounts["22"].Ending, m22o = amounts["22"].Opening;
        decimal m23e = amounts["23"].Ending, m23o = amounts["23"].Opening;
        decimal m25e = amounts["25"].Ending, m25o = amounts["25"].Opening;
        decimal m26e = amounts["26"].Ending, m26o = amounts["26"].Opening;
        decimal m31e = amounts["31"].Ending, m31o = amounts["31"].Opening;
        decimal m32e = amounts["32"].Ending, m32o = amounts["32"].Opening;
        decimal m51e = amounts["51"].Ending, m51o = amounts["51"].Opening;
        decimal m52e = amounts["52"].Ending, m52o = amounts["52"].Opening;

        decimal m10e = m01e - m02e, m10o = m01o - m02o;
        decimal m20e = m10e - m11e, m20o = m10o - m11o;
        decimal m30e = m20e + m21e + m22e - (m23e + m25e + m26e), m30o = m20o + m21o + m22o - (m23o + m25o + m26o);
        decimal m40e = m31e - m32e, m40o = m31o - m32o;
        decimal m50e = m30e + m40e, m50o = m30o + m40o;
        decimal m60e = m50e - m51e - m52e, m60o = m50o - m51o - m52o;

        amounts["10"] = (m10e, m10o);
        amounts["20"] = (m20e, m20o);
        amounts["30"] = (m30e, m30o);
        amounts["40"] = (m40e, m40o);
        amounts["50"] = (m50e, m50o);
        amounts["60"] = (m60e, m60o);

        // Step 3: Build FinancialStatementLine list.
        var lines = new List<FinancialStatementLine>();
        foreach (var line in template.Lines)
        {
            var (ending, opening) = amounts[line.ReportItemCode];
            lines.Add(new FinancialStatementLine(
                line.ReportItemCode, line.ReportItemName,
                ending, opening, line.Level,
                line.IsNormalNegative && ending < 0));
        }

        return new IncomeStatement(
            tenantId, period, DateTime.UtcNow,
            TotalRevenueEnding: m01e,
            TotalRevenueOpening: m01o,
            NetProfitEnding: m60e,
            NetProfitOpening: m60o,
            Lines: lines,
            // VA-FI-MVP2: expose COGS (mã 02) + OpEx (mã 11) — values already computed above.
            TotalCogsEnding: m02e, TotalCogsOpening: m02o,
            TotalOpExEnding: m11e, TotalOpExOpening: m11o);
    }

    /// <summary>
    /// Flat account list generation (backward compatible for TT 133 and other standards).
    /// </summary>
    private async Task<IncomeStatement> GenerateFlatAsync(
        TenantId tenantId, AccountingPeriod period, AccountingStandard standard,
        Dictionary<string, decimal> endingByAccount, Dictionary<string, decimal> openingByAccount,
        CancellationToken ct)
    {
        var lines = new List<FinancialStatementLine>();
        decimal totalRevenueEnding = 0, totalRevenueOpening = 0;
        decimal cogsEnding = 0, cogsOpening = 0;
        decimal opexEnding = 0, opexOpening = 0;
        decimal otherIncomeEnding = 0, otherIncomeOpening = 0;
        decimal otherExpenseEnding = 0, otherExpenseOpening = 0;

        var allAccounts = endingByAccount.Keys.Concat(openingByAccount.Keys).Distinct().OrderBy(a => a, StringComparer.Ordinal).ToList();
        foreach (string accountCode in allAccounts)
        {
            decimal ending = endingByAccount.GetValueOrDefault(accountCode);
            decimal opening = openingByAccount.GetValueOrDefault(accountCode);
            AccountChartEntry? chart = await _accountChart.GetAccountAsync(accountCode, standard, ct).ConfigureAwait(false);
            if (chart is null)
            {
                _logger.LogWarning("IS: account {AccountCode} not found in standard {Standard} — skipped", accountCode, standard);
                continue;
            }

            decimal endingPresented = ending;
            decimal openingPresented = opening;

            var line = new FinancialStatementLine(accountCode, chart.AccountName, endingPresented, openingPresented, Level: 1, IsNormalNegative: endingPresented < 0);
            lines.Add(line);

            switch (chart.Type)
            {
                case DomainAccountType.Revenue:
                    totalRevenueEnding += endingPresented;
                    totalRevenueOpening += openingPresented;
                    break;
                case DomainAccountType.Expense:
                    if (accountCode.StartsWith("632", StringComparison.Ordinal))
                    {
                        cogsEnding += -endingPresented;
                        cogsOpening += -openingPresented;
                    }
                    else if (accountCode.StartsWith("64", StringComparison.Ordinal) || accountCode.StartsWith("641", StringComparison.Ordinal) || accountCode.StartsWith("642", StringComparison.Ordinal))
                    {
                        opexEnding += -endingPresented;
                        opexOpening += -openingPresented;
                    }
                    else if (accountCode.StartsWith("8", StringComparison.Ordinal))
                    {
                        otherExpenseEnding += -endingPresented;
                        otherExpenseOpening += -openingPresented;
                    }
                    else
                    {
                        opexEnding += -endingPresented;
                        opexOpening += -openingPresented;
                    }
                    break;
            }
        }

        otherIncomeEnding = endingByAccount.Where(k => k.Key.StartsWith("7", StringComparison.Ordinal)).Sum(k => k.Value);
        otherIncomeOpening = openingByAccount.Where(k => k.Key.StartsWith("7", StringComparison.Ordinal)).Sum(k => k.Value);

        decimal netProfitEnding = totalRevenueEnding - cogsEnding - opexEnding + otherIncomeEnding - otherExpenseEnding;
        decimal netProfitOpening = totalRevenueOpening - cogsOpening - opexOpening + otherIncomeOpening - otherExpenseOpening;

        return new IncomeStatement(
            tenantId, period, DateTime.UtcNow,
            TotalRevenueEnding: totalRevenueEnding,
            TotalRevenueOpening: totalRevenueOpening,
            NetProfitEnding: netProfitEnding,
            NetProfitOpening: netProfitOpening,
            Lines: lines,
            // VA-FI-MVP2: expose COGS + OpEx — values already computed above (cogsEnding/cogsOpening/opexEnding/opexOpening).
            TotalCogsEnding: cogsEnding, TotalCogsOpening: cogsOpening,
            TotalOpExEnding: opexEnding, TotalOpExOpening: opexOpening);
    }
}
