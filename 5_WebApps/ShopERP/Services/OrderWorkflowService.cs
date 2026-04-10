using VanAn.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace VanAn.ShopERP.Services;

public class OrderWorkflowService : IOrderWorkflowService
{
    private readonly DbContext _context;
    private readonly ILogger<OrderWorkflowService> _logger;

    public OrderWorkflowService(DbContext context, ILogger<OrderWorkflowService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        try
        {
            await _context.Set<Order>().AddAsync(order);
            await _context.SaveChangesAsync();
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            throw;
        }
    }

    public async Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        return await _context.Set<Order>().FindAsync(orderId);
    }

    public async Task<Order> UpdateOrderStatusAsync(Guid orderId, OrderStatusId newStatus)
    {
        var order = await _context.Set<Order>().FindAsync(orderId);
        if (order == null)
        {
            throw new ArgumentException($"Order with ID {orderId} not found");
        }

        if (await IsTransitionValidAsync(order.Status, newStatus))
        {
            order.Status = newStatus;
            await _context.SaveChangesAsync();
            return order;
        }

        throw new InvalidOperationException($"Invalid status transition from {order.Status} to {newStatus}");
    }

    public async Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus)
    {
        // Simple validation logic - can be expanded
        return true;
    }

    public async Task<List<Order>> GetOrdersByShopAsync(Guid shopId)
    {
        return await _context.Set<Order>()
            .Where(o => o.TenantId == shopId)
            .ToListAsync();
    }

    public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status)
    {
        return await _context.Set<Order>()
            .Where(o => o.Status == status)
            .ToListAsync();
    }
}
