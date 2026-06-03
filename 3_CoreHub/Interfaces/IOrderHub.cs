using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Interfaces;

/// <summary>
/// Order Hub interface for real-time notifications
/// Phase 2.5: Backend Consolidation
/// </summary>
public interface IOrderHub
{
    /// <summary>
    /// Send order update notification
    /// </summary>
    Task NotifyOrderUpdateAsync(Order order);
    
    /// <summary>
    /// Send order status change notification
    /// </summary>
    Task NotifyStatusChangeAsync(Guid orderId, string status);
    
    /// <summary>
    /// Send new order notification
    /// </summary>
    Task NotifyNewOrderAsync(Order order);
    
    /// <summary>
    /// Notify staff about new order
    /// </summary>
    Task NotifyStaffAsync(Order order);
}
