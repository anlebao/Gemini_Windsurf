using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository interface for Inventory entities
    /// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public interface IInventoryRepository
    {
        /// <summary>
        /// Gets inventory by ingredient ID
        /// </summary>
        Task<Inventory?> GetByIngredientIdAsync(Guid ingredientId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all inventory for a tenant
        /// </summary>
        Task<IEnumerable<Inventory>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets low stock items for a tenant
        /// </summary>
        Task<IEnumerable<Inventory>> GetLowStockAsync(TenantId tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a new inventory record
        /// </summary>
        Task<Inventory> AddAsync(Inventory inventory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing inventory record
        /// </summary>
        Task<Inventory> UpdateAsync(Inventory inventory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks availability of an ingredient
        /// </summary>
        Task<bool> CheckAvailabilityAsync(Guid ingredientId, int quantity, CancellationToken cancellationToken = default);
    }
}
