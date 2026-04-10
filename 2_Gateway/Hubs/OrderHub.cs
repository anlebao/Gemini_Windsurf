using Microsoft.AspNetCore.SignalR;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Hubs;

public class OrderHub : Hub
{
    private static readonly Dictionary<string, string> ConnectedShops = new();

    public async Task JoinShopGroup(string shopId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Shop_{shopId}");
        ConnectedShops[Context.ConnectionId] = shopId;
        
        await Clients.OthersInGroup($"Shop_{shopId}").SendAsync("StaffConnected", 
            new { ConnectionId = Context.ConnectionId, ConnectedAt = DateTime.UtcNow });
    }

    public async Task LeaveShopGroup(string shopId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Shop_{shopId}");
        ConnectedShops.Remove(Context.ConnectionId);
        
        await Clients.OthersInGroup($"Shop_{shopId}").SendAsync("StaffDisconnected", 
            new { ConnectionId = Context.ConnectionId, DisconnectedAt = DateTime.UtcNow });
    }

    public async Task NotifyNewOrder(string shopId, Order order)
    {
        await Clients.Group($"Shop_{shopId}").SendAsync("NewOrderReceived", new
        {
            OrderId = order.OrderId.Value,
            CustomerDeviceId = order.CustomerDeviceId,
            OrderType = order.OrderType,
            Status = order.Status.Value,
            TotalAmount = order.TotalAmount,
            OrderDate = order.OrderDate,
            Items = order.Items.Select(i => new
            {
                ProductName = i.Product?.Name ?? "Unknown",
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalAmount = i.TotalAmount
            }).ToList()
        });
    }

    public async Task NotifyOrderStatusChanged(string shopId, Order order)
    {
        await Clients.Group($"Shop_{shopId}").SendAsync("OrderStatusChanged", new
        {
            OrderId = order.OrderId.Value,
            OldStatus = order.Status.Value,
            NewStatus = order.Status.Value,
            ChangedAt = DateTime.UtcNow
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectedShops.TryGetValue(Context.ConnectionId, out string? shopId))
        {
            await LeaveShopGroup(shopId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
