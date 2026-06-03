using VanAn.Shared.Domain;
using Microsoft.EntityFrameworkCore.Storage;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository interface for Loyalty Rewards entities
    /// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public interface ILoyaltyRewardsRepository
    {
        /// <summary>
        /// Gets loyalty rewards by customer ID
        /// </summary>
        Task<LoyaltyRewards?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets loyalty rewards by tenant ID
        /// </summary>
        Task<IEnumerable<LoyaltyRewards>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets active loyalty rewards
        /// </summary>
        Task<IEnumerable<LoyaltyRewards>> GetActiveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a new loyalty reward
        /// </summary>
        Task<LoyaltyRewards> AddAsync(LoyaltyRewards reward, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing loyalty reward
        /// </summary>
        Task<LoyaltyRewards> UpdateAsync(LoyaltyRewards reward, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves changes to database
        /// </summary>
        Task SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Begins a database transaction
        /// </summary>
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets total points for a customer
        /// </summary>
        Task<int> GetTotalPointsAsync(Guid customerId, CancellationToken cancellationToken = default);
    }
}
