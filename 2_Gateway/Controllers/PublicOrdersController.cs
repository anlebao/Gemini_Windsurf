using Microsoft.AspNetCore.Authorization;
using VanAn.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Commands;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.DTOs;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/public/orders")]
    [AllowAnonymous]
    public class PublicOrdersController(
        IOrderService orderService,
        ISocialCampaignService socialCampaignService,
        ITenantProvider tenantProvider,
        IVanAnDbContext? dbContext,
        ILogger<PublicOrdersController> logger) : ControllerBase
    {
        private readonly IOrderService _orderService = orderService;
        private readonly ISocialCampaignService _socialCampaignService = socialCampaignService;
        private readonly ITenantProvider _tenantProvider = tenantProvider;
        private readonly IVanAnDbContext? _dbContext = dbContext;
        private readonly ILogger<PublicOrdersController> _logger = logger;

        [HttpPost]
        public async Task<ActionResult<Order>> CreateGuestOrder([FromBody] GuestOrderRequest request)
        {
            try
            {
                if (request == null
                    || string.IsNullOrWhiteSpace(request.TrackingCode)
                    || request.ProductId == Guid.Empty
                    || request.Quantity <= 0)
                {
                    return BadRequest(new { error = "Invalid order request" });
                }

                SocialCampaign? campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(request.TrackingCode);
                if (campaign == null)
                {
                    return BadRequest(new { error = "Campaign not found" });
                }

                Guid customerDeviceId = Guid.TryParse(request.CustomerDeviceId, out Guid parsedId)
                    ? parsedId
                    : Guid.NewGuid();

                var command = new CreateOrderCommand
                {
                    CustomerDeviceId = customerDeviceId,
                    Items =
                    [
                        new CoreHub.Commands.OrderItemRequest
                        {
                            ProductId = request.ProductId,
                            Quantity = request.Quantity,
                            UnitPrice = request.UnitPrice
                        }
                    ]
                };

                Order createdOrder = await _orderService.CreateOrderFromCommandAsync(command, campaign.TenantId.Value);

                _logger.LogInformation(
                    "Guest order {OrderId} created for campaign {TrackingCode} for tenant {TenantId}",
                    createdOrder.Id,
                    request.TrackingCode,
                    campaign.TenantId.Value);

                return Ok(createdOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating guest order for campaign {TrackingCode}", request?.TrackingCode);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Phase 3 (Multi-VPS Checkout): Create order(s) from KhachLink checkout flow.
        /// No auth required — KhachLink is a customer-facing app.
        ///
        /// Multi-tenant grouping: if cart has items from 2 tenants, creates 2 separate orders.
        /// Client provides ProductName + VatRate snapshot — Gateway does NOT query Products table.
        /// Each order's Outbox event is routed to the correct ShopERP via ShopInstanceId routing key.
        /// </summary>
        [HttpPost("checkout")]
        public async Task<ActionResult<CheckoutResponse>> CreateCheckoutOrder([FromBody] CheckoutOrderRequest request)
        {
            try
            {
                if (request == null || request.Items == null || request.Items.Count == 0)
                {
                    return BadRequest(new { error = "Invalid checkout order request — items required" });
                }

                // Validate: each item must have TenantId + ProductName (Phase 3 client snapshot)
                var invalidItems = request.Items
                    .Where(i => i.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(i.ProductName))
                    .ToList();
                if (invalidItems.Count > 0)
                {
                    return BadRequest(new
                    {
                        error = "Each checkout item must include TenantId and ProductName (Phase 3 client snapshot). " +
                                "Items missing these fields will trigger a broken product lookup."
                    });
                }

                // TIER 0: Sanity checks — reject obviously invalid prices/quantities instantly.
                // Protects against client bugs, DevTools manipulation, and corrupted cached data.
                var sanityFailures = new List<string>();
                foreach (var item in request.Items)
                {
                    if (item.UnitPrice <= 0)
                        sanityFailures.Add($"Sản phẩm '{item.ProductName}': giá không hợp lệ (UnitPrice={item.UnitPrice}).");
                    if (item.Quantity <= 0)
                        sanityFailures.Add($"Sản phẩm '{item.ProductName}': số lượng phải lớn hơn 0 (Quantity={item.Quantity}).");
                    if (item.VatRate < 0 || item.VatRate > 1.0m)
                        sanityFailures.Add($"Sản phẩm '{item.ProductName}': thuế suất không hợp lệ (VatRate={item.VatRate}, phải từ 0 đến 1.0).");
                }
                if (sanityFailures.Count > 0)
                {
                    _logger.LogWarning("Checkout rejected — Tier 0 sanity check failed: {Failures}", string.Join("; ", sanityFailures));
                    return BadRequest(new { error = "Dữ liệu đơn hàng không hợp lệ.", details = sanityFailures });
                }

                // TIER 1: FeaturedProducts cross-check — compare client UnitPrice against
                // Gateway PG FeaturedProducts.DisplayPrice (local query, ~5ms).
                // Only applies to products that ARE in FeaturedProducts (QR-scanned products skip).
                // Tolerance: 5% — catches obvious manipulation (100k→1k) while allowing minor
                // price drift between featured time and checkout time.
                if (_dbContext != null)
                {
                    var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
                    var featuredPrices = await _dbContext.FeaturedProducts
                        .IgnoreQueryFilters()
                        .Where(fp => productIds.Contains(fp.ProductId) && fp.IsActive)
                        .Select(fp => new { fp.ProductId, fp.DisplayPrice, fp.DisplayName })
                        .ToListAsync();

                    if (featuredPrices.Count > 0)
                    {
                        var priceMismatches = new List<string>();
                        foreach (var fp in featuredPrices)
                        {
                            var clientItem = request.Items.FirstOrDefault(i => i.ProductId == fp.ProductId);
                            if (clientItem != null && fp.DisplayPrice > 0)
                            {
                                decimal tolerance = fp.DisplayPrice * 0.05m; // 5% tolerance
                                decimal diff = Math.Abs(clientItem.UnitPrice - fp.DisplayPrice);
                                if (diff > tolerance)
                                {
                                    priceMismatches.Add(
                                        $"Sản phẩm '{fp.DisplayName}': giá đã thay đổi (đơn hàng gửi {clientItem.UnitPrice:N0}đ, " +
                                        $"giá hiện tại {fp.DisplayPrice:N0}đ). Vui lòng tải lại trang để xem giá mới nhất.");
                                }
                            }
                        }
                        if (priceMismatches.Count > 0)
                        {
                            _logger.LogWarning("Checkout rejected — Tier 1 price mismatch: {Mismatches}", string.Join("; ", priceMismatches));
                            return BadRequest(new { error = "Giá sản phẩm đã thay đổi.", details = priceMismatches });
                        }
                    }
                }

                Guid customerDeviceId = Guid.TryParse(request.CustomerDeviceId, out Guid parsedId)
                    ? parsedId
                    : Guid.NewGuid();

                // Group items by TenantId — each tenant group becomes a separate order.
                var tenantGroups = request.Items
                    .GroupBy(i => i.TenantId)
                    .ToList();

                var response = new CheckoutResponse();

                // Pre-fetch ShopInstance IDs + Tenant names for all tenants in the cart (single query, IgnoreQueryFilters)
                Dictionary<Guid, Guid> tenantToShopInstance = [];
                Dictionary<Guid, string> tenantNames = [];
                if (_dbContext != null)
                {
                    var tenantIds = tenantGroups.Select(g => g.Key).ToList();
                    // Convert to List<TenantId> for LINQ translation (Tenant.Id is TenantId value object
                    // with HasConversion — Known Error Pattern #1: never use EF.Property<Guid> or .Value
                    // in Where. Contains with matching type translates correctly.)
                    var tenantIdValues = tenantIds.Select(id => new TenantId(id)).ToList();
                    var tenants = await _dbContext.Tenants
                        .IgnoreQueryFilters()
                        .Where(t => tenantIdValues.Contains(t.Id))
                        .Select(t => new { TenantId = t.Id.Value, t.ShopInstanceId, t.Name })
                        .ToListAsync();
                    tenantToShopInstance = tenants
                        .Where(t => t.ShopInstanceId.HasValue)
                        .ToDictionary(t => t.TenantId, t => t.ShopInstanceId!.Value);
                    tenantNames = tenants
                        .ToDictionary(t => t.TenantId, t => t.Name ?? string.Empty);

                    // Fallback: if any tenant has no ShopInstanceId (or doesn't exist in Tenants table),
                    // route to the first active ShopInstance. This ensures orders are never lost in
                    // single-VPS deployments where the tenant record may be missing or incomplete.
                    var unresolvedTenantIds = tenantGroups
                        .Select(g => g.Key)
                        .Where(tid => !tenantToShopInstance.ContainsKey(tid))
                        .ToList();
                    if (unresolvedTenantIds.Count > 0)
                    {
                        var fallbackShopInstanceId = await _dbContext.ShopInstances
                            .IgnoreQueryFilters()
                            .Where(s => s.IsActive)
                            .OrderBy(s => s.Id)
                            .Select(s => s.Id)
                            .FirstOrDefaultAsync();
                        if (fallbackShopInstanceId != Guid.Empty)
                        {
                            foreach (var tid in unresolvedTenantIds)
                            {
                                tenantToShopInstance[tid] = fallbackShopInstanceId;
                            }
                            _logger.LogWarning(
                                "Checkout: {Count} tenant(s) have no ShopInstanceId — routing to fallback {FallbackShopInstanceId}",
                                unresolvedTenantIds.Count, fallbackShopInstanceId);
                        }
                    }
                }

                foreach (var group in tenantGroups)
                {
                    Guid tenantId = group.Key;

                    // Lookup ShopInstanceId for routing key
                    string? routingKey = tenantToShopInstance.TryGetValue(tenantId, out Guid shopInstanceId)
                        ? shopInstanceId.ToString()
                        : null;

                    // Set tenant context for VanAnDbContext multi-tenancy filters
                    _tenantProvider.SetTenant(tenantId);

                    var command = new CreateOrderCommand
                    {
                        CustomerDeviceId = customerDeviceId,
                        Items = group.Select(i => new CoreHub.Commands.OrderItemRequest
                        {
                            ProductId = i.ProductId,
                            TenantId = i.TenantId,
                            ProductName = i.ProductName,
                            VatRate = i.VatRate,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice
                        }).ToList(),
                        CustomerName = request.CustomerName,
                        CustomerPhone = request.CustomerPhone,
                        CustomerAddress = request.CustomerAddress,
                        CustomerId = request.CustomerId,
                        CustomerNotes = request.CustomerNotes,
                        TrackingCode = request.TrackingCode
                    };

                    try
                    {
                        Order createdOrder = await _orderService.CreateOrderFromCommandAsync(command, tenantId, routingKey);

                        response.Orders.Add(new CreatedOrderDto
                        {
                            OrderId = createdOrder.Id,
                            TenantId = createdOrder.TenantId.Value,
                            TenantName = tenantNames.GetValueOrDefault(tenantId, string.Empty),
                            Amount = createdOrder.TotalAmount,
                            SubTotal = createdOrder.SubTotal,
                            TotalVatAmount = createdOrder.TotalVatAmount
                        });
                        response.SuccessCount++;

                        _logger.LogInformation(
                            "Checkout: order {OrderId} created for tenant {TenantId}, routingKey={RoutingKey}",
                            createdOrder.Id, tenantId, routingKey ?? "(none)");
                    }
                    catch (Exception ex)
                    {
                        response.FailureCount++;
                        response.Errors.Add(new CheckoutErrorDto
                        {
                            TenantId = tenantId,
                            TenantName = tenantNames.GetValueOrDefault(tenantId, string.Empty),
                            Error = ex.Message
                        });
                        _logger.LogError(ex,
                            "Checkout: failed to create order for tenant {TenantId}", tenantId);
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating checkout order — Items: {ItemCount}, CustomerName: {CustomerName}, CustomerPhone: {CustomerPhone}",
                    request?.Items?.Count ?? 0, request?.CustomerName, request?.CustomerPhone);
                return StatusCode(500, new { error = $"Lỗi tạo đơn hàng: {ex.Message}", detail = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// W6/Bucket D: Public order tracking endpoint for KhachLink customer-facing page.
        /// No auth required — customer knows their own order id (just placed it).
        /// Returns limited DTO (no tenant PII).
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<PublicOrderTrackingDto>> GetPublicOrder(Guid id)
        {
            try
            {
                Order? order = await _orderService.GetOrderByIdForPublicTrackingAsync(id, HttpContext.RequestAborted);
                if (order == null)
                {
                    return NotFound(new { error = "Order not found" });
                }

                // Resolve tenant name from PG Tenants table (single query)
                string tenantName = string.Empty;
                if (_dbContext != null && order.TenantId.Value != Guid.Empty)
                {
                    var tenant = await _dbContext.Tenants
                        .AsNoTracking()
                        .IgnoreQueryFilters()
                        .Where(t => t.Id == order.TenantId)
                        .Select(t => t.Name)
                        .FirstOrDefaultAsync(HttpContext.RequestAborted);
                    tenantName = tenant ?? string.Empty;
                }

                var dto = new PublicOrderTrackingDto
                {
                    OrderId = order.Id,
                    Status = order.Status?.Value ?? "pending",
                    PaymentStatus = order.PaymentStatus ?? "pending",
                    CreatedAt = order.CreatedAt,
                    TotalPrice = order.TotalPrice,
                    SubTotal = order.SubTotal,
                    TotalVatAmount = order.TotalVatAmount,
                    ItemCount = order.Items.Count,
                    TenantId = order.TenantId.Value,
                    TenantName = tenantName,
                    Items = order.Items.Select(i => new PublicOrderItemDto
                    {
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice,
                        VatRate = i.VatRate,
                        VatAmount = i.VatAmount
                    }).ToList()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching public order {OrderId}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    public class GuestOrderRequest
    {
        public string TrackingCode { get; set; } = string.Empty;
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string CustomerDeviceId { get; set; } = string.Empty;
    }

    public class CheckoutOrderRequest
    {
        public string? CustomerDeviceId { get; set; }
        public string? OrderType { get; set; }
        public string? CustomerNotes { get; set; }
        public List<CheckoutOrderItem> Items { get; set; } = new();

        // Bucket A feature (approved 2026-07-07): Guest checkout customer info.
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }

        // Logged-in customer ID (from KhachLink localStorage "customer_id").
        // When set, the created order is linked to this Customer so it appears in order history.
        public Guid? CustomerId { get; set; }

        // Campaign conversion tracking: tracking code from social campaign (set by KhachLink /c/{trackingCode}).
        // When set, OrderWorkflowService increments ConvertedOrders on the matching SocialCampaign.
        public string? TrackingCode { get; set; }
    }

    public class CheckoutOrderItem
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? Notes { get; set; }

        // Phase 3 (Multi-VPS Checkout): Client snapshot — Gateway creates order WITHOUT querying Products table.
        public Guid TenantId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal VatRate { get; set; } = 0.10m;
    }
}
