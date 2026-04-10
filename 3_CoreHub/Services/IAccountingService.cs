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
