using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service interface for 7 HKD Books implementation - Phase 2.3.4
    /// Implements Vietnamese Accounting Standard (Thông tư 152/2025/TT-BTC)
    /// 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public interface IHKDBookService
    {
        // Week 1: Basic Revenue/Expense operations
        Task<CoreAccountingEntry> RecordRevenueAsync(
            TenantId tenantId,
            decimal amount,
            string description,
            DateTime? transactionDate = null,
            CancellationToken cancellationToken = default);

        Task<CoreAccountingEntry> RecordExpenseAsync(
            TenantId tenantId,
            decimal amount,
            string description,
            DateTime? transactionDate = null,
            CancellationToken cancellationToken = default);

        // Phase 2.3.4: 7 HKD Books Implementation (Thông tư 152/2025/TT-BTC)

        /// <summary>
        /// Generate S1a-HKD Book (Không chịu thuế GTGT, không nộp thuế TNCN)
        /// For HKD Group 1 businesses
        /// </summary>
        Task<GenericHKDBook> GenerateS1aBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate S2a-HKD Book (Nộp thuế GTGT và TNCN theo tỷ lệ % trên doanh thu)
        /// For HKD Group 2 businesses
        /// </summary>
        Task<GenericHKDBook> GenerateS2aBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate S2b-HKD Book (Sổ doanh thu bán hàng hóa, dịch vụ)
        /// For HKD Group 2 businesses
        /// </summary>
        Task<GenericHKDBook> GenerateS2bBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate S2c-HKD Book (Sổ chi tiết doanh thu, chi phí)
        /// For HKD Group 2 businesses
        /// </summary>
        Task<GenericHKDBook> GenerateS2cBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate S2d-HKD Book (Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa)
        /// For HKD Group 2 businesses
        /// </summary>
        Task<GenericHKDBook> GenerateS2dBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate S2e-HKD Book (Sổ chi tiết tiền)
        /// For HKD Group 2 businesses
        /// </summary>
        Task<GenericHKDBook> GenerateS2eBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate S3a-HKD Book (Hộ kinh doanh có hoạt động thuộc diện chịu các loại thuế khác)
        /// For HKD Group 3 businesses
        /// </summary>
        Task<GenericHKDBook> GenerateS3aBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Business Logic Validation Methods
        /// </summary>
        Task<bool> ValidateHKDGroupAsync(TenantId tenantId, HKDGroup requiredGroup);
        Task<List<AccountingBookType>> GetAvailableBookTypesAsync(TenantId tenantId);
        Task<HKDGroup> GetTenantHKDGroupAsync(TenantId tenantId);

        /// <summary>
        /// Generate all HKD books for a tenant based on their HKD group
        /// </summary>
        Task<HKDBooksPackage> GenerateAllHKDBooksAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        // Existing Week 1 methods (maintained for compatibility)
        Task<decimal> GetRevenueTotalAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        Task<decimal> GetExpenseTotalAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        Task<decimal> GetProfitAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<CoreAccountingEntry>> GetRevenueEntriesAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<CoreAccountingEntry>> GetExpenseEntriesAsync(
            TenantId tenantId,
            AccountingPeriod period,
            CancellationToken cancellationToken = default);
    }
}
