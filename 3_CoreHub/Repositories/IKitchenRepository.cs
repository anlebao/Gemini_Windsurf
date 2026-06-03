using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories;

/// <summary>
/// Repository interface for Kitchen/Order operations
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public interface IKitchenRepository
{
    /// <summary>
    /// Gets orders by status for kitchen operations
    /// </summary>
    Task<IEnumerable<Order>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets orders by status and tenant for kitchen operations
    /// </summary>
    Task<IEnumerable<Order>> GetByStatusAndTenantAsync(string status, TenantId tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets pending orders for kitchen
    /// </summary>
    Task<IEnumerable<Order>> GetPendingOrdersAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates order status
    /// </summary>
    Task<Order> UpdateOrderStatusAsync(OrderId orderId, string newStatus, CancellationToken cancellationToken = default);
}
