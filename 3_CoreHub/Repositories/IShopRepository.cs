using System.Threading;
using System.Threading.Tasks;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories;

/// <summary>
/// Repository interface for Shop entities
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public interface IShopRepository
{
    /// <summary>
    /// Gets a shop by ID
    /// </summary>
    Task<Shop?> GetByIdAsync(Guid shopId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a shop by tenant ID
    /// </summary>
    Task<Shop?> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a new shop
    /// </summary>
    Task<Shop> AddAsync(Shop shop, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing shop
    /// </summary>
    Task<Shop> UpdateAsync(Shop shop, CancellationToken cancellationToken = default);
}
