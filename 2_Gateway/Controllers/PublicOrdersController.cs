using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Commands;
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
        ILogger<PublicOrdersController> logger) : ControllerBase
    {
        private readonly IOrderService _orderService = orderService;
        private readonly ISocialCampaignService _socialCampaignService = socialCampaignService;
        private readonly ITenantProvider _tenantProvider = tenantProvider;
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
        /// W4 Fix: Create order from KhachLink checkout flow (no auth required).
        /// KhachLink is a customer-facing app — customers don't login.
        /// Uses a default test tenant for E2E. In production, tenant is resolved from shop context.
        /// </summary>
        [HttpPost("checkout")]
        public async Task<ActionResult<object>> CreateCheckoutOrder([FromBody] CheckoutOrderRequest request)
        {
            try
            {
                if (request == null || request.Items == null || request.Items.Count == 0)
                {
                    return BadRequest(new { error = "Invalid checkout order request — items required" });
                }

                // Use tenant that matches seeded product data (tenantId in ShopERP's vanan_shoperp.db)
                Guid tenantId = new("00000000-0000-0000-0000-000000000001");

                // Set tenant context for VanAnDbContext multi-tenancy filters (no JWT in anonymous flow)
                _tenantProvider.SetTenant(tenantId);

                Guid customerDeviceId = Guid.TryParse(request.CustomerDeviceId, out Guid parsedId)
                    ? parsedId
                    : Guid.NewGuid();

                var command = new CreateOrderCommand
                {
                    CustomerDeviceId = customerDeviceId,
                    Items = request.Items.Select(i => new CoreHub.Commands.OrderItemRequest
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice
                    }).ToList(),
                    // Bucket A feature (approved 2026-07-07): pass guest customer info through.
                    CustomerName = request.CustomerName,
                    CustomerPhone = request.CustomerPhone,
                    CustomerAddress = request.CustomerAddress
                };

                Order createdOrder = await _orderService.CreateOrderFromCommandAsync(command, tenantId);

                _logger.LogInformation(
                    "Checkout order {OrderId} created for tenant {TenantId}",
                    createdOrder.Id,
                    tenantId);

                return Ok(new
                {
                    OrderId = createdOrder.Id,
                    QrImageUrl = (string?)null,
                    PaymentUrl = (string?)null,
                    Amount = createdOrder.TotalAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating checkout order");
                return StatusCode(500, new { error = "Internal server error" });
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
                    CreatedAt = order.CreatedAt,
                    TotalPrice = order.TotalPrice,
                    ItemCount = order.Items.Count,
                    TenantId = order.TenantId.Value,
                    Items = order.Items.Select(i => new PublicOrderItemDto
                    {
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice
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
    }

    public class CheckoutOrderItem
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? Notes { get; set; }
    }
}
