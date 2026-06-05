using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Repositories;

namespace VanAn.CoreHub.Infrastructure.Repositories
{
    public class SystemMetricsRepository(IVanAnDbContext context) : ISystemMetricsRepository
    {
        private readonly IVanAnDbContext _context = context;

        public async Task<int> GetTenantCountAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .IgnoreQueryFilters()
                .Select(o => o.TenantId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetTotalOrdersCountAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .IgnoreQueryFilters()
                .CountAsync(cancellationToken);
        }

        public async Task<decimal> GetTotalRevenueAsync(CancellationToken cancellationToken = default)
        {
            List<decimal> amounts = await _context.Orders
                .IgnoreQueryFilters()
                .Select(o => o.TotalAmount)
                .ToListAsync(cancellationToken);
            return amounts.Sum();
        }

        public async Task<int> GetTenantCountByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .IgnoreQueryFilters()
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .Select(o => o.TenantId)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetOrderCountByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .IgnoreQueryFilters()
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .CountAsync(cancellationToken);
        }

        public async Task<decimal> GetRevenueByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            List<decimal> amounts = await _context.Orders
                .IgnoreQueryFilters()
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .Select(o => o.TotalAmount)
                .ToListAsync(cancellationToken);
            return amounts.Sum();
        }

        public async Task<int> GetOrderCountByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            List<Shared.Domain.Order> orders = await _context.Orders
                .IgnoreQueryFilters()
                .ToListAsync(cancellationToken);
            return orders.Count(o => o.Status.Value == status);
        }

        public async Task<int> GetSyncedOrdersCountAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .IgnoreQueryFilters()
                .CountAsync(o => o.LastSyncedAt != null, cancellationToken);
        }

        public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await ((DbContext)_context).Database.CanConnectAsync(cancellationToken);
            }
            catch
            {
                return false;
            }
        }
    }
}
