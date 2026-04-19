using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Accounting Service interface for VAT 2026 compliance
    /// Direct Method for Household Businesses
    /// </summary>
    public interface IAccountingService
    {
        /// <summary>
        /// Get today's revenue for a specific tenant (VAT 2026 compliant)
        /// </summary>
        Task<decimal> GetTodayRevenueAsync(Guid tenantId);
        
        /// <summary>
        /// Get revenue by date range with VAT calculation
        /// </summary>
        Task<decimal> GetRevenueByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Create accounting entry (Append-Only - Immutable)
        /// </summary>
        Task<AccountingEntry> CreateEntryAsync(AccountingEntry entry);
        
        /// <summary>
        /// Get accounting entry by ID
        /// </summary>
        Task<AccountingEntry?> GetEntryByIdAsync(Guid entryId);
        
        /// <summary>
        /// Create revenue entry (VAT 2026 compliant)
        /// </summary>
        Task<AccountingEntry> CreateRevenueEntryAsync(TenantId tenantId, AccountingPeriod period, decimal amount, string description);
        
        /// <summary>
        /// Create expense entry (VAT 2026 compliant)
        /// </summary>
        Task<AccountingEntry> CreateExpenseEntryAsync(TenantId tenantId, AccountingPeriod period, decimal amount, string description);
        
        /// <summary>
        /// Get entries by tenant
        /// </summary>
        Task<IEnumerable<AccountingEntry>> GetEntriesByTenantAsync(TenantId tenantId);
        
        /// <summary>
        /// Get entries by tenant and book type
        /// </summary>
        Task<IEnumerable<AccountingEntry>> GetEntriesByTenantAndBookTypeAsync(TenantId tenantId, AccountingBookType bookType);
        
        /// <summary>
        /// Get entries by tenant and period
        /// </summary>
        Task<IEnumerable<AccountingEntry>> GetEntriesByTenantAndPeriodAsync(TenantId tenantId, AccountingPeriod period);
        
        /// <summary>
        /// Create reversal entry for VAT 2026 compliance (Bút toán đảo)
        /// </summary>
        Task<AccountingEntry> CreateReversalEntryAsync(Guid originalEntryId, string reason, Guid tenantId);
        
        /// <summary>
        /// Get accounting entries by date range
        /// </summary>
        Task<IEnumerable<AccountingEntry>> GetEntriesByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Calculate VAT amount based on rate and revenue
        /// Direct Method for Household Businesses per Vietnamese Tax Law 2026
        /// </summary>
        Task<decimal> CalculateVatAsync(decimal revenue, VatRate vatRate);
    }
}
