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
        /// CC-S3 (Sprint 3): GET /api/community/chat/conversations/{orderId}
        /// Get chat history for the given order. Requires DeliveryTask to exist.
        /// </summary>
        [HttpGet("chat/conversations/{orderId:guid}")]
        public async Task<IActionResult> GetChatHistory(Guid orderId)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            try
            {
                if (!await _chatService.HasActiveDeliveryTaskAsync(orderId))
                    return StatusCode(403, new { error = "Không có đơn giao nào cho đơn hàng này." });

                var messages = await _chatService.GetHistoryAsync(orderId);
                var conversation = await _chatService.GetOrCreateConversationAsync(orderId);

                return Ok(new
                {
                    conversationId = conversation?.Id,
                    orderId,
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
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || string.IsNullOrWhiteSpace(body.Content))
                return BadRequest(new { error = "Nội dung tin nhắn không được để trống." });

            if (body.Content.Length > 2000)
                return BadRequest(new { error = "Nội dung tin nhắn không được vượt quá 2000 ký tự." });

            if (body.OrderId == Guid.Empty)
                return BadRequest(new { error = "OrderId không hợp lệ." });

            try
            {
                var message = await _chatService.SendMessageAsync(body.OrderId, customerId.Value, body.Content);

                if (message == null)
                    return StatusCode(403, new { error = "Không thể gửi tin nhắn. Đơn giao không tồn tại." });

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
        /// GET /api/community/salesman/qr?productId={productId}
        /// Returns composite QR code for salesman + product.
        /// </summary>
        [HttpGet("salesman/qr")]
        public async Task<IActionResult> GetSalesmanQr([FromQuery] Guid productId)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var (hasRole, roleError) = await CheckSalesmanRoleAsync(customerId.Value);
            if (!hasRole) return roleError!;

            if (productId == Guid.Empty)
                return BadRequest(new { error = "ProductId không hợp lệ." });

            try
            {
                var qr = await _salesmanService.GetCompositeSalesmanQrAsync(customerId.Value, productId);
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
        /// Sprint 7 Q5: POST /api/community/wallet/confirm-external-payment
        /// Vạn An confirms external payment (non-COD Reseller — VietQR/card) for an order.
        /// Creates 5-split: ExternalPayment + Settlement + DeliveryFee + Commission? + PlatformFee + CommunityFund.
        /// Auth: SystemAdmin Bearer JWT (Vạn An staff confirms after VietQR webhook or manual check).
        /// NOTE: Endpoint moved to CommerceModeController (this controller has [AllowAnonymous] at class level
        /// which would bypass the [Authorize] attribute — CommerceModeController has proper class-level auth).
        /// </summary>
        [HttpPost("wallet/confirm-external-payment")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SystemAdmin", AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ConfirmExternalPayment([FromBody] ConfirmExternalPaymentRequest body)
        {
            // Delegate to CommerceModeController logic via WalletService
            // This endpoint is kept for backward compat but auth is enforced by [Authorize] above.
            // However, due to [AllowAnonymous] at class level, this may not enforce auth properly.
            // Use /api/admin/commerce-mode/confirm-external-payment instead (CommerceModeController).
            return await ConfirmExternalPaymentImpl(body);
        }

        private async Task<IActionResult> ConfirmExternalPaymentImpl(ConfirmExternalPaymentRequest body)
        {
            if (body == null || body.OrderId == Guid.Empty)
                return BadRequest(new { error = "OrderId không hợp lệ." });
            if (body.Amount <= 0)
                return BadRequest(new { error = "Amount phải lớn hơn 0." });
            if (string.IsNullOrWhiteSpace(body.PaymentRef))
                return BadRequest(new { error = "PaymentRef không được để trống." });

            try
            {
                var tx = await _walletService.ConfirmExternalPaymentAsync(body.OrderId, body.Amount, body.PaymentRef);
                return Ok(new { transactionId = tx.Id, balanceAfter = tx.BalanceAfter });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming external payment for order {OrderId}", body.OrderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

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

        // Sprint 7 Q5: External payment confirmation (non-COD Reseller)
        public class ConfirmExternalPaymentRequest
        {
            public Guid OrderId { get; set; }
            public decimal Amount { get; set; }
            public string PaymentRef { get; set; } = string.Empty; // VietQR txn ref / card txn id
        }
    }
}
