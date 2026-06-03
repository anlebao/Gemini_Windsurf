using System.Threading;
using System.Threading.Tasks;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories;

/// <summary>
/// Repository interface for System Metrics and Dashboard data
/// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public interface ISystemMetricsRepository
{
    /// <summary>
    /// Gets tenant count (unique tenants with orders)
    /// </summary>
    Task<int> GetTenantCountAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets total orders count
    /// </summary>
    Task<int> GetTotalOrdersCountAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets total revenue
    /// </summary>
    Task<decimal> GetTotalRevenueAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets tenant count for a date range
    /// </summary>
    Task<int> GetTenantCountByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets order count for a date range
    /// </summary>
    Task<int> GetOrderCountByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets revenue for a date range
    /// </summary>
    Task<decimal> GetRevenueByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets orders count by status
    /// </summary>
    Task<int> GetOrderCountByStatusAsync(string status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets count of orders that have been synced (LastSyncedAt is not null)
    /// </summary>
    Task<int> GetSyncedOrdersCountAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks database connectivity
    /// </summary>
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
