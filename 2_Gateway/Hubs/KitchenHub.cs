using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using VanAn.Shared.DTOs;

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
            await Clients.Caller.SendAsync("JoinedKitchen", $"Connected to kitchen {shopId}");
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

        /// <summary>
        /// Broadcast kitchen item status change
        /// </summary>
        public async Task BroadcastStatusChange(KitchenStatusUpdateDto update)
        {
            await Clients.Group($"shop_{update.ShopId}").SendAsync("ItemStatusChanged", update);
        }

        /// <summary>
        /// Broadcast new order confirmation
        /// </summary>
        public async Task BroadcastOrderConfirmation(OrderConfirmedEvent orderEvent)
        {
            await Clients.Group($"shop_{orderEvent.ShopId}").SendAsync("OrderConfirmed", orderEvent);
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            // Clean up group memberships on disconnect
            await base.OnDisconnectedAsync(exception);
        }
    }
}
