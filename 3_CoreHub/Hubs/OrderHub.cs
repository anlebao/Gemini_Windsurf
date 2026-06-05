using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Hubs
{
    [Authorize]
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
                await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");

                _logger.LogInformation("Connection {ConnectionId} joined order group {OrderId}",
                    Context.ConnectionId, orderId);
            }
        }

        public async Task JoinTenantGroup()
        {
            Guid tenantId = GetTenantId();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");

            _logger.LogInformation("Connection {ConnectionId} joined tenant group {TenantId}",
                Context.ConnectionId, tenantId);
        }

        public async Task NotifyStaffAsync(Order order)
        {
            await Clients.Group($"tenant_{order.TenantId}").SendAsync("OrderCreated", new
            {
                OrderId = order.Id,
                CustomerName = order.CustomerInfo?.FullName ?? "Khách hàng",
                TotalAmount = order.TotalPrice,
                order.Status,
                order.CreatedAt,
                Items = order.Items.Select(i => new
                {
                    i.ProductName,
                    i.Quantity,
                    i.UnitPrice
                }).ToList()
            });

            _logger.LogInformation("Notified staff for order {OrderId}", order.Id);
        }

        public async Task NotifyCustomerAsync(Guid orderId, OrderStatusId status)
        {
            await Clients.Group($"order_{orderId}").SendAsync("OrderStatusUpdated", new
            {
                OrderId = orderId,
                Status = status.Value,
                StatusDisplay = GetStatusDisplay(status),
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notified customer for order {OrderId} status {Status}", orderId, status);
        }

        private static string GetStatusDisplay(OrderStatusId status)
        {
            return status.Value switch
            {
                "pending" => "🔄 Đang chờ xử lý",
                "preparing" => "🔥 Đang chuẩn bị",
                "ready" => "🎯 Sẵn sàng",
                "delivering" => "🚚 Đang giao hàng",
                "completed" => "✅ Hoàn thành",
                "cancelled" => "❌ Đã hủy",
                _ => status.Value
            };
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
