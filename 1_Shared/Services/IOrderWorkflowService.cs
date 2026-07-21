using VanAn.Shared.Domain;

namespace VanAn.Shared.Services
{
    public interface IOrderWorkflowService
    {
        Task<Order?> TransitionStatusAsync(Guid orderId, OrderStatusId newStatus, string? reason = null);
        Task<Order?> GetOrderAsync(Guid orderId);
        Task<List<Order>> GetOrdersByCustomerAsync(string customerDeviceId);
        Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status);
        Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status, Guid tenantId);
        Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus);
    }
}
