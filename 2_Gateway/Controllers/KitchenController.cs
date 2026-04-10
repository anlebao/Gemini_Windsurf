using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VanAn.Shared.DTOs;
using VanAn.Shared.Services;
using VanAn.Gateway.Hubs;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Masterchef,Staff,Manager")]
    public class KitchenController : ControllerBase
    {
        private readonly IKitchenService _kitchenService;
        private readonly IHubContext<KitchenHub> _hubContext;

        public KitchenController(IKitchenService kitchenService, IHubContext<KitchenHub> hubContext)
        {
            _kitchenService = kitchenService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Get grouped kitchen items for display
        /// </summary>
        [HttpGet("items/{shopId}")]
        public async Task<ActionResult<List<KitchenItemGroupDto>>> GetGroupedItems(Guid shopId)
        {
            var result = await _kitchenService.GetGroupedKitchenItemsAsync(shopId);
            return Ok(result);
        }

        /// <summary>
        /// Update kitchen item status
        /// </summary>
        [HttpPut("status")]
        public async Task<ActionResult<bool>> UpdateItemStatus([FromBody] KitchenStatusUpdateDto update)
        {
            var userIdStr = User.FindFirst("sub")?.Value ?? Guid.NewGuid().ToString();
            var userId = Guid.TryParse(userIdStr, out var parsedUserId) ? parsedUserId : Guid.NewGuid();
            
            var result = await _kitchenService.UpdateItemStatusAsync(update, userId);
            
            if (result)
            {
                // Broadcast update to all kitchen clients
                await _hubContext.Clients.Group($"shop_{update.ShopId}").SendAsync("ItemStatusChanged", update);
            }
            
            return Ok(result);
        }

        /// <summary>
        /// Process voice note for order
        /// </summary>
        [HttpPost("voice-note/{orderId}")]
        public async Task<ActionResult<VoiceNoteDto>> ProcessVoiceNote(Guid orderId, [FromBody] VoiceNoteDto voiceNote)
        {
            var result = await _kitchenService.ProcessVoiceNoteAsync(orderId, voiceNote);
            
            // Broadcast voice note update
            await _hubContext.Clients.Group($"order_{orderId}").SendAsync("VoiceNoteProcessed", new { OrderId = orderId, VoiceNote = result });
            
            return Ok(result);
        }

        /// <summary>
        /// Get kitchen analytics
        /// </summary>
        [HttpGet("analytics/{shopId}")]
        public async Task<ActionResult<KitchenAnalyticsDto>> GetAnalytics(Guid shopId, [FromQuery] DateTime from)
        {
            var result = await _kitchenService.GetKitchenAnalyticsAsync(shopId, from);
            return Ok(result);
        }

        /// <summary>
        /// Get order kitchen status
        /// </summary>
        [HttpGet("order-status/{orderId}")]
        public async Task<ActionResult<KitchenStatus>> GetOrderStatus(Guid orderId)
        {
            var result = await _kitchenService.GetOrderKitchenStatusAsync(orderId);
            return Ok(result);
        }
    }
}
