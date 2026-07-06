using Microsoft.AspNetCore.SignalR;

namespace VanAn.Gateway.Hubs
{
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

        // W0-T6: Per-order subscription — staff can subscribe to updates for a specific order
        public async Task JoinOrderGroup(string orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Order_{orderId}");
        }

        public async Task LeaveOrderGroup(string orderId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Order_{orderId}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
