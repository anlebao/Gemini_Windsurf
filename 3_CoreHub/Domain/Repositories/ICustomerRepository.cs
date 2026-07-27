using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Domain.Repositories
{
    /// <summary>
    /// Phase 5 + WS-2: Segmentation criteria for customer filtering (bulk push campaigns + CRM list).
    /// All fields optional — null means no filter on that field.
    /// WS-2 additions: MinPointBalance, MaxPointBalance, BirthdayMonth, LastOrderWithinDays.
    /// </summary>
    public record CustomerSegmentCriteria(
        string? CustomerTier = null,
        IdentityLevel? MinIdentityLevel = null,
        decimal? MinTotalSpent = null,
        decimal? MaxTotalSpent = null,
        DateTime? LastOrderAfter = null,
        DateTime? LastOrderBefore = null,
        bool HasPushSubscription = false,
        // WS-2: Loyalty points range filter (joins LoyaltyRewards table)
        int? MinPointBalance = null,
        int? MaxPointBalance = null,
        // WS-2: Birthday month filter (1-12, null = no filter)
        int? BirthdayMonth = null,
        // WS-2: Convenience filter — last order within N days (converted to LastOrderAfter = Now.AddDays(-N))
        int? LastOrderWithinDays = null);

    /// <summary>
    /// Repository interface for Customer entity
    /// Follows Engineering Constitution: Always filter by tenant and soft delete
    /// </summary>
    public interface ICustomerRepository
    {
        /// <summary>
        /// Get customer by ID (only active, non-deleted, same tenant)
        /// </summary>
        Task<Customer?> GetByIdAsync(Guid id);

        /// <summary>
        /// Get customer by device ID (only active, non-deleted, same tenant)
        /// </summary>
        Task<Customer?> GetByDeviceIdAsync(Guid deviceId);

        /// <summary>
        /// Get all active customers (non-deleted, same tenant)
        /// </summary>
        Task<IReadOnlyList<Customer>> GetAllActiveAsync();

        /// <summary>
        /// Add new customer
        /// </summary>
        Task<Customer> AddAsync(Customer customer);

        /// <summary>
        /// Update existing customer
        /// </summary>
        Task<Customer> UpdateAsync(Customer customer);

        /// <summary>
        /// Soft delete customer by ID
        /// </summary>
        Task<bool> SoftDeleteAsync(Guid id);

        /// <summary>
        /// Check if customer exists by device ID (active, same tenant)
        /// </summary>
        Task<bool> ExistsByDeviceIdAsync(Guid deviceId);

        /// <summary>
        /// Get customer with orders (for complex queries)
        /// </summary>
        Task<Customer?> GetWithOrdersAsync(Guid id);

        /// <summary>
        /// Get customer by phone number (only active, non-deleted, same tenant)
        /// </summary>
        Task<Customer?> GetByPhoneAsync(string phoneNumber);

        /// <summary>
        /// Phase 5: Get customers matching segmentation criteria (for bulk push campaigns).
        /// Filters by tier, identity level, spend, last order date, and push subscription status.
        /// </summary>
        Task<IReadOnlyList<Customer>> GetBySegmentAsync(CustomerSegmentCriteria criteria);

        /// <summary>
        /// Loyalty-C WS-B: Get all active customers whose birthday (month + day) matches today's UTC date.
        /// Used by BirthdayBonusJob to award annual birthday bonus points + send notification.
        /// Birthday is stored as date-only (time = 00:00); comparison is on Month + Day only (year ignored).
        /// </summary>
        Task<IReadOnlyList<Customer>> GetCustomersWithBirthdayTodayAsync();
    }
}
