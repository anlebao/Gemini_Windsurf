using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.PreAggregation
{
    /// <summary>
    /// PreAggregation Service Interface
    /// Optimizes data access by pre-aggregating account balances
    /// </summary>
    public interface IPreAggregationService
    {
        /// <summary>
        /// Get account aggregates for tenant and period
        /// </summary>
        /// <param name="tenantId">Tenant identifier</param>
        /// <param name="period">Accounting period</param>
        /// <returns>Dictionary of aggregated values</returns>
        Task<Dictionary<string, decimal>> GetAccountAggregatesAsync(TenantId tenantId, AccountingPeriod period);
    }
}
