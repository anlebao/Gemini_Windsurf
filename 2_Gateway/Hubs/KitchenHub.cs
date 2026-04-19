using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;

namespace VanAn.Gateway.Hubs
{
    [Authorize]
    public class KitchenHub : Hub
    {
        /// <summary>
        /// Join kitchen display for specific shop
        /// </summary>
        public async Task JoinKitchen(string shopId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"shop_{shopId}");
        }

        /// <summary>
        /// Leave kitchen display
        /// </summary>
        public async Task LeaveKitchen(string shopId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"shop_{shopId}");
        }

        /// <summary>
        /// Join order-specific updates
        /// </summary>
        public async Task JoinOrder(string orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
        }

        /// <summary>
        /// Leave order updates
        /// </summary>
        public async Task LeaveOrder(string orderId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order_{orderId}");
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
