using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Interfaces;
using VanAn.Gateway.Hubs;

namespace VanAn.Gateway.Services
{
    /// <summary>
    /// W0-T2: Implements IOrderNotificationService using IHubContext<OrderHub>.
    /// Broadcasts to ShopGroup (per-tenant) so only staff of that shop receive updates.
    /// Group naming convention: "Shop_{tenantId}" — matches OrderHub.JoinShopGroup.
    ///
    /// #98 fix: Also broadcasts OrderStatusUpdated to LocationHub "order_{orderId}" group
    /// so KhachLink OrderTracking page receives realtime status updates (not just 15s polling).
    ///
    /// All methods are best-effort: exceptions are logged, not thrown.
    /// Notification delivery is NOT part of the order transaction.
    /// </summary>
    public class OrderNotificationService(
        IHubContext<OrderHub> hubContext,
        IHubContext<LocationHub> locationHubContext,
        ILogger<OrderNotificationService> logger) : IOrderNotificationService
    {
        private readonly IHubContext<OrderHub> _hubContext = hubContext;
        private readonly IHubContext<LocationHub> _locationHubContext = locationHubContext;
        private readonly ILogger<OrderNotificationService> _logger = logger;

        public async Task NotifyOrderStatusChangedAsync(Guid orderId, Guid tenantId, string oldStatus, string newStatus)
        {
            try
            {
                await _hubContext.Clients.Group($"Shop_{tenantId}")
                    .SendAsync("OrderStatusChanged", new { orderId, tenantId, oldStatus, newStatus, timestamp = DateTime.UtcNow });
                _logger.LogDebug("Broadcast OrderStatusChanged: {OrderId} {OldStatus}→{NewStatus} to Shop_{TenantId}",
                    orderId, oldStatus, newStatus, tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast OrderStatusChanged for {OrderId}", orderId);
            }

            // #98 fix: Also push to LocationHub order_{orderId} group for KhachLink customers
            try
            {
                await _locationHubContext.Clients.Group($"order_{orderId}")
                    .SendAsync("OrderStatusUpdated", new { orderId, oldStatus, newStatus, timestamp = DateTime.UtcNow });
                _logger.LogDebug("Broadcast OrderStatusUpdated to LocationHub order_{OrderId}: {OldStatus}→{NewStatus}",
                    orderId, oldStatus, newStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast OrderStatusUpdated to LocationHub for {OrderId}", orderId);
            }
        }

        public async Task NotifyPaymentConfirmedAsync(Guid orderId, Guid tenantId, string transactionId)
        {
            try
            {
                await _hubContext.Clients.Group($"Shop_{tenantId}")
                    .SendAsync("PaymentConfirmed", new { orderId, tenantId, transactionId, timestamp = DateTime.UtcNow });
                _logger.LogDebug("Broadcast PaymentConfirmed: {OrderId} to Shop_{TenantId}", orderId, tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast PaymentConfirmed for {OrderId}", orderId);
            }
        }

        public async Task NotifyKitchenItemCompletedAsync(Guid orderId, Guid orderItemId, string newStatus)
        {
            try
            {
                // Kitchen updates broadcast to all — staff filter by orderId on client side.
                // (Kitchen staff may handle multiple orders simultaneously.)
                await _hubContext.Clients.All
                    .SendAsync("KitchenItemCompleted", new { orderId, orderItemId, newStatus, timestamp = DateTime.UtcNow });
                _logger.LogDebug("Broadcast KitchenItemCompleted: {OrderId} item {OrderItemId} → {NewStatus}",
                    orderId, orderItemId, newStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast KitchenItemCompleted for {OrderId}", orderId);
            }
        }
    }
}
