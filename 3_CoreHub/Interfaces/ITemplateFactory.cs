using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Interfaces
{
    /// <summary>
    /// Template Factory interface for HKD book generation
    /// Phase 2.3: HKD Books Implementation
    /// </summary>
    public interface ITemplateFactory
    {
        /// <summary>
        /// Generate HKD book template for order
        /// </summary>
        Task GenerateHKDBookAsync(Order order, TenantId tenantId);

        /// <summary>
        /// Generate monthly financial report
        /// </summary>
        Task GenerateMonthlyReportAsync(TenantId tenantId, int year, int month);

        /// <summary>
        /// Generate balance sheet
        /// </summary>
        Task GenerateBalanceSheetAsync(TenantId tenantId, AccountingPeriod period);

        /// <summary>
        /// Get templates for tenant
        /// </summary>
        Task<List<string>> GetTemplatesForTenant(TenantId tenantId);
    }
}
