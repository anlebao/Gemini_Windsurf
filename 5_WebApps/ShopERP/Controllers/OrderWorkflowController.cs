using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// API surface for order workflow operations exposed to KhachLink via Gateway.
    /// Business logic remains in CoreHub services; this controller is a thin adapter.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderWorkflowController(
        IOrderWorkflowService orderWorkflowService,
        ILogger<OrderWorkflowController> logger) : ControllerBase
    {
        private readonly IOrderWorkflowService _orderWorkflowService = orderWorkflowService;
        private readonly ILogger<OrderWorkflowController> _logger = logger;

        [HttpGet("{orderId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<Order>> GetOrder(Guid orderId)
        {
            try
            {
                Order? order = await _orderWorkflowService.GetOrderAsync(orderId);
                return order == null ? NotFound() : Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId}", orderId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("by-customer/{customerDeviceId}")]
        [AllowAnonymous]
        public async Task<ActionResult<List<Order>>> GetOrdersByCustomer(string customerDeviceId)
        {
            try
            {
                List<Order> orders = await _orderWorkflowService.GetOrdersByCustomerAsync(customerDeviceId);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders for customer {CustomerDeviceId}", customerDeviceId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{orderId:guid}/status")]
        [Authorize]
        public async Task<ActionResult<Order>> TransitionStatus(Guid orderId, [FromBody] TransitionStatusRequest request)
        {
            try
            {
                Order? order = await _orderWorkflowService.TransitionStatusAsync(
                    orderId,
                    new OrderStatusId(request.Status),
                    request.Reason);

                return order == null ? NotFound() : Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transitioning order {OrderId} to status {Status}", orderId, request.Status);
                return StatusCode(500, "Internal server error");
            }
        }

        // P2 FIX: Missing endpoints referenced by KhachLink OrderWorkflowHttpService

        [HttpGet("by-status/{status}")]
        [AllowAnonymous]
        public async Task<ActionResult<List<Order>>> GetOrdersByStatus(string status)
        {
            try
            {
                List<Order> orders = await _orderWorkflowService.GetOrdersByStatusAsync(new OrderStatusId(status));
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders by status {Status}", status);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("transition-valid")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> IsTransitionValid([FromQuery] string current, [FromQuery] string next)
        {
            try
            {
                bool valid = await _orderWorkflowService.IsTransitionValidAsync(new OrderStatusId(current), new OrderStatusId(next));
                return Ok(valid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking transition validity {Current} -> {Next}", current, next);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class TransitionStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
