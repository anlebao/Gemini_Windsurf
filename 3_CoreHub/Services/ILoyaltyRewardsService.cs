using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    public interface ILoyaltyRewardsService
    {
        Task<LoyaltyRewards> GetOrCreateCustomerRewardsAsync(Guid customerId, TenantId tenantId);

        /// <summary>
        /// Loyalty Points Integrity (Batch 2): tenant-scoped Silo award. The tenantId is the
        /// AWARDING tenant — the row at (customerId, tenantId) is created/credited (tenant
        /// attribution: one row per (customer, tenant), no more cross-tenant point merging).
        /// </summary>
        Task<bool> AddPointsAsync(Guid customerId, Guid tenantId, int points, string reason);

        /// <summary>
        /// Loyalty Points Integrity (Batch 2): tenant-scoped Silo spend. Only the row of the
        /// REDEEMING tenant may be deducted ("luật Silo") — a customer cannot spend points
        /// earned at tenant A while redeeming at tenant B.
        /// </summary>
        Task<bool> SubtractPointsAsync(Guid customerId, Guid tenantId, int points, string reason);
        Task<LoyaltyRewards?> GetCustomerRewardsAsync(Guid customerId);
        Task<List<LoyaltyRewards>> GetAllRewardsAsync();
        Task<bool> UpdateHistoryAsync(Guid customerId, string historyEntry);
        Task<bool> ActivateCustomerAsync(Guid customerId);
    }
}
