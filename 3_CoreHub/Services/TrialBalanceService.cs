using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 4 — Trial Balance service implementation (Sổ Tổng hợp).
/// Replaces the broken <c>HKDBookService.GenerateTrialBalanceAsync</c> (Pattern #1 + #5 violations).
/// Per-account: opening balance (cumulative EntryDate &lt; periodStart) + movement (period range) → Balance.
/// TotalDebit == TotalCredit validated via the existing <see cref="TrialBalance.IsBalanced"/> flag.
/// </summary>
public class TrialBalanceService : ITrialBalanceService
{
    private readonly IVanAnDbContext _dbContext;
    private readonly IAccountChartService _accountChart;
    private readonly ILogger<TrialBalanceService> _logger;

    public TrialBalanceService(IVanAnDbContext dbContext, IAccountChartService accountChart, ILogger<TrialBalanceService> logger)
    {
        _dbContext = dbContext;
        _accountChart = accountChart;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TrialBalance> GenerateAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating Trial Balance for tenant {TenantId}, period {Period}, standard {Standard}",
            tenantId.Value, period.ToString(), standard);

        DateTime periodStart = period.StartDate;
        DateTime periodEnd = period.StartDate.AddMonths(1);

        // Pattern #1 + #5 fix: direct TenantId comparison + EntryDate range (NOT EF.Property<Guid>, NOT e.Period.Year).
        List<JournalEntry> entries = await _dbContext.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.TenantId == tenantId && e.EntryDate < periodEnd)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Per-account aggregation: opening (EntryDate < periodStart) + movement (periodStart <= EntryDate < periodEnd).
        var openingByAccount = new Dictionary<string, (decimal debit, decimal credit)>(StringComparer.Ordinal);
        var movementByAccount = new Dictionary<string, (decimal debit, decimal credit)>(StringComparer.Ordinal);

        foreach (JournalEntry entry in entries)
        {
            bool isOpening = entry.EntryDate < periodStart;
            var target = isOpening ? openingByAccount : movementByAccount;
            foreach (JournalEntryLine line in entry.Lines)
            {
                target.TryGetValue(line.AccountNumber, out (decimal d, decimal c) current);
                target[line.AccountNumber] = (current.d + line.DebitAmount, current.c + line.CreditAmount);
            }
        }

        var accounts = new List<TrialBalanceAccount>();
        decimal totalDebit = 0;
        decimal totalCredit = 0;

        var allAccountCodes = openingByAccount.Keys.Concat(movementByAccount.Keys).Distinct().OrderBy(a => a, StringComparer.Ordinal).ToList();
        foreach (string accountCode in allAccountCodes)
        {
            (decimal openDebit, decimal openCredit) = openingByAccount.GetValueOrDefault(accountCode);
            (decimal moveDebit, decimal moveCredit) = movementByAccount.GetValueOrDefault(accountCode);

            // Trial balance reports movement debit/credit totals (period activity) + cumulative balance.
            decimal debitTotal = moveDebit;
            decimal creditTotal = moveCredit;
            decimal balance = (openDebit - openCredit) + (moveDebit - moveCredit); // signed: positive = debit balance

            string accountName = await _accountChart.GetAccountNameAsync(accountCode, standard, ct).ConfigureAwait(false);

            accounts.Add(new TrialBalanceAccount(accountCode, accountName, debitTotal, creditTotal, balance));
            totalDebit += debitTotal;
            totalCredit += creditTotal;
        }

        bool isBalanced = Math.Abs(totalDebit - totalCredit) < 0.01m;

        return new TrialBalance(period, DateTime.UtcNow, accounts, totalDebit, totalCredit, isBalanced);
    }
}
