using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using DomainAccountType = VanAn.Shared.Domain.AccountType;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 4 — Income Statement service implementation (Mẫu B02-DN / B02-DNN).
/// 2-column comparative: Ending = current period movement, Opening = same month prior year movement.
/// Revenue (credit 5xx) - COGS (debit 632) - OpEx (debit 641+642) + OtherIncome (credit 7xx) - OtherExpense (debit 8xx) = NetProfit.
/// </summary>
public class IncomeStatementService : IIncomeStatementService
{
    private readonly IVanAnDbContext _dbContext;
    private readonly IAccountChartService _accountChart;
    private readonly ILogger<IncomeStatementService> _logger;

    public IncomeStatementService(IVanAnDbContext dbContext, IAccountChartService accountChart, ILogger<IncomeStatementService> logger)
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
        // Opening column = same month prior year (legal comparative requirement).
        DateTime priorYearStart = periodStart.AddYears(-1);
        DateTime priorYearEnd = periodEnd.AddYears(-1);

        // Pattern #1 + #5 fix.
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
                // For Income Statement: Revenue = credit (+), Expense = debit (+). Signed = credit - debit.
                decimal signed = line.CreditAmount - line.DebitAmount;
                target.TryGetValue(line.AccountNumber, out decimal current);
                target[line.AccountNumber] = current + signed;
            }
        }

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

            // IS presentation: signed = credit - debit already gives correct sign (revenue positive, expense negative).
            // No IsNormalCredit inversion needed — that flag is for BS contra-asset presentation, not IS.
            // Contra-revenue (521, debit balance) → signed negative → naturally reduces revenue. ✓
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
                    // Distinguish COGS (632) vs OpEx (641/642) vs Other Expense (8xx) by account prefix.
                    if (accountCode.StartsWith("632", StringComparison.Ordinal))
                    {
                        cogsEnding += -endingPresented; // expense presented as positive cost
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
                        // Other 6xx expenses → OpEx fallback.
                        opexEnding += -endingPresented;
                        opexOpening += -openingPresented;
                    }
                    break;
                // 7xx = Other Income — classified as Revenue type in chart (W3 seeder).
                // If chart returns Revenue for 7xx, it's already in totalRevenue. If not, handle here:
            }
        }

        // 7xx accounts: if classified as Revenue in chart, they're in totalRevenue. Separate OtherIncome explicitly for clarity.
        // Recompute OtherIncome from 7xx accounts directly (chart may classify 7xx as Revenue).
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
            Lines: lines);
    }
}
