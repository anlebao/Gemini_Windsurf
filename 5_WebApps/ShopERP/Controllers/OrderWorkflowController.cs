using Microsoft.AspNetCore.Authorization;
using VanAn.Shared.Services;
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
                // Try to get tenantId from claims; fall back to no-tenant (legacy) if not available.
                string? tenantClaim = User.FindFirst("TenantId")?.Value;
                if (Guid.TryParse(tenantClaim, out Guid tenantId) && tenantId != Guid.Empty)
                {
                    List<Order> orders = await _orderWorkflowService.GetOrdersByStatusAsync(new OrderStatusId(status), tenantId);
                    return Ok(orders);
                }
                // No tenant context (anonymous) — return empty list (security: don't leak cross-tenant orders)
                return Ok(new List<Order>());
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

        /// <summary>
        /// W2-T2: Customer confirms receipt of order (ready → delivered).
        /// Called by KhachLink OrderTracking when customer clicks "Xác nhận đã nhận hàng".
        /// Routes through TransitionStatusAsync for validation + outbox events.
        /// Idempotent: if order is already "delivered" or "completed", returns success (no-op).
        /// </summary>
        [HttpPost("{orderId:guid}/confirm-received")]
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmReceived(Guid orderId)
        {
            try
            {
                // Check current status first — if already delivered/completed, return success (idempotent).
                // This handles race conditions where kitchen staff marks order completed while customer
                // is viewing the tracking page with the confirm button still visible.
                Order? existingOrder = await _orderWorkflowService.GetOrderAsync(orderId);
                if (existingOrder == null)
                {
                    return NotFound(new { error = "Order not found" });
                }

                if (existingOrder.Status.Value == "delivered" || existingOrder.Status.Value == "completed")
                {
                    _logger.LogInformation("ConfirmReceived: order {OrderId} already {Status} — returning success (idempotent)",
                        orderId, existingOrder.Status.Value);
                    return Ok(new { success = true, orderId, status = existingOrder.Status.Value });
                }

                Order? order = await _orderWorkflowService.TransitionStatusAsync(
                    orderId,
                    new OrderStatusId("delivered"),
                    "Customer confirmed receipt");

                if (order == null)
                {
                    return NotFound(new { error = "Invalid status transition (ready→delivered required)" });
                }

                return Ok(new { success = true, orderId, status = "delivered" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming receipt for order {OrderId}", orderId);
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
