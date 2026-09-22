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
        IChatService chatService,
        ISalesmanService salesmanService,
        IAppInstallAttributionService appInstallAttributionService,
        IWalletService walletService,
        IFraudReviewService fraudReviewService,
        ICommerceModeService commerceModeService,
        IVanAnDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IHubContext<LocationHub> locationHubContext,
        IHubContext<ChatHub> chatHubContext,
        ILogger<CommunityController> logger) : ControllerBase
    {
        private readonly ICommunityOrderService _communityOrderService = communityOrderService;
        private readonly IDeliveryWorkflowService _deliveryWorkflowService = deliveryWorkflowService;
        private readonly IChatService _chatService = chatService;
        private readonly ISalesmanService _salesmanService = salesmanService;
        private readonly IAppInstallAttributionService _appInstallAttributionService = appInstallAttributionService;
        private readonly IWalletService _walletService = walletService;
        private readonly IFraudReviewService _fraudReviewService = fraudReviewService;
        private readonly ICommerceModeService _commerceModeService = commerceModeService;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly IHubContext<LocationHub> _locationHubContext = locationHubContext;
        private readonly IHubContext<ChatHub> _chatHubContext = chatHubContext;
        private readonly ILogger<CommunityController> _logger = logger;

        /// <summary>
        /// GET /api/community/role
        /// Returns the caller's community role (isShipper, isSalesman, isShopOwner).
        /// Used by KhachLink NavMenu to show/hide tabs.
        /// v1.2 (Sprint 6): Added isShopOwner — derived from tenant ownership (User.RoleType == Owner for this customer's tenant).
        /// </summary>
        [HttpGet("role")]
        public async Task<IActionResult> GetMyRole()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null)
                return error!;

            var roles = await _dbContext.CommunityRoles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.CustomerId == customerId.Value && r.IsActive)
                .ToListAsync();

            // v1.2: Check if customer is a shop owner — has Settlement wallet transactions (shop wallet = TenantId as OwnerId)
            // Pragmatic PoC approach: if customer has wallet tx with Type=Settlement, they're a shop owner.
            // (Settlement txs are created for shop in COD flow + advance confirmation flow)
            var customerTenantId = await _dbContext.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.Id == customerId.Value)
                .Select(c => c.TenantId.Value)
                .FirstOrDefaultAsync();

            var isShopOwner = false;
            if (customerTenantId != Guid.Empty)
            {
                isShopOwner = await _dbContext.WalletTransactions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(w => w.OwnerId == customerTenantId
                        && w.Type == WalletTransactionType.Settlement);
            }

            return Ok(new
            {
                isShipper = roles.Any(r => r.RoleType == CommunityRoleType.Shipper),
                isSalesman = roles.Any(r => r.RoleType == CommunityRoleType.Salesman),
                isShopOwner
            });
        }

        /// <summary>
        /// GET /api/community/my-roles
        /// Returns all community roles for the caller (active + inactive).
        /// Used by KhachLink Profile.razor to display role badges.
        /// </summary>
        [HttpGet("my-roles")]
        public async Task<IActionResult> GetMyRoles()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null)
                return error!;

            var roles = await _dbContext.CommunityRoles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.CustomerId == customerId.Value)
                .OrderByDescending(r => r.ActivatedAt)
                .Select(r => new
                {
                    roleType = r.RoleType.ToString(),
                    isActive = r.IsActive,
                    activatedAt = r.ActivatedAt,
                    deactivatedAt = r.DeactivatedAt,
                    salesmanCode = r.SalesmanCode
                })
                .ToListAsync();

            return Ok(roles);
        }

        /// <summary>
        /// GET /api/community/my-fraud-flags
        /// Salesman self-view: returns own fraud flags only.
        /// Used by KhachLink Profile.razor to show fraud flag status.
        /// </summary>
        [HttpGet("my-fraud-flags")]
        public async Task<IActionResult> GetMyFraudFlags()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null)
                return error!;

            var flags = await _fraudReviewService.GetMyFlagsAsync(customerId.Value);
            return Ok(flags);
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
        /// GET /api/community/my-deliveries
        /// Returns the shipper's active delivery tasks (Assigned/PickedUp/OutForDelivery).
        /// Excludes Delivered/Failed/Cancelled.
        /// </summary>
        [HttpGet("my-deliveries")]
        public async Task<IActionResult> GetMyActiveDeliveries()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null)
                return error!;

            var roleCheck = await CheckShipperRoleAsync(customerId.Value);
            if (!roleCheck.IsValid)
                return roleCheck.Error!;

            try
            {
                var activeStatuses = new[]
                {
                    DeliveryTaskStatus.Assigned,
                    DeliveryTaskStatus.PickedUp,
                    DeliveryTaskStatus.OutForDelivery
                };

                var tasks = await _dbContext.DeliveryTasks
                    .Where(t => t.ShipperId == customerId.Value && activeStatuses.Contains(t.Status))
                    .OrderByDescending(t => t.AssignedAt)
                    .Select(t => new
                    {
                        orderId = t.OrderId,
                        deliveryTaskId = t.Id,
                        status = t.Status.ToString(),
                        assignedAt = t.AssignedAt,
                        pickedUpAt = t.PickedUpAt,
                        outForDeliveryAt = t.OutForDeliveryAt
                    })
                    .ToListAsync();

                return Ok(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active deliveries for shipper {ShipperId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
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
        /// D2/D3/D6 (2026-09-17): GET /api/community/orders/{orderId}/tracking
        /// Buyer-accessible tracking snapshot for ONE order the caller owns — replaces the
        /// shipper-only /api/community/nearby-orders call the customer page used to build its map
        /// (which returned 403 for buyers, so the customer map never rendered).
        ///
        /// Auth: X-Customer-Token (logged-in) OR X-Customer-Device-Id (guest).
        /// Authorized for: the order's CustomerId, the order's CustomerDeviceId (guest), or the
        /// assigned shipper. Returns shop / delivery / latest-shipper coordinates + statuses.
        /// </summary>
        [HttpGet("orders/{orderId:guid}/tracking")]
        public async Task<IActionResult> GetOrderTracking(Guid orderId)
        {
            var (identity, _, error) = await ValidateCustomerOrDeviceAsync();
            if (identity == null) return error!;

            try
            {
                var order = await _dbContext.Orders
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                    return NotFound(new { error = "Không tìm thấy đơn hàng." });

                var task = await _dbContext.DeliveryTasks
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(dt => dt.OrderId == orderId && dt.Status != DeliveryTaskStatus.Cancelled)
                    .OrderByDescending(dt => dt.AssignedAt)
                    .FirstOrDefaultAsync();

                bool isCustomer = order.CustomerId == identity.Value;
                bool isDeviceGuest = !isCustomer
                    && Guid.TryParse(order.CustomerDeviceId, out var orderDeviceId)
                    && orderDeviceId == identity.Value;
                bool isShipper = task != null && task.ShipperId == identity.Value;

                if (!isCustomer && !isDeviceGuest && !isShipper)
                    return StatusCode(403, new { error = "Bạn không có quyền xem đơn hàng này." });

                // Shop coordinates: prefer the DeliveryTask snapshot, fall back to tenant settings.
                string shopName = string.Empty;
                double? shopLat = task?.ShopLat;
                double? shopLng = task?.ShopLng;
                if (order.TenantId.Value != Guid.Empty)
                {
                    var tenant = await _dbContext.Tenants
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == order.TenantId);
                    shopName = tenant?.Name ?? string.Empty;
                    if (shopLat == null || shopLat == 0)
                    {
                        shopLat = tenant?.Settings?.Latitude;
                        shopLng = tenant?.Settings?.Longitude;
                    }
                }
                // Normalise "no coordinate" (0) to null — the client must not centre a map on (0,0)
                // (blank ocean) just because a tenant/DeliveryTask has an unset default of 0.
                if (shopLat == 0 || shopLng == 0) { shopLat = null; shopLng = null; }

                // Delivery coordinates: DeliveryTask snapshot first, then the order itself.
                double? deliveryLat = task?.CustomerLat ?? order.DeliveryLat;
                double? deliveryLng = task?.CustomerLng ?? order.DeliveryLng;
                if (deliveryLat == 0 || deliveryLng == 0) { deliveryLat = null; deliveryLng = null; }

                // Latest shipper GPS ping for this delivery task.
                double? shipperLat = null;
                double? shipperLng = null;
                DateTime? locationUpdatedAt = null;
                if (task != null)
                {
                    var ping = await _dbContext.DeliveryTrackings
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(t => t.DeliveryTaskId == task.Id)
                        .OrderByDescending(t => t.RecordedAt)
                        .FirstOrDefaultAsync();
                    if (ping != null)
                    {
                        shipperLat = ping.Latitude;
                        shipperLng = ping.Longitude;
                        locationUpdatedAt = ping.RecordedAt;
                    }
                }

                return Ok(new
                {
                    orderId,
                    orderStatus = order.Status?.Value ?? "pending",
                    deliveryStatus = task?.Status.ToString() ?? "Pending",
                    shipperId = task?.ShipperId,
                    deliveryAddress = order.DeliveryAddress,
                    shopName,
                    shopLat,
                    shopLng,
                    deliveryLat,
                    deliveryLng,
                    shipperLat,
                    shipperLng,
                    locationUpdatedAt,
                    updatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tracking for order {OrderId}", orderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// CC-S3 (Sprint 3): GET /api/community/chat/conversations/{orderId}
        /// Get chat history for the given order. Requires DeliveryTask to exist.
        /// </summary>
        [HttpGet("chat/conversations/{orderId:guid}")]
        public async Task<IActionResult> GetChatHistory(Guid orderId)
        {
            // D6 (2026-09-17): accept a logged-in token OR a guest device id.
            var (identity, isGuest, error) = await ValidateCustomerOrDeviceAsync();
            if (identity == null) return error!;

            try
            {
                // Chat is available for DELIVERY orders with a CustomerId — no DeliveryTask required.
                // GetOrCreateConversationAsync creates conversation with placeholder ShipperId=Guid.Empty
                // if no DeliveryTask exists yet (before shipper accepts).
                var conversation = await _chatService.GetOrCreateConversationAsync(orderId, isGuest ? identity : null);
                if (conversation == null)
                    return NotFound(new { error = "Không tìm thấy đơn hàng hoặc đơn hàng không phải loại giao hàng." });

                // Authorization: caller must be the conversation's customer (or the shipper).
                if (identity.Value != conversation.CustomerId && identity.Value != conversation.ShipperId)
                    return StatusCode(403, new { error = "Bạn không có quyền xem cuộc trò chuyện này." });

                var messages = await _chatService.GetHistoryAsync(orderId);

                return Ok(new
                {
                    conversationId = conversation.Id,
                    orderId,
                    shipperId = conversation.ShipperId,
                    messages = messages.Select(m => new
                    {
                        id = m.Id,
                        senderId = m.SenderId,
                        content = m.Content,
                        sentAt = m.SentAt,
                        isRead = m.IsRead
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat history for order {OrderId}", orderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// CC-S3 (Sprint 3): POST /api/community/chat/messages
        /// Send a chat message. Requires DeliveryTask + sender is ShipperId or CustomerId.
        /// </summary>
        [HttpPost("chat/messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest body)
        {
            // D6 (2026-09-17): accept a logged-in token OR a guest device id.
            var (identity, isGuest, error) = await ValidateCustomerOrDeviceAsync();
            if (identity == null) return error!;

            if (body == null || string.IsNullOrWhiteSpace(body.Content))
                return BadRequest(new { error = "Nội dung tin nhắn không được để trống." });

            if (body.Content.Length > 2000)
                return BadRequest(new { error = "Nội dung tin nhắn không được vượt quá 2000 ký tự." });

            if (body.OrderId == Guid.Empty)
                return BadRequest(new { error = "OrderId không hợp lệ." });

            try
            {
                var message = await _chatService.SendMessageAsync(body.OrderId, identity.Value, body.Content, isGuest ? identity : null);

                if (message == null)
                    return StatusCode(403, new { error = "Không thể gửi tin nhắn. Đơn hàng không tồn tại hoặc không phải loại giao hàng." });

                // Push via SignalR to chat group
                await _chatHubContext.Clients.Group($"chat_{body.OrderId}")
                    .SendAsync("ReceiveMessage", message.Id.ToString(), message.SenderId.ToString(), message.Content, message.SentAt.ToString("O"));

                return Ok(new { messageId = message.Id, sentAt = message.SentAt.ToString("O") });
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, new { error = "Bạn không có quyền gửi tin nhắn trong cuộc trò chuyện này." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message for order {OrderId}", body.OrderId);
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

        // === CC-S4 (Sprint 4): Salesman endpoints ===

        /// <summary>
        /// GET /api/community/commerce-mode
        /// Customer-facing: returns the global commerce mode (Marketplace/Reseller) for UI badge.
        /// Sprint 7 — used by KhachLink to show mode badge + adjust price display.
        /// </summary>
        [HttpGet("commerce-mode")]
        public async Task<IActionResult> GetCommerceMode()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            try
            {
                var settings = await _commerceModeService.GetSettingsAsync();
                return Ok(new
                {
                    globalMode = settings.GlobalMode.ToString(),
                    isReseller = settings.GlobalMode == CommerceMode.Reseller
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting commerce mode for customer {CustomerId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// GET /api/community/nearby-products?lat={lat}&lng={lng}&radiusKm=10
        /// Returns nearby products with commission rate + app-install bonus (for salesman referral).
        /// </summary>
        [HttpGet("nearby-products")]
        public async Task<IActionResult> GetNearbyProducts([FromQuery] double lat, [FromQuery] double lng, [FromQuery] int radiusKm = 10)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var (hasRole, roleError) = await CheckSalesmanRoleAsync(customerId.Value);
            if (!hasRole) return roleError!;

            try
            {
                var products = await _salesmanService.GetNearbyProductsAsync(lat, lng, radiusKm, customerId.Value);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby products for salesman {SalesmanId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// GET /api/community/salesman/qr?productId={productId}&amp;sourceDomain={host}
        /// Returns composite QR code for salesman + product.
        /// sourceDomain = KhachLink origin the salesman is using (window.location.hostname) so the QR
        /// points at the right instance instead of a hardcoded host.
        /// </summary>
        [HttpGet("salesman/qr")]
        public async Task<IActionResult> GetSalesmanQr([FromQuery] Guid productId, [FromQuery] string? sourceDomain = null)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var (hasRole, roleError) = await CheckSalesmanRoleAsync(customerId.Value);
            if (!hasRole) return roleError!;

            if (productId == Guid.Empty)
                return BadRequest(new { error = "ProductId không hợp lệ." });

            try
            {
                var qr = await _salesmanService.GetCompositeSalesmanQrAsync(customerId.Value, productId, sourceDomain);
                if (qr == null)
                    return BadRequest(new { error = "Không tìm thấy cấu hình referral cho sản phẩm này." });

                return Ok(qr);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting salesman QR for {SalesmanId}, product {ProductId}", customerId.Value, productId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// GET /api/community/salesman/commissions
        /// Returns commission summary for the authenticated salesman.
        /// </summary>
        [HttpGet("salesman/commissions")]
        public async Task<IActionResult> GetMyCommissions()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var (hasRole, roleError) = await CheckSalesmanRoleAsync(customerId.Value);
            if (!hasRole) return roleError!;

            try
            {
                var summary = await _salesmanService.GetCommissionsAsync(customerId.Value);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting commissions for salesman {SalesmanId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// Issue #178 ph2: GET /api/community/salesman/products?sourceDomain={host}
        /// "Gian hàng của tôi" — the salesman's configured referral products (product + config +
        /// composite code + qrUrl + live catalog price) + active featured products available to add.
        /// QR composite is deterministic ("{salesmanCode}|{productShortCode}") — no storage needed.
        /// </summary>
        [HttpGet("salesman/products")]
        public async Task<IActionResult> GetSalesmanProducts([FromQuery] string? sourceDomain = null)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var (hasRole, roleError) = await CheckSalesmanRoleAsync(customerId.Value);
            if (!hasRole) return roleError!;

            try
            {
                var store = await _salesmanService.GetSalesmanStoreAsync(customerId.Value, sourceDomain);
                return Ok(store);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting salesman store for {SalesmanId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// Issue #178 ph2: POST /api/community/salesman/products/add
        /// Add an active featured product to the salesman's store (creates ProductReferralConfig
        /// with safe defaults 0.01 / 1000 VND — owner can adjust via admin UI).
        /// </summary>
        [HttpPost("salesman/products/add")]
        public async Task<IActionResult> AddSalesmanProduct([FromBody] AddSalesmanProductRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var (hasRole, roleError) = await CheckSalesmanRoleAsync(customerId.Value);
            if (!hasRole) return roleError!;

            if (body == null || body.ProductId == Guid.Empty)
                return BadRequest(new { error = "ProductId không hợp lệ." });

            try
            {
                var product = await _salesmanService.AddProductToStoreAsync(customerId.Value, body.ProductId);
                if (product == null)
                    return BadRequest(new { error = "Sản phẩm không tồn tại hoặc đã có cấu hình referral." });
                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding product {ProductId} to salesman store for {SalesmanId}", body.ProductId, customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// Issue #178 ph2: POST /api/community/salesman/products/remove
        /// Remove a product from the salesman's store (soft — DeactivateAsync).
        /// </summary>
        [HttpPost("salesman/products/remove")]
        public async Task<IActionResult> RemoveSalesmanProduct([FromBody] RemoveSalesmanProductRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var (hasRole, roleError) = await CheckSalesmanRoleAsync(customerId.Value);
            if (!hasRole) return roleError!;

            if (body == null || body.ProductId == Guid.Empty)
                return BadRequest(new { error = "ProductId không hợp lệ." });

            try
            {
                var removed = await _salesmanService.RemoveProductFromStoreAsync(customerId.Value, body.ProductId);
                if (!removed)
                    return NotFound(new { error = "Không tìm thấy cấu hình referral cho sản phẩm này." });
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing product {ProductId} from salesman store for {SalesmanId}", body.ProductId, customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/app-install/attributed
        /// Attribute an app install to a salesman via composite referral code.
        /// </summary>
        [HttpPost("app-install/attributed")]
        public async Task<IActionResult> AttributeInstall([FromBody] AttributeInstallRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || string.IsNullOrWhiteSpace(body.ReferralCode))
                return BadRequest(new { error = "ReferralCode không được để trống." });

            try
            {
                var result = await _appInstallAttributionService.AttributeInstallAsync(
                    customerId.Value, body.ReferralCode,
                    body.FingerprintHash, body.FingerprintSignals, body.DeviceToken);

                if (result == null)
                    return BadRequest(new { error = "Mã referral không hợp lệ." });

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attributing install for customer {CustomerId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/resolve-referral
        /// Resolve a composite referral code to (salesmanId, productId). Used by checkout to set Order.SalesmanId.
        /// </summary>
        [HttpPost("resolve-referral")]
        public async Task<IActionResult> ResolveReferral([FromBody] ResolveReferralRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || string.IsNullOrWhiteSpace(body.ReferralCode))
                return BadRequest(new { error = "ReferralCode không được để trống." });

            try
            {
                var result = await _salesmanService.ResolveCompositeReferralCodeAsync(body.ReferralCode);
                if (result == null)
                    return NotFound(new { error = "Mã referral không hợp lệ." });

                return Ok(new { salesmanId = result.Value.salesmanId, productId = result.Value.productId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving referral code {Code}", body.ReferralCode);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// GET /api/community/referral/{code}
        /// Anonymous: resolve a scanned composite referral code "{salesmanCode}|{productShortCode}"
        /// into the referred product's display info so the customer can add it to the cart and buy it.
        /// Mirrors the product-QR scan flow. The code is printed on a public QR, so no auth is needed
        /// (the order itself still requires a customer token / checkout).
        /// </summary>
        [HttpGet("referral/{code}")]
        [AllowAnonymous]
        public async Task<IActionResult> ResolveReferralForScan(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest(new { error = "Mã referral không được để trống." });

            try
            {
                // Query-string decoding: the QR carries %7C for '|'; [FromRoute] already decodes,
                // but a caller may pass a still-encoded value — normalise defensively.
                var referralCode = code.Contains('%') ? Uri.UnescapeDataString(code) : code;

                var result = await _salesmanService.ResolveReferralForScanAsync(referralCode);
                if (result == null)
                    return NotFound(new { error = "Mã referral không hợp lệ hoặc sản phẩm không còn bán." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving referral code for scan {Code}", code);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        // ============================================================
        // CC-S5 (Sprint 5): Wallet + COD + Settlement endpoints
        // ============================================================

        /// <summary>
        /// GET /api/community/wallet
        /// Returns wallet balance + transaction history for the authenticated customer (shipper/salesman).
        /// Auth: X-Customer-Token.
        /// </summary>
        [HttpGet("wallet")]
        public async Task<IActionResult> GetWallet()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            try
            {
                var wallet = await _walletService.GetWalletAsync(customerId.Value);
                return Ok(wallet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wallet for customer {CustomerId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/wallet/confirm-cod
        /// Shipper confirms COD collection for an order.
        /// Creates WalletTransaction(CODCollection) for shipper + WalletTransaction(Settlement) for shop.
        /// Auth: X-Customer-Token (must be shipper of the order's DeliveryTask).
        /// </summary>
        [HttpPost("wallet/confirm-cod")]
        public async Task<IActionResult> ConfirmCod([FromBody] ConfirmCodRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || body.OrderId == Guid.Empty)
                return BadRequest(new { error = "OrderId không hợp lệ." });

            if (body.Amount <= 0)
                return BadRequest(new { error = "Amount phải lớn hơn 0." });

            try
            {
                var tx = await _walletService.ConfirmCodAsync(customerId.Value, body.OrderId, body.Amount);
                return Ok(new { transactionId = tx.Id, balanceAfter = tx.BalanceAfter });
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, new { error = "Bạn không phải là shipper của đơn hàng này." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming COD for order {OrderId}", body.OrderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/wallet/confirm-advance
        /// Shipper confirms advance payment to shop (paid cash before pickup).
        /// Creates WalletTransaction(AdvancePayment) for shipper. Pending shop confirmation.
        /// Auth: X-Customer-Token (must be shipper of the order's DeliveryTask).
        /// </summary>
        [HttpPost("wallet/confirm-advance")]
        public async Task<IActionResult> ConfirmAdvance([FromBody] ConfirmAdvanceRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || body.OrderId == Guid.Empty)
                return BadRequest(new { error = "OrderId không hợp lệ." });

            if (body.Amount <= 0)
                return BadRequest(new { error = "Amount phải lớn hơn 0." });

            try
            {
                var tx = await _walletService.ConfirmAdvanceAsync(customerId.Value, body.OrderId, body.Amount);
                return Ok(new { transactionId = tx.Id, balanceAfter = tx.BalanceAfter });
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, new { error = "Bạn không phải là shipper của đơn hàng này." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming advance for order {OrderId}", body.OrderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// GET /api/community/wallet/pending-advances
        /// Shop owner lists pending advance payments awaiting confirmation.
        /// Auth: X-Customer-Token (shop owner — uses TenantId as shopOwnerId).
        /// </summary>
        [HttpGet("wallet/pending-advances")]
        public async Task<IActionResult> GetPendingAdvances()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            try
            {
                // Shop owner ID = TenantId of the customer's tenant
                var customer = await _dbContext.Customers
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == customerId.Value);

                if (customer == null)
                    return NotFound(new { error = "Không tìm thấy khách hàng." });

                var shopOwnerId = customer.TenantId.Value;
                var pending = await _walletService.GetPendingAdvancesAsync(shopOwnerId);
                return Ok(pending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending advances for customer {CustomerId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/wallet/confirm-advance-received
        /// Shop owner confirms they received advance payment from shipper.
        /// Creates WalletTransaction(Settlement) for shop, linked to original AdvancePayment.
        /// Auth: X-Customer-Token (shop owner).
        /// </summary>
        [HttpPost("wallet/confirm-advance-received")]
        public async Task<IActionResult> ConfirmAdvanceReceived([FromBody] ConfirmAdvanceReceivedRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || body.AdvanceTransactionId == Guid.Empty)
                return BadRequest(new { error = "AdvanceTransactionId không hợp lệ." });

            try
            {
                // Shop owner ID = TenantId of the customer's tenant
                var customer = await _dbContext.Customers
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == customerId.Value);

                if (customer == null)
                    return NotFound(new { error = "Không tìm thấy khách hàng." });

                var shopOwnerId = customer.TenantId.Value;
                var tx = await _walletService.ConfirmAdvanceReceivedAsync(shopOwnerId, body.AdvanceTransactionId);
                return Ok(new { transactionId = tx.Id, balanceAfter = tx.BalanceAfter });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming advance received {AdvanceTxId}", body.AdvanceTransactionId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/wallet/remit
        /// Settlement Batch-3 (TC-08): Shipper nộp tiền COD đã thu hộ cho một đơn.
        /// Marketplace: Remittance(-cod, shipper) + Settlement(+cod, shop).
        /// Reseller: Remittance(-cod, shipper) + Settlement(+cod, PlatformWallet).
        /// Amount is derived server-side — client only supplies the order.
        /// Auth: X-Customer-Token (must be shipper of the order's DeliveryTask).
        /// </summary>
        [HttpPost("wallet/remit")]
        public async Task<IActionResult> RemitCod([FromBody] RemitCodRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || body.OrderId == Guid.Empty)
                return BadRequest(new { error = "OrderId không hợp lệ." });

            try
            {
                var tx = await _walletService.RemitCodAsync(customerId.Value, body.OrderId);
                return Ok(new { transactionId = tx.Id, balanceAfter = tx.BalanceAfter });
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, new { error = "Bạn không phải là shipper của đơn hàng này." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error remitting COD for order {OrderId}", body.OrderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// GET /api/community/wallet/pending-remittances
        /// TC-08: COD the shipper collected but has not yet remitted ("đang giữ hộ").
        /// Auth: X-Customer-Token.
        /// </summary>
        [HttpGet("wallet/pending-remittances")]
        public async Task<IActionResult> GetPendingRemittances()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            try
            {
                var pending = await _walletService.GetPendingRemittancesAsync(customerId.Value);
                return Ok(pending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending remittances for customer {CustomerId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/wallet/withdraw
        /// Settlement Batch-3 (TC-09): owner requests a payout (Pending → admin approves/pays).
        /// Min 500.000đ; blocked while another request is Pending/Approved.
        /// Auth: X-Customer-Token.
        /// </summary>
        [HttpPost("wallet/withdraw")]
        public async Task<IActionResult> RequestWithdrawal([FromBody] WithdrawalRequestBody body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || body.Amount <= 0)
                return BadRequest(new { error = "Amount phải lớn hơn 0." });

            try
            {
                var request = await _walletService.RequestWithdrawalAsync(customerId.Value, body.Amount);
                return Ok(new { requestId = request.Id, status = request.Status.ToString() });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating withdrawal request for customer {CustomerId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// GET /api/community/wallet/withdrawals
        /// TC-09: owner's own withdrawal request history.
        /// Auth: X-Customer-Token.
        /// </summary>
        [HttpGet("wallet/withdrawals")]
        public async Task<IActionResult> GetWithdrawals()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            try
            {
                var withdrawals = await _walletService.GetWithdrawalsAsync(customerId.Value);
                return Ok(withdrawals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting withdrawals for customer {CustomerId}", customerId.Value);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// POST /api/community/wallet/withdrawals/{id}/cancel
        /// TC-09: owner cancels their own Pending withdrawal request.
        /// Auth: X-Customer-Token.
        /// </summary>
        [HttpPost("wallet/withdrawals/{id:guid}/cancel")]
        public async Task<IActionResult> CancelWithdrawal(Guid id)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            try
            {
                await _walletService.CancelWithdrawalAsync(customerId.Value, id);
                return Ok(new { requestId = id, status = "Cancelled" });
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, new { error = "Yêu cầu rút tiền này không thuộc về bạn." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling withdrawal {RequestId}", id);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        // Sprint 7 Q5: confirm-external-payment endpoint REMOVED from this controller.
        // Reason: [AllowAnonymous] at class level bypasses [Authorize] at method level in ASP.NET Core,
        // creating an auth bypass on a financial endpoint (5-split wallet transaction).
        // Canonical endpoint: POST /api/admin/commerce-mode/confirm-external-payment (CommerceModeController,
        // which has proper class-level [Authorize(Policy = "SystemAdmin")]).

        /// <summary>
        /// Check if customer has Salesman role (Active). Queries Gateway PG CommunityRoles table.
        /// </summary>
        private async Task<(bool IsValid, IActionResult? Error)> CheckSalesmanRoleAsync(Guid customerId)
        {
            var salesmanRole = await _dbContext.CommunityRoles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CustomerId == customerId
                    && r.RoleType == CommunityRoleType.Salesman
                    && r.IsActive);

            if (salesmanRole == null)
                return (false, StatusCode(403, new { error = "Bạn không có quyền Salesman." }));

            return (true, null);
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
        /// D6 (2026-09-17): resolve the caller identity from either
        /// <c>X-Customer-Token</c> (logged-in customer, validated via ShopERP /me) or
        /// <c>X-Customer-Device-Id</c> (guest — the localStorage <c>customer_device_id</c> GUID).
        /// Guest identity = the device id, matched against <c>Order.CustomerDeviceId</c> downstream.
        /// </summary>
        private async Task<(Guid? Identity, bool IsGuest, IActionResult? Error)> ValidateCustomerOrDeviceAsync()
        {
            bool hasToken = Request.Headers.TryGetValue("X-Customer-Token", out var token)
                && !string.IsNullOrEmpty(token.ToString());

            if (hasToken)
            {
                var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
                return customerId == null ? (null, false, error) : (customerId, false, null);
            }

            if (Request.Headers.TryGetValue("X-Customer-Device-Id", out var deviceHeader)
                && Guid.TryParse(deviceHeader.ToString(), out var deviceId)
                && deviceId != Guid.Empty)
            {
                return (deviceId, true, null);
            }

            return (null, false, Unauthorized(new { error = "Cần X-Customer-Token hoặc X-Customer-Device-Id." }));
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

        public class SendMessageRequest
        {
            public Guid OrderId { get; set; }
            public string Content { get; set; } = string.Empty;
        }

        public class AttributeInstallRequest
        {
            public string ReferralCode { get; set; } = string.Empty;
            public string? FingerprintHash { get; set; }
            public string? FingerprintSignals { get; set; }
            public string? DeviceToken { get; set; }
        }

        /// <summary>Issue #178 ph2: add product to salesman store.</summary>
        public class AddSalesmanProductRequest
        {
            public Guid ProductId { get; set; }
        }

        /// <summary>Issue #178 ph2: remove product from salesman store.</summary>
        public class RemoveSalesmanProductRequest
        {
            public Guid ProductId { get; set; }
        }

        public class ResolveReferralRequest
        {
            public string ReferralCode { get; set; } = string.Empty;
        }

        // CC-S5 (Sprint 5): Wallet request DTOs
        public class ConfirmCodRequest
        {
            public Guid OrderId { get; set; }
            public decimal Amount { get; set; }
        }

        public class ConfirmAdvanceRequest
        {
            public Guid OrderId { get; set; }
            public decimal Amount { get; set; }
        }

        public class ConfirmAdvanceReceivedRequest
        {
            public Guid AdvanceTransactionId { get; set; }
        }

        // Settlement Batch-3 DTOs
        public class RemitCodRequest
        {
            public Guid OrderId { get; set; }
        }

        public class WithdrawalRequestBody
        {
            public decimal Amount { get; set; }
        }
    }
}
