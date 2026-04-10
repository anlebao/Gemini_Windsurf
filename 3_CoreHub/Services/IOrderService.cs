using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Order Service interface for real-time dashboard integration
    /// </summary>
    public interface IOrderService
    {
        /// <summary>
        /// Get today's order count for a specific tenant
        /// </summary>
        Task<int> GetTodayOrderCountAsync(Guid tenantId);
        
        /// <summary>
        /// Get orders by date range for a tenant
        /// </summary>
        Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Get order by ID
        /// </summary>
        Task<Order?> GetOrderByIdAsync(Guid orderId, Guid tenantId);
        
        /// <summary>
        /// Create new order
        /// </summary>
        Task<Order> CreateOrderAsync(Order order, Guid tenantId);
        
        /// <summary>
        /// Update order status
        /// </summary>
        Task<bool> UpdateOrderStatusAsync(Guid orderId, string newStatus, Guid tenantId);
    }
}
