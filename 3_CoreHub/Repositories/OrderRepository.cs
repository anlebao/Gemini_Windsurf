using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository implementation for Order entities
    /// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public class OrderRepository(IVanAnDbContext context, ILogger<OrderRepository> logger) : IOrderRepository
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<OrderRepository> _logger = logger;

        public async Task<Order?> GetByIdAsync(OrderId id, TenantId tenantId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == id.Value && EF.Property<Guid>(o, "TenantId") == tenantId.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId} for tenant {TenantId}", id.Value, tenantId.Value);
                return null;
            }
        }

        public async Task<IEnumerable<Order>> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Orders
                    .Where(o => EF.Property<Guid>(o, "TenantId") == tenantId.Value)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders for tenant {TenantId}", tenantId.Value);
                return new List<Order>();
            }
        }

        public async Task<IEnumerable<Order>> GetByDateRangeAsync(
            TenantId tenantId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Orders
                    .Where(o => EF.Property<Guid>(o, "TenantId") == tenantId.Value &&
                               o.CreatedAt.Date >= startDate.Date &&
                               o.CreatedAt.Date <= endDate.Date)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders for tenant {TenantId} between {StartDate} and {EndDate}",
                    tenantId.Value, startDate, endDate);
                return new List<Order>();
            }
        }

        public async Task<IEnumerable<Order>> GetByStatusAsync(
            TenantId tenantId,
            string status,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Orders
                    .Where(o => EF.Property<Guid>(o, "TenantId") == tenantId.Value && o.Status.Value == status)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders with status {Status} for tenant {TenantId}", status, tenantId.Value);
                return new List<Order>();
            }
        }

        public async Task<IEnumerable<Order>> GetTodayOrdersAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            try
            {
                DateTime today = DateTime.UtcNow.Date;
                return await _context.Orders
                    .Where(o => EF.Property<Guid>(o, "TenantId") == tenantId.Value && o.CreatedAt.Date == today)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today's orders for tenant {TenantId}", tenantId.Value);
                return new List<Order>();
            }
        }

        public async Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default)
        {
            try
            {
                _ = await _context.Orders.AddAsync(order, cancellationToken);
                _ = await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Added order {OrderId} for tenant {TenantId}", order.Id, order.TenantId);
                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding order for tenant {TenantId}", order.TenantId);
                throw;
            }
        }

        public async Task<Order> UpdateAsync(Order order, CancellationToken cancellationToken = default)
        {
            try
            {
                _ = _context.Orders.Update(order);
                _ = await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Updated order {OrderId} for tenant {TenantId}", order.Id, order.TenantId);
                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order {OrderId} for tenant {TenantId}", order.Id, order.TenantId);
                throw;
            }
        }

        public async Task<int> GetCountByDateRangeAsync(
            TenantId tenantId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Orders
                    .CountAsync(o => EF.Property<Guid>(o, "TenantId") == tenantId.Value &&
                                   o.CreatedAt.Date >= startDate.Date &&
                                   o.CreatedAt.Date <= endDate.Date, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order count for tenant {TenantId} between {StartDate} and {EndDate}",
                    tenantId.Value, startDate, endDate);
                return 0;
            }
        }

        public async Task<Order?> GetByIdWithIncludesAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Orders
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .Include(o => o.Customer)
                    .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId} with includes", orderId);
                return null;
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _ = await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return await _context.BeginTransactionAsync(cancellationToken);
        }

        /// <summary>
        /// Gets orders by device ID with null customer (for guest merge)
        /// Sprint 3 incomplete - stub implementation
        /// </summary>
        public async Task<IEnumerable<Order>> GetByDeviceIdAndNullCustomerAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Orders
                    .Where(o => o.CustomerDeviceId == deviceId && o.CustomerId == null)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by device ID {DeviceId} with null customer", deviceId);
                return new List<Order>();
            }
        }

        /// <summary>
        /// Bulk assigns customer to orders by device ID
        /// Sprint 3 incomplete - stub implementation
        /// NOTE: Order.CustomerId has protected set (immutable domain), requires proper domain method
        /// </summary>
        public async Task<int> BulkAssignCustomerAsync(string deviceId, Guid customerId, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("BulkAssignCustomerAsync called but not implemented - Sprint 3 incomplete stub");
            return 0;
        }
    }
}
