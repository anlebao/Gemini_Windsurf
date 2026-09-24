using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Hubs;
using VanAn.CoreHub.Interfaces;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// NF-2: IOrderNotificationService implementation for ShopERP scope.
    /// Broadcasts on ShopERP's own /orderHub (VanAn.CoreHub.Hubs.OrderHub) to all
    /// connected Blazor staff pages (Orders/Index, Orders/Detail, Kitchen/Display,
    /// VanADashboard). Previously IOrderNotificationService was only registered on
    /// Gateway — every broadcast call in ShopERP scope was a silent no-op, leaving
    /// the "OrderStatusChanged"/"PaymentConfirmed" client listeners dead.
    ///
    /// Signatures match the client handlers exactly:
    ///   OrderStatusChanged    → On&lt;Guid, Guid, string, string&gt;
    ///   PaymentConfirmed      → On&lt;Guid, Guid, string&gt;
    ///   KitchenItemCompleted  → no current client listener — sent for contract completeness
    ///
    /// Clients.All (no groups) — same pattern as OrderSyncSubscriber's OrderCreated
    /// broadcast; a ShopERP instance serves a single tenant so there is no cross-tenant leak.
    /// All methods are best-effort: exceptions are logged, not thrown.
    /// </summary>
    public class ShopErpOrderNotificationService(
        IHubContext<OrderHub> hubContext,
        ILogger<ShopErpOrderNotificationService> logger) : IOrderNotificationService
    {
        private readonly IHubContext<OrderHub> _hubContext = hubContext;
        private readonly ILogger<ShopErpOrderNotificationService> _logger = logger;

        public async Task NotifyOrderStatusChangedAsync(Guid orderId, Guid tenantId, string oldStatus, string newStatus)
        {
            try
            {
                await _hubContext.Clients.All
                    .SendAsync("OrderStatusChanged", orderId, tenantId, oldStatus, newStatus);
                _logger.LogDebug("Broadcast OrderStatusChanged: {OrderId} {OldStatus}→{NewStatus} (tenant {TenantId})",
                    orderId, oldStatus, newStatus, tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast OrderStatusChanged for {OrderId}", orderId);
            }
        }

        public async Task NotifyPaymentConfirmedAsync(Guid orderId, Guid tenantId, string transactionId)
        {
            try
            {
                await _hubContext.Clients.All
                    .SendAsync("PaymentConfirmed", orderId, tenantId, transactionId);
                _logger.LogDebug("Broadcast PaymentConfirmed: {OrderId} (tenant {TenantId})", orderId, tenantId);
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
                await _hubContext.Clients.All
                    .SendAsync("KitchenItemCompleted", orderId, orderItemId, newStatus);
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
