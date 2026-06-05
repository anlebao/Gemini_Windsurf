using VanAn.Shared.Domain;

namespace VanAn.Shared.Services
{
    /// <summary>
    /// Accounting Service interface for financial operations
    /// Phase 2.2: Order to Accounting Integration
    /// </summary>
    public interface IAccountingService
    {
        /// <summary>
        /// Create revenue entry
        /// </summary>
        Task<AccountingEntry> CreateRevenueEntryAsync(TenantId tenantId, AccountingPeriod period, decimal amount, string description);

        /// <summary>
        /// Create expense entry
        /// </summary>
        Task<AccountingEntry> CreateExpenseEntryAsync(TenantId tenantId, AccountingPeriod period, decimal amount, string description);

        /// <summary>
        /// Get accounting entries by order
        /// </summary>
        Task<List<AccountingEntry>> GetEntriesByOrderAsync(TenantId tenantId, Guid orderId);

        /// <summary>
        /// Get entries by period
        /// </summary>
        Task<List<AccountingEntry>> GetEntriesByPeriodAsync(TenantId tenantId, AccountingPeriod period);

        /// <summary>
        /// Generate financial reports
        /// </summary>
        Task<byte[]> GenerateFinancialReportAsync(TenantId tenantId, AccountingPeriod period, string reportType);
    }
}
