using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController(
        IOrderService orderService,
        IOrderManagementService orderManagementService,
        ILogger<OrdersController> logger) : ControllerBase
    {
        private readonly IOrderService _orderService = orderService;
        private readonly IOrderManagementService _orderManagementService = orderManagementService;
        private readonly ILogger<OrdersController> _logger = logger;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromQuery] string? status = null)
        {
            try
            {
                Guid tenantId = GetTenantId();

                if (string.IsNullOrEmpty(status))
                {
                    DateTime today = DateTime.UtcNow.Date;
                    IEnumerable<Order> orders = await _orderService.GetOrdersByDateRangeAsync(tenantId, today, today.AddDays(1));
                    return Ok(orders);
                }
                else
                {
                    OrderStatusId statusId = new(status);
                    List<Order> orders = await _orderService.GetOrdersByStatusAsync(statusId, tenantId);
                    return Ok(orders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(Guid id)
        {
            try
            {
                Guid tenantId = GetTenantId();
                Order? order = await _orderService.GetOrderByIdAsync(id, tenantId);

                return order == null ? (ActionResult<Order>)NotFound() : (ActionResult<Order>)Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/status")]
        public async Task<ActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                Guid tenantId = GetTenantId();

                // Validate transition using CoreHub service
                Order? currentOrder = await _orderService.GetOrderByIdAsync(id, tenantId);
                if (currentOrder == null)
                {
                    return NotFound();
                }

                OrderStatusId newStatus = new(request.Status);
                bool isValidTransition = await _orderService.IsTransitionValidAsync(currentOrder.Status, newStatus);

                if (!isValidTransition)
                {
                    return BadRequest($"Invalid status transition from {currentOrder.Status} to {request.Status}");
                }

                bool success = await _orderService.UpdateOrderStatusAsync(id, request.Status, tenantId);

                if (!success)
                {
                    return NotFound();
                }

                // Log status change for audit
                _logger.LogInformation("Order {OrderId} status updated to {Status} by {User}",
                    id, request.Status, User.Identity?.Name);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("metrics")]
        public async Task<ActionResult<OrderDashboardData>> GetMetrics()
        {
            try
            {
                Guid tenantId = GetTenantId();
                OrderDashboardData metrics = await _orderService.GetDashboardDataAsync(tenantId);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order metrics");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("summary/{id}")]
        public async Task<ActionResult<CoreHub.Services.OrderSummary>> GetOrderSummary(Guid id)
        {
            try
            {
                Guid tenantId = GetTenantId();
                CoreHub.Services.OrderSummary summary = await _orderService.GetOrderSummaryAsync(id, tenantId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order summary {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/assign")]
        public async Task<ActionResult> AssignOrderToStaff(Guid id, [FromBody] AssignStaffRequest request)
        {
            try
            {
                bool success = await _orderManagementService.AssignOrderToStaffAsync(id, request.StaffId);

                if (!success)
                {
                    return NotFound();
                }

                _logger.LogInformation("Order {OrderId} assigned to staff {StaffId}", id, request.StaffId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning order {OrderId} to staff {StaffId}", id, request.StaffId);
                return StatusCode(500, "Internal server error");
            }
        }

        private Guid GetTenantId()
        {
            string? tenantClaim = User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out Guid tenantId) ? tenantId : Guid.Empty;
        }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class AssignStaffRequest
    {
        public Guid StaffId { get; set; }
    }
}
