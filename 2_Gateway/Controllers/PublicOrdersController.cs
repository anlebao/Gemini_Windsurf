using Microsoft.AspNetCore.Authorization;
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

                Order createdOrder = await _orderService.CreateOrderFromCommandAsync(command, campaign.ShopId);

                _logger.LogInformation(
                    "Guest order {OrderId} created for campaign {TrackingCode} on shop {ShopId}",
                    createdOrder.Id,
                    request.TrackingCode,
                    campaign.ShopId);

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

                Guid customerDeviceId = Guid.TryParse(request.CustomerDeviceId, out Guid parsedId)
                    ? parsedId
                    : Guid.NewGuid();

                // Group items by TenantId — each tenant group becomes a separate order.
                var tenantGroups = request.Items
                    .GroupBy(i => i.TenantId)
                    .ToList();

                var response = new CheckoutResponse();

                // Pre-fetch ShopInstance IDs for all tenants in the cart (single query, IgnoreQueryFilters)
                Dictionary<Guid, Guid> tenantToShopInstance = [];
                if (_dbContext != null)
                {
                    var tenantIds = tenantGroups.Select(g => g.Key).ToList();
                    var tenants = await _dbContext.Tenants
                        .IgnoreQueryFilters()
                        .Where(t => tenantIds.Contains(t.Id))
                        .Select(t => new { TenantId = t.Id.Value, t.ShopInstanceId })
                        .ToListAsync();
                    tenantToShopInstance = tenants
                        .Where(t => t.ShopInstanceId.HasValue)
                        .ToDictionary(t => t.TenantId, t => t.ShopInstanceId!.Value);
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
                        CustomerId = request.CustomerId
                    };

                    try
                    {
                        Order createdOrder = await _orderService.CreateOrderFromCommandAsync(command, tenantId, routingKey);

                        response.Orders.Add(new CreatedOrderDto
                        {
                            OrderId = createdOrder.Id,
                            TenantId = createdOrder.TenantId.Value,
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
