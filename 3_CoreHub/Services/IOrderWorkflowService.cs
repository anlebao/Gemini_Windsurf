using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

public interface IOrderWorkflowService
{
    Task<Order?> TransitionStatusAsync(Guid orderId, OrderStatusId newStatus, string? reason = null);
    Task<Order?> GetOrderAsync(Guid orderId);
    Task<List<Order>> GetOrdersByCustomerAsync(string customerDeviceId);
    Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status);
    Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus);
}
