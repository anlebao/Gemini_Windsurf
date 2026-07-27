using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Domain.Repositories
{
    /// <summary>
    /// Repository interface for Customer entity
    /// Follows Engineering Constitution: Always filter by tenant and soft delete
    /// NOTE AF-P0-T2: CustomerSegmentCriteria moved to 1_Shared/Domain/CustomerSegmentCriteria.cs (VanAn.Shared.Domain).
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

        /// <summary>
        /// AF-P1-T1: Get ALL active, non-deleted customers across ALL tenants (SystemAdmin only).
        /// Bypasses the global TenantId query filter via IgnoreQueryFilters() so customers from
        /// every tenant are returned regardless of the ambient ITenantProvider context.
        /// DO NOT expose to Owner/Staff roles — only the SystemAdmin-scoped controller action
        /// (GET /api/customers/global with [Authorize(Policy = "SystemAdmin")]) consumes this.
        /// Results are ordered by TenantId then FullName for stable cross-tenant grouping in the UI.
        /// </summary>
        Task<IReadOnlyList<Customer>> GetAllCustomersAcrossTenantsAsync();
    }
}
