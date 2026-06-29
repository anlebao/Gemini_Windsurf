using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Domain.Repositories
{
    /// <summary>
    /// Wave 9: Repository interface for PushSubscription entity
    /// Follows Engineering Constitution: Always filter by tenant and soft delete
    /// </summary>
    public interface IPushSubscriptionRepository
    {
        /// <summary>
        /// Get push subscription by ID (only active, non-deleted, same tenant)
        /// </summary>
        Task<PushSubscription?> GetByIdAsync(Guid id);

        /// <summary>
        /// Get active push subscriptions for a customer (non-deleted, same tenant)
        /// </summary>
        Task<IReadOnlyList<PushSubscription>> GetByCustomerIdAsync(Guid customerId);

        /// <summary>
        /// Get all active push subscriptions (non-deleted, same tenant)
        /// </summary>
        Task<IReadOnlyList<PushSubscription>> GetAllActiveAsync();

        /// <summary>
        /// Add new push subscription
        /// </summary>
        Task<PushSubscription> AddAsync(PushSubscription subscription);

        /// <summary>
        /// Update existing push subscription
        /// </summary>
        Task<PushSubscription> UpdateAsync(PushSubscription subscription);

        /// <summary>
        /// Soft delete push subscription by ID
        /// </summary>
        Task<bool> SoftDeleteAsync(Guid id);

        /// <summary>
        /// Delete expired subscriptions (cleanup operation)
        /// </summary>
        Task<int> DeleteExpiredAsync();

        /// <summary>
        /// Get or create subscription for customer (upsert pattern)
        /// </summary>
        Task<PushSubscription> GetOrCreateAsync(Guid customerId, string subscriptionJson, string? userAgent = null);
    }
}