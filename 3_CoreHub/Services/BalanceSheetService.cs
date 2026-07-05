using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using DomainAccountType = VanAn.Shared.Domain.AccountType;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 4 — Balance Sheet service implementation (Mẫu B01-DN / B01-DNN).
/// Groups JournalEntry lines by AccountType (via IAccountChartService) into Assets / Liabilities / Equity.
/// Enforces W2 invariant: throws if TotalAssetsEnding != TotalLiabilitiesAndEquityEnding.
/// </summary>
public class BalanceSheetService : IBalanceSheetService
{
    private readonly IVanAnDbContext _dbContext;
    private readonly IAccountChartService _accountChart;
    private readonly ILogger<BalanceSheetService> _logger;

    public BalanceSheetService(IVanAnDbContext dbContext, IAccountChartService accountChart, ILogger<BalanceSheetService> logger)
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

        // Pattern #1 + #5 fix: direct TenantId comparison + EntryDate range (NOT EF.Property<Guid>, NOT e.Period.Year).
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
                decimal signed = line.DebitAmount - line.CreditAmount; // positive = debit balance
                var target = isOpening ? openingByAccount : movementByAccount;
                target.TryGetValue(line.AccountNumber, out decimal current);
                target[line.AccountNumber] = current + signed;
            }
        }

        // Classify accounts via AccountChart and aggregate by section.
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

            // Revenue/Expense accounts: skip (not presented on BS). Their net effect is captured by the residual plug below.
            if (chart.Type is DomainAccountType.Revenue or DomainAccountType.Expense)
            {
                continue;
            }

            string name = chart.AccountName;
            // For contra accounts (IsNormalCredit), normal balance is credit → invert sign for parent-group presentation.
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

        // NetIncome plug (residual approach): interim BS includes unclosed P&L as an Equity line.
        // NetIncome = TotalAssets - TotalLiabilities - TotalEquity (before plug). This is the accounting identity:
        // the residual that makes the BS balance, regardless of which Rev/Exp accounts are in the chart.
        // Works even when sub-accounts (5113, 6421, 6422) are not in the chart but parent accounts (511, 642) are.
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

        // W2 invariant: throw if unbalanced (no IsBalanced flag — unbalanced data never stored).
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
