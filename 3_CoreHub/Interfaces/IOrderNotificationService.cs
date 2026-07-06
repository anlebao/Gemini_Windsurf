using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Interfaces
{
    /// <summary>
    /// W0-T1: Abstraction for real-time order notifications via SignalR.
    /// Implemented in Gateway using IHubContext<OrderHub> (OrderNotificationService).
    /// CoreHub remains pure class library — no SignalR dependency.
    ///
    /// Methods are async and best-effort: failures are logged, not thrown.
    /// Notification delivery is NOT part of the transaction — if SignalR fails,
    /// the order status change still persists (Outbox + NATS handles cross-system sync).
    /// </summary>
    public interface IOrderNotificationService
    {
        /// <summary>Notify staff that order status changed (confirm, preparing, ready, completed).</summary>
        Task NotifyOrderStatusChangedAsync(Guid orderId, Guid tenantId, string oldStatus, string newStatus);

        /// <summary>Notify staff that payment was confirmed for an order.</summary>
        Task NotifyPaymentConfirmedAsync(Guid orderId, Guid tenantId, string transactionId);

        /// <summary>Notify staff that a kitchen item was completed.</summary>
        Task NotifyKitchenItemCompletedAsync(Guid orderId, Guid orderItemId, string newStatus);
    }
}
