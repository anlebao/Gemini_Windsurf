using Microsoft.AspNetCore.SignalR;

namespace VanAn.Gateway.Hubs;

public class OrderHub : Hub
{
    public async Task JoinShopGroup(string shopId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Shop_{shopId}");
    }

    public async Task LeaveShopGroup(string shopId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Shop_{shopId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
