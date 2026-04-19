using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Order Service implementation for real-time dashboard integration
/// </summary>
public class OrderService : IOrderService
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(VanAnDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get today's order count for a specific tenant
    /// </summary>
    public async Task<int> GetTodayOrderCountAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        
        return await _context.Orders
            .Where(o => o.TenantId.Value == tenantId && 
                       o.CreatedAt >= today && 
                       o.CreatedAt < tomorrow &&
                       !o.IsDeleted)
            .CountAsync();
    }

    /// <summary>
    /// Get orders by date range for a tenant
    /// </summary>
    public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
    {
        return await _context.Orders
            .Where(o => o.TenantId.Value == tenantId && 
                       o.CreatedAt >= startDate && 
                       o.CreatedAt <= endDate &&
                       !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get order by ID
    /// </summary>
    public async Task<Order?> GetOrderByIdAsync(Guid orderId, Guid tenantId)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.Id == orderId && 
                       o.TenantId.Value == tenantId && 
                       !o.IsDeleted)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Create new order
    /// </summary>
    public async Task<Order> CreateOrderAsync(Order order, Guid tenantId)
    {
        // Ensure tenant compliance
        order.TenantId = tenantId;
        order.CreatedAt = DateTime.UtcNow;
        order.IsDeleted = false;

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new order {OrderId} for tenant {TenantId}", order.Id, tenantId);
        return order;
    }

    /// <summary>
    /// Update order status
    /// </summary>
    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, string newStatus, Guid tenantId)
    {
        var order = await _context.Orders
            .Where(o => o.Id == orderId && 
                       o.TenantId.Value == tenantId && 
                       !o.IsDeleted)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for tenant {TenantId}", orderId, tenantId);
            return false;
        }

        order.Status = new OrderStatusId(newStatus);
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated order {OrderId} status to {Status}", orderId, newStatus);
        return true;
    }
}
