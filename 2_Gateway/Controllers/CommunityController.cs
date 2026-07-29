using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Hubs;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// CC-S1-T1/T2 (Sprint 1): Community Commerce endpoints for shipper flow.
    /// GET /api/community/nearby-orders — list DELIVERY orders within radius (Haversine).
    /// POST /api/community/orders/{orderId}/accept — accept order for delivery (concurrency-safe).
    ///
    /// CC-S2 (Sprint 2): Delivery workflow + GPS tracking.
    /// POST /api/community/orders/{orderId}/pickup — mark as picked up.
    /// POST /api/community/orders/{orderId}/delivering — mark as out for delivery.
    /// POST /api/community/orders/{orderId}/delivered — mark as delivered (+ Order.Completed).
    /// POST /api/community/orders/{orderId}/failed — mark as failed (with reason).
    /// POST /api/community/location/update — record GPS location ping + SignalR push.
    ///
    /// Auth: X-Customer-Token header (validated via ShopERP /me forward).
    /// Role check: CommunityRole(Shipper, Active) — queried from Gateway PG.
    /// Gateway-native (uses IVanAnDbContext + ICommunityOrderService — both registered in Gateway DI).
    /// </summary>
    [ApiController]
    [Route("api/community")]
    [AllowAnonymous]
    public class CommunityController(
        ICommunityOrderService communityOrderService,
        IDeliveryWorkflowService deliveryWorkflowService,
        IVanAnDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IHubContext<LocationHub> locationHubContext,
        ILogger<CommunityController> logger) : ControllerBase
    {
        private readonly ICommunityOrderService _communityOrderService = communityOrderService;
        private readonly IDeliveryWorkflowService _deliveryWorkflowService = deliveryWorkflowService;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly IHubContext<LocationHub> _locationHubContext = locationHubContext;
        private readonly ILogger<CommunityController> _logger = logger;

        /// <summary>
        /// GET /api/community/role
        /// Returns the caller's community role (isShipper). Used by KhachLink NavMenu to show/hide shipper tab.
        /// </summary>
        [HttpGet("role")]
        public async Task<IActionResult> GetMyRole()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null)
                return error!;

            var shipperRole = await _dbContext.CommunityRoles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CustomerId == customerId.Value
                    && r.RoleType == CommunityRoleType.Shipper
                    && r.IsActive);

            return Ok(new { isShipper = shipperRole != null });
        }

        /// <summary>
        /// GET /api/community/nearby-orders?lat={lat}&lng={lng}&radiusKm=5
        /// Returns DELIVERY orders within radius, sorted by distance.
        /// </summary>
        [HttpGet("nearby-orders")]
        public async Task<IActionResult> GetNearbyOrders(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] int radiusKm = 5)
        {
            // 1. Validate X-Customer-Token + get CustomerId
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null)
                return error!;

            // 2. Check Shipper role
            var roleCheck = await CheckShipperRoleAsync(customerId.Value);
            if (!roleCheck.IsValid)
                return roleCheck.Error!;

            // 3. Validate coordinates
            if (lat == 0 && lng == 0)
                return BadRequest(new { error = "Tọa độ không hợp lệ. Vui lòng bật GPS." });

            if (radiusKm < 1 || radiusKm > 50)
                return BadRequest(new { error = "Bán kính phải từ 1-50km." });

            // 4. Get nearby orders
            try
            {
                var orders = await _communityOrderService.GetNearbyOrdersAsync(lat, lng, radiusKm, customerId.Value);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby orders for shipper {ShipperId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server. Vui lòng thử lại." });
            }
        }

        /// <summary>
        /// POST /api/community/orders/{orderId}/accept
        /// Accept an order for delivery. Creates DeliveryTask + sets Order.ShipperId.
        /// Returns 409 if already assigned or not in accept-able status.
        /// </summary>
        [HttpPost("orders/{orderId:guid}/accept")]
        public async Task<IActionResult> AcceptOrder(Guid orderId)
        {
            // 1. Validate X-Customer-Token + get CustomerId
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null)
                return error!;

            // 2. Check Shipper role
            var roleCheck = await CheckShipperRoleAsync(customerId.Value);
            if (!roleCheck.IsValid)
                return roleCheck.Error!;

            // 3. Accept order
            try
            {
                var deliveryTask = await _communityOrderService.AcceptOrderAsync(orderId, customerId.Value);

                if (deliveryTask == null)
                    return Conflict(new { error = "Đơn hàng đã được nhận hoặc không thể nhận lúc này." });

                return Ok(new
                {
                    deliveryTaskId = deliveryTask.Id,
                    orderId = deliveryTask.OrderId,
                    status = deliveryTask.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting order {OrderId} for shipper {ShipperId}", orderId, customerId.Value);
                return StatusCode(500, new { error = "Lỗi server. Vui lòng thử lại." });
            }
        }

        /// <summary>
        /// POST /api/community/orders/{orderId}/pickup
        /// Mark the active DeliveryTask as PickedUp.
        /// </summary>
        [HttpPost("orders/{orderId:guid}/pickup")]
        public async Task<IActionResult> PickupOrder(Guid orderId)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var roleCheck = await CheckShipperRoleAsync(customerId.Value);
            if (!roleCheck.IsValid) return roleCheck.Error!;

            try
            {
                var task = await _deliveryWorkflowService.TransitionStatusAsync(orderId, DeliveryTaskStatus.PickedUp);
                if (task == null)
                    return NotFound(new { error = "Không tìm thấy đơn giao đang hoạt động." });

                await PublishDeliveryStatusUpdateAsync(orderId, task.Status.ToString());
                return Ok(new { deliveryTaskId = task.Id, status = task.Status.ToString(), timestamp = task.PickedUpAt });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error picking up order {OrderId}", orderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/orders/{orderId}/delivering
        /// Mark the active DeliveryTask as OutForDelivery.
        /// </summary>
        [HttpPost("orders/{orderId:guid}/delivering")]
        public async Task<IActionResult> StartDelivering(Guid orderId)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var roleCheck = await CheckShipperRoleAsync(customerId.Value);
            if (!roleCheck.IsValid) return roleCheck.Error!;

            try
            {
                var task = await _deliveryWorkflowService.TransitionStatusAsync(orderId, DeliveryTaskStatus.OutForDelivery);
                if (task == null)
                    return NotFound(new { error = "Không tìm thấy đơn giao đang hoạt động." });

                await PublishDeliveryStatusUpdateAsync(orderId, task.Status.ToString());
                return Ok(new { deliveryTaskId = task.Id, status = task.Status.ToString(), timestamp = task.OutForDeliveryAt });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting delivery for order {OrderId}", orderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/orders/{orderId}/delivered
        /// Mark the active DeliveryTask as Delivered + Order → completed.
        /// </summary>
        [HttpPost("orders/{orderId:guid}/delivered")]
        public async Task<IActionResult> CompleteDelivery(Guid orderId)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var roleCheck = await CheckShipperRoleAsync(customerId.Value);
            if (!roleCheck.IsValid) return roleCheck.Error!;

            try
            {
                var task = await _deliveryWorkflowService.TransitionStatusAsync(orderId, DeliveryTaskStatus.Delivered);
                if (task == null)
                    return NotFound(new { error = "Không tìm thấy đơn giao đang hoạt động." });

                await PublishDeliveryStatusUpdateAsync(orderId, task.Status.ToString());
                return Ok(new { deliveryTaskId = task.Id, status = task.Status.ToString(), timestamp = task.DeliveredAt });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing delivery for order {OrderId}", orderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/orders/{orderId}/failed
        /// Mark the active DeliveryTask as Failed with reason.
        /// </summary>
        [HttpPost("orders/{orderId:guid}/failed")]
        public async Task<IActionResult> FailDelivery(Guid orderId, [FromBody] FailureRequest? body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var roleCheck = await CheckShipperRoleAsync(customerId.Value);
            if (!roleCheck.IsValid) return roleCheck.Error!;

            try
            {
                var task = await _deliveryWorkflowService.TransitionStatusAsync(orderId, DeliveryTaskStatus.Failed, body?.Reason);
                if (task == null)
                    return NotFound(new { error = "Không tìm thấy đơn giao đang hoạt động." });

                await PublishDeliveryStatusUpdateAsync(orderId, task.Status.ToString());
                return Ok(new { deliveryTaskId = task.Id, status = task.Status.ToString(), reason = task.FailureReason, timestamp = task.FailedAt });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error failing delivery for order {OrderId}", orderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/location/update
        /// Record a GPS location ping for the DeliveryTask + push via SignalR to order group.
        /// </summary>
        [HttpPost("location/update")]
        public async Task<IActionResult> UpdateLocation([FromBody] LocationUpdateRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var roleCheck = await CheckShipperRoleAsync(customerId.Value);
            if (!roleCheck.IsValid) return roleCheck.Error!;

            if (body == null || string.IsNullOrEmpty(body.DeliveryTaskId))
                return BadRequest(new { error = "deliveryTaskId is required." });

            if (!Guid.TryParse(body.DeliveryTaskId, out var taskGuid))
                return BadRequest(new { error = "deliveryTaskId không hợp lệ." });

            try
            {
                await _deliveryWorkflowService.RecordLocationAsync(taskGuid, body.Lat, body.Lng);

                // Push location update via SignalR to the order group
                // Find the orderId for this deliveryTask
                var task = await _dbContext.DeliveryTasks
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(dt => dt.Id == taskGuid);

                if (task != null)
                {
                    var recordedAt = DateTime.UtcNow.ToString("O");
                    await _locationHubContext.Clients.Group($"order_{task.OrderId}")
                        .SendAsync("LocationUpdate", taskGuid.ToString(), body.Lat, body.Lng, recordedAt);
                }

                return Ok(new { recordedAt = DateTime.UtcNow.ToString("O") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording location for task {TaskId}", body.DeliveryTaskId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// Push delivery status update via SignalR to the order group.
        /// </summary>
        private async Task PublishDeliveryStatusUpdateAsync(Guid orderId, string status)
        {
            var timestamp = DateTime.UtcNow.ToString("O");
            await _locationHubContext.Clients.Group($"order_{orderId}")
                .SendAsync("DeliveryStatusUpdate", orderId.ToString(), status, timestamp);
        }

        /// <summary>
        /// Validate X-Customer-Token by forwarding to ShopERP /api/customer-identity/me.
        /// Returns CustomerId if valid, or an IActionResult error if invalid.
        /// </summary>
        private async Task<(Guid? CustomerId, IActionResult? Error)> ValidateTokenAndGetCustomerIdAsync()
        {
            if (!Request.Headers.TryGetValue("X-Customer-Token", out var token) || string.IsNullOrEmpty(token))
                return (null, Unauthorized(new { error = "X-Customer-Token header is required." }));

            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/customer-identity/me");
                meReq.Headers.Add("X-Customer-Token", token.ToString());

                var meResp = await client.SendAsync(meReq);
                if (!meResp.IsSuccessStatusCode)
                    return (null, Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." }));

                var meContent = await meResp.Content.ReadFromJsonAsync<MeResponse>();
                if (meContent?.CustomerId == null || meContent.CustomerId == Guid.Empty)
                    return (null, Unauthorized(new { error = "Không tìm thấy khách hàng." }));

                return (meContent.CustomerId.Value, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating customer token for community endpoint");
                return (null, StatusCode(500, new { error = "Lỗi xác thực token." }));
            }
        }

        /// <summary>
        /// Check if customer has Shipper role (Active). Queries Gateway PG CommunityRoles table.
        /// </summary>
        private async Task<(bool IsValid, IActionResult? Error)> CheckShipperRoleAsync(Guid customerId)
        {
            var shipperRole = await _dbContext.CommunityRoles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CustomerId == customerId
                    && r.RoleType == CommunityRoleType.Shipper
                    && r.IsActive);

            if (shipperRole == null)
                return (false, StatusCode(403, new { error = "Bạn không có quyền Shipper." }));

            return (true, null);
        }

        private class MeResponse
        {
            public Guid? CustomerId { get; set; }
        }

        public class FailureRequest
        {
            public string? Reason { get; set; }
        }

        public class LocationUpdateRequest
        {
            public string DeliveryTaskId { get; set; } = string.Empty;
            public double Lat { get; set; }
            public double Lng { get; set; }
        }
    }
}
