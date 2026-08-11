using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository interface for Product entities — management (write) operations.
    /// Read endpoints (catalog) remain on the controller via IVanAnDbContext (out of scope).
    /// Implements multi-tenancy: every query filters by TenantId.
    /// </summary>
    public interface IProductRepository
    {
        /// <summary>Get a product by ProductId + TenantId. Returns null if not found or belongs to another tenant.</summary>
        Task<Product?> GetByIdAsync(ProductId id, TenantId tenantId, CancellationToken cancellationToken = default);

        /// <summary>Get all products for management (include inactive, exclude deleted) for a tenant.
        /// #114: includePosOnly=false (default) filters out POS-only service products from non-POS views.</summary>
        Task<List<Product>> GetAllForManagementAsync(TenantId tenantId, CancellationToken cancellationToken = default, bool includePosOnly = false);

        /// <summary>Add a new product.</summary>
        Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default);

        /// <summary>Update an existing product (EF Core change tracking).</summary>
        Task<Product> UpdateAsync(Product product, CancellationToken cancellationToken = default);

        /// <summary>Save changes to database.</summary>
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
