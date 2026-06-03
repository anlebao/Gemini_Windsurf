using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using Microsoft.Extensions.Logging;

[Authorize]
public class OrderHub : Hub
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderHub> _logger;
    
    public OrderHub(IOrderService orderService, ILogger<OrderHub> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }
    
    public async Task JoinOrderGroup(Guid orderId)
    {
        var tenantId = GetTenantId();
        var order = await _orderService.GetOrderByIdAsync(orderId, tenantId);
        
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
        var tenantId = GetTenantId();
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
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(i => new 
            {
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
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
    
    private string GetStatusDisplay(OrderStatusId status)
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
        var tenantClaim = Context.User?.FindFirst("TenantId")?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
    }
}
