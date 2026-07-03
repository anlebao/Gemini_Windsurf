using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

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
        Task<AccountingEntryDto> CreateEntryAsync(AccountingEntryDto entry);

        /// <summary>
        /// Get accounting entry by ID
        /// </summary>
        Task<AccountingEntryDto?> GetEntryByIdAsync(Guid entryId);

        /// <summary>
        /// Create revenue entry (VAT 2026 compliant)
        /// accountCode: mã tài khoản kế toán (511, 515...) — TT 152/2025/TT-BTC
        /// reference: số chứng từ/hóa đơn tham chiếu
        /// industrySector: Wave 5 — ngành nghề kinh doanh (TT 152 S2a/S2b industry-group split)
        /// </summary>
        Task<AccountingEntryDto> CreateRevenueEntryAsync(TenantId tenantId, AccountingPeriod period, decimal amount, string description,
            string? accountCode = null, string? reference = null, IndustrySector? industrySector = null);

        /// <summary>
        /// Create expense entry (VAT 2026 compliant)
        /// accountCode: mã tài khoản kế toán (621, 627, 642...) — TT 152/2025/TT-BTC
        /// vendor: nhà cung cấp; category: danh mục chi phí; reference: số chứng từ
        /// industrySector: Wave 5 — ngành nghề kinh doanh (TT 152 S2a/S2b industry-group split)
        /// </summary>
        Task<AccountingEntryDto> CreateExpenseEntryAsync(TenantId tenantId, AccountingPeriod period, decimal amount, string description,
            string? accountCode = null, string? vendor = null, string? category = null, string? reference = null,
            IndustrySector? industrySector = null);

        /// <summary>
        /// Get entries by tenant
        /// </summary>
        Task<IEnumerable<AccountingEntryDto>> GetEntriesByTenantAsync(TenantId tenantId);

        /// <summary>
        /// Get entries by tenant and book type
        /// </summary>
        Task<IEnumerable<AccountingEntryDto>> GetEntriesByTenantAndBookTypeAsync(TenantId tenantId, AccountingBookType bookType);

        /// <summary>
        /// Get entries by tenant and period
        /// </summary>
        Task<IEnumerable<AccountingEntryDto>> GetEntriesByTenantAndPeriodAsync(TenantId tenantId, AccountingPeriod period);

        /// <summary>
        /// Create reversal entry for VAT 2026 compliance (Bút toán đảo)
        /// </summary>
        Task<AccountingEntryDto> CreateReversalEntryAsync(Guid originalEntryId, string reason, Guid tenantId);

        /// <summary>
        /// Get accounting entries by date range
        /// </summary>
        Task<IEnumerable<AccountingEntryDto>> GetEntriesByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Calculate VAT amount based on rate and revenue
        /// Direct Method for Household Businesses per Vietnamese Tax Law 2026
        /// </summary>
        decimal CalculateVat(decimal revenue, VatRate vatRate);
    }
}
