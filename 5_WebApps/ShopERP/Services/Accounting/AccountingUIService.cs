using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Services.Accounting;

/// <summary>
/// UI adapter service that maps form data to CoreHub service calls
/// </summary>
public class AccountingUIService
{
    private readonly IAccountingService _accountingService;

    public AccountingUIService(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }

    /// <summary>
    /// Submit revenue form data to CoreHub service
    /// </summary>
    public async Task<AccountingEntryDto> SubmitRevenueFormAsync(Guid tenantId, Dictionary<string, string> formData)
    {
        var amount = decimal.Parse(formData["amount"]);
        var date = DateTime.Parse(formData["date"]);
        var description = formData.GetValueOrDefault("description") ?? string.Empty;

        var period = new AccountingPeriod(date.Year, date.Month);
        var tenant = new TenantId(tenantId);

        return await _accountingService.CreateRevenueEntryAsync(tenant, period, amount, description);
    }

    /// <summary>
    /// Submit expense form data to CoreHub service
    /// </summary>
    public async Task<AccountingEntryDto> SubmitExpenseFormAsync(Guid tenantId, Dictionary<string, string> formData)
    {
        var amount = decimal.Parse(formData["amount"]);
        var date = DateTime.Parse(formData["date"]);
        var description = formData.GetValueOrDefault("description") ?? string.Empty;

        var period = new AccountingPeriod(date.Year, date.Month);
        var tenant = new TenantId(tenantId);

        return await _accountingService.CreateExpenseEntryAsync(tenant, period, amount, description);
    }

    /// <summary>
    /// Get period-over-period comparison metrics
    /// </summary>
    public async Task<PeriodComparison> GetPeriodComparisonAsync(Guid tenantId, DateTime currentPeriodDate)
    {
        var tenant = new TenantId(tenantId);
        var currentPeriod = new AccountingPeriod(currentPeriodDate.Year, currentPeriodDate.Month);
        var prevDate = currentPeriodDate.AddMonths(-1);
        var previousPeriod = new AccountingPeriod(prevDate.Year, prevDate.Month);

        var currentEntries = await _accountingService.GetEntriesByTenantAndPeriodAsync(tenant, currentPeriod);
        var prevEntries = await _accountingService.GetEntriesByTenantAndPeriodAsync(tenant, previousPeriod);

        var currentRevenue = currentEntries.Where(e => e.EntryType == AccountingEntryType.Revenue).Sum(e => e.Amount);
        var currentExpense = currentEntries.Where(e => e.EntryType == AccountingEntryType.Expense).Sum(e => e.Amount);
        var prevRevenueTotal = prevEntries.Where(e => e.EntryType == AccountingEntryType.Revenue).Sum(e => e.Amount);
        var prevExpenseTotal = prevEntries.Where(e => e.EntryType == AccountingEntryType.Expense).Sum(e => e.Amount);

        return new PeriodComparison
        {
            CurrentRevenue = currentRevenue,
            CurrentExpense = currentExpense,
            CurrentProfit = currentRevenue - currentExpense,
            PreviousRevenue = prevRevenueTotal,
            PreviousExpense = prevExpenseTotal,
            PreviousProfit = prevRevenueTotal - prevExpenseTotal,
            RevenueChangePercent = CalculateChangePercent(currentRevenue, prevRevenueTotal),
            ExpenseChangePercent = CalculateChangePercent(currentExpense, prevExpenseTotal),
            ProfitChangePercent = CalculateChangePercent(currentRevenue - currentExpense, prevRevenueTotal - prevExpenseTotal)
        };
    }

    private static decimal CalculateChangePercent(decimal current, decimal previous)
    {
        if (previous == 0) return current > 0 ? 100m : 0m;
        return ((current - previous) / Math.Abs(previous)) * 100m;
    }
}

/// <summary>
/// Period-over-period comparison result
/// </summary>
public class PeriodComparison
{
    public decimal CurrentRevenue { get; set; }
    public decimal CurrentExpense { get; set; }
    public decimal CurrentProfit { get; set; }
    public decimal PreviousRevenue { get; set; }
    public decimal PreviousExpense { get; set; }
    public decimal PreviousProfit { get; set; }
    public decimal RevenueChangePercent { get; set; }
    public decimal ExpenseChangePercent { get; set; }
    public decimal ProfitChangePercent { get; set; }
}
