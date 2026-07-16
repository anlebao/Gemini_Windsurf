using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository implementation for Product entities — management (write) operations.
    /// Multi-tenancy: every query filters by TenantId. Pattern follows OrderRepository.
    /// </summary>
    public class ProductRepository(IVanAnDbContext context, ILogger<ProductRepository> logger) : IProductRepository
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<ProductRepository> _logger = logger;

        public async Task<Product?> GetByIdAsync(ProductId id, TenantId tenantId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == id.Value && p.TenantId == tenantId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product {ProductId} for tenant {TenantId}", id.Value, tenantId.Value);
                return null;
            }
        }

        public async Task<List<Product>> GetAllForManagementAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Management view: include inactive, exclude soft-deleted (IsDeleted = true)
                return await _context.Products
                    .Where(p => p.TenantId == tenantId && !p.IsDeleted)
                    .OrderByDescending(p => p.UpdatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products for management for tenant {TenantId}", tenantId.Value);
                return new List<Product>();
            }
        }

        public async Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            _ = await _context.Products.AddAsync(product, cancellationToken);
            return product;
        }

        public Task<Product> UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            // EF Core change tracking: entity is already tracked, just return.
            return Task.FromResult(product);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
