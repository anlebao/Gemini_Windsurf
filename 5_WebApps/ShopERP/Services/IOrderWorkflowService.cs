using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services;

public interface IOrderWorkflowService
{
    Task<Order> CreateOrderAsync(Order order);
    Task<Order?> GetOrderByIdAsync(Guid orderId);
    Task<Order> UpdateOrderStatusAsync(Guid orderId, OrderStatusId newStatus);
    Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus);
    Task<List<Order>> GetOrdersByShopAsync(Guid shopId);
    Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status);
}
