using VanAn.Shared.Domain;
using VanAn.CoreHub.Commands;

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

        // NEW METHODS - Queue Support
        Task<Order> CreateOrderWithQueueAsync(Order order, Guid tenantId);
        Task<List<Order>> GetQueuedOrdersAsync(Guid tenantId);
        Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus);

        // NEW METHODS - Enhanced Query Support
        Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status, Guid tenantId);
        Task<OrderDashboardData> GetDashboardDataAsync(Guid tenantId);
        Task<OrderSummary> GetOrderSummaryAsync(Guid orderId, Guid tenantId);

        // NEW METHODS - Accounting Integration
        Task<List<AccountingEntry>> GetEntriesByOrderAsync(Guid orderId, TenantId tenantId);

        // NEW METHODS - Gateway Command Support
        Task<Order> CreateOrderFromCommandAsync(CreateOrderCommand command, Guid tenantId);
    }
}
