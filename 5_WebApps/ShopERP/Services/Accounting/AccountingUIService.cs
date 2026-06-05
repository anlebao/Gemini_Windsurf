using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Services.Accounting
{
    /// <summary>
    /// UI adapter service that maps form data to CoreHub service calls
    /// </summary>
    public class AccountingUIService(IAccountingService accountingService)
    {
        private readonly IAccountingService _accountingService = accountingService;

        /// <summary>
        /// Submit revenue form data to CoreHub service
        /// </summary>
        public async Task<AccountingEntryDto> SubmitRevenueFormAsync(Guid tenantId, Dictionary<string, string> formData)
        {
            decimal amount = decimal.Parse(formData["amount"]);
            DateTime date = DateTime.Parse(formData["date"]);
            string description = formData.GetValueOrDefault("description") ?? string.Empty;

            AccountingPeriod period = new(date.Year, date.Month);
            TenantId tenant = new(tenantId);

            return await _accountingService.CreateRevenueEntryAsync(tenant, period, amount, description);
        }

        /// <summary>
        /// Submit expense form data to CoreHub service
        /// </summary>
        public async Task<AccountingEntryDto> SubmitExpenseFormAsync(Guid tenantId, Dictionary<string, string> formData)
        {
            decimal amount = decimal.Parse(formData["amount"]);
            DateTime date = DateTime.Parse(formData["date"]);
            string description = formData.GetValueOrDefault("description") ?? string.Empty;

            AccountingPeriod period = new(date.Year, date.Month);
            TenantId tenant = new(tenantId);

            return await _accountingService.CreateExpenseEntryAsync(tenant, period, amount, description);
        }

        /// <summary>
        /// Get period-over-period comparison metrics
        /// </summary>
        public async Task<PeriodComparison> GetPeriodComparisonAsync(Guid tenantId, DateTime currentPeriodDate)
        {
            TenantId tenant = new(tenantId);
            AccountingPeriod currentPeriod = new(currentPeriodDate.Year, currentPeriodDate.Month);
            DateTime prevDate = currentPeriodDate.AddMonths(-1);
            AccountingPeriod previousPeriod = new(prevDate.Year, prevDate.Month);

            IEnumerable<AccountingEntryDto> currentEntries = await _accountingService.GetEntriesByTenantAndPeriodAsync(tenant, currentPeriod);
            IEnumerable<AccountingEntryDto> prevEntries = await _accountingService.GetEntriesByTenantAndPeriodAsync(tenant, previousPeriod);

            decimal currentRevenue = currentEntries.Where(e => e.EntryType == AccountingEntryType.Revenue).Sum(e => e.Amount);
            decimal currentExpense = currentEntries.Where(e => e.EntryType == AccountingEntryType.Expense).Sum(e => e.Amount);
            decimal prevRevenueTotal = prevEntries.Where(e => e.EntryType == AccountingEntryType.Revenue).Sum(e => e.Amount);
            decimal prevExpenseTotal = prevEntries.Where(e => e.EntryType == AccountingEntryType.Expense).Sum(e => e.Amount);

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
            return previous == 0 ? current > 0 ? 100m : 0m : (current - previous) / Math.Abs(previous) * 100m;
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
}
