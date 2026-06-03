using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IOrderManagementService _orderManagementService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderService orderService, 
            IOrderManagementService orderManagementService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _orderManagementService = orderManagementService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromQuery] string? status = null)
        {
            try
            {
                var tenantId = GetTenantId();
                
                if (string.IsNullOrEmpty(status))
                {
                    var today = DateTime.UtcNow.Date;
                    var orders = await _orderService.GetOrdersByDateRangeAsync(tenantId, today, today.AddDays(1));
                    return Ok(orders);
                }
                else
                {
                    var statusId = new OrderStatusId(status);
                    var orders = await _orderService.GetOrdersByStatusAsync(statusId, tenantId);
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
                var tenantId = GetTenantId();
                var order = await _orderService.GetOrderByIdAsync(id, tenantId);
                
                if (order == null)
                {
                    return NotFound();
                }
                
                return Ok(order);
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
                var tenantId = GetTenantId();
                
                // Validate transition using CoreHub service
                var currentOrder = await _orderService.GetOrderByIdAsync(id, tenantId);
                if (currentOrder == null)
                {
                    return NotFound();
                }
                
                var newStatus = new OrderStatusId(request.Status);
                var isValidTransition = await _orderService.IsTransitionValidAsync(currentOrder.Status, newStatus);
                
                if (!isValidTransition)
                {
                    return BadRequest($"Invalid status transition from {currentOrder.Status} to {request.Status}");
                }
                
                var success = await _orderService.UpdateOrderStatusAsync(id, request.Status, tenantId);
                
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
                var tenantId = GetTenantId();
                var metrics = await _orderService.GetDashboardDataAsync(tenantId);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order metrics");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("summary/{id}")]
        public async Task<ActionResult<VanAn.CoreHub.Services.OrderSummary>> GetOrderSummary(Guid id)
        {
            try
            {
                var tenantId = GetTenantId();
                var summary = await _orderService.GetOrderSummaryAsync(id, tenantId);
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
                var success = await _orderManagementService.AssignOrderToStaffAsync(id, request.StaffId);
                
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
            var tenantClaim = User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
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
