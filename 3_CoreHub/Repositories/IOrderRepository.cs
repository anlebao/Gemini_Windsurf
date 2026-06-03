using VanAn.Shared.Domain;
using Microsoft.EntityFrameworkCore.Storage;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository interface for Order entities
    /// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public interface IOrderRepository
    {
        /// <summary>
        /// Gets an order by ID and tenant
        /// </summary>
        Task<Order?> GetByIdAsync(OrderId id, TenantId tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets an order by ID with related entities (Items, Product, Customer)
        /// </summary>
        Task<Order?> GetByIdWithIncludesAsync(Guid orderId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all orders for a tenant
        /// </summary>
        Task<IEnumerable<Order>> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets orders by date range for a tenant
        /// </summary>
        Task<IEnumerable<Order>> GetByDateRangeAsync(
            TenantId tenantId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets orders by status for a tenant
        /// </summary>
        Task<IEnumerable<Order>> GetByStatusAsync(
            TenantId tenantId,
            string status,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets today's orders for a tenant
        /// </summary>
        Task<IEnumerable<Order>> GetTodayOrdersAsync(TenantId tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a new order
        /// </summary>
        Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing order
        /// </summary>
        Task<Order> UpdateAsync(Order order, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves changes to database
        /// </summary>
        Task SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Begins a database transaction
        /// </summary>
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets order count by date range for a tenant
        /// </summary>
        Task<int> GetCountByDateRangeAsync(
            TenantId tenantId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);
    }
}
