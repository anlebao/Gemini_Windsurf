using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// CC-S1-T1/T2 (Sprint 1): Community Commerce endpoints for shipper flow.
    /// GET /api/community/nearby-orders — list DELIVERY orders within radius (Haversine).
    /// POST /api/community/orders/{orderId}/accept — accept order for delivery (concurrency-safe).
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
        IVanAnDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<CommunityController> logger) : ControllerBase
    {
        private readonly ICommunityOrderService _communityOrderService = communityOrderService;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
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
    }
}
