using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Hubs
{
    // Bug 5 fix: Remove [Authorize] — SignalR client in Blazor Server cannot pass auth cookie
    // to the negotiate endpoint, causing 401 and breaking real-time updates.
    // The hub is internal to ShopERP and only broadcasts events from OrderSyncSubscriber (IHubContext).
    // Blazor pages listen for "OrderCreated"/"OrderStatusChanged" events — they don't call hub methods.
    [AllowAnonymous]
    public class OrderHub(IOrderService orderService, ILogger<OrderHub> logger) : Hub
    {
        private readonly IOrderService _orderService = orderService;
        private readonly ILogger<OrderHub> _logger = logger;

        public async Task JoinOrderGroup(Guid orderId)
        {
            Guid tenantId = GetTenantId();
            Order? order = await _orderService.GetOrderByIdAsync(orderId, tenantId);

            if (order != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
                if (tenantId != Guid.Empty)
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");

                _logger.LogInformation("Connection {ConnectionId} joined order group {OrderId}",
                    Context.ConnectionId, orderId);
            }
        }

        public async Task JoinTenantGroup()
        {
            Guid tenantId = GetTenantId();
            if (tenantId == Guid.Empty)
            {
                _logger.LogDebug("JoinTenantGroup skipped — no TenantId claim (anonymous connection)");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");

            _logger.LogInformation("Connection {ConnectionId} joined tenant group {TenantId}",
                Context.ConnectionId, tenantId);
        }

        private Guid GetTenantId()
        {
            // Extract tenant ID from user claims or context
            // For now, return a default - in real implementation, get from auth
            string? tenantClaim = Context.User?.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out Guid tenantId) ? tenantId : Guid.Empty;
        }
    }
}
