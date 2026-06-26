using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Commands;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/public/orders")]
    [AllowAnonymous]
    public class PublicOrdersController(
        IOrderService orderService,
        ISocialCampaignService socialCampaignService,
        ILogger<PublicOrdersController> logger) : ControllerBase
    {
        private readonly IOrderService _orderService = orderService;
        private readonly ISocialCampaignService _socialCampaignService = socialCampaignService;
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
    }

    public class GuestOrderRequest
    {
        public string TrackingCode { get; set; } = string.Empty;
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string CustomerDeviceId { get; set; } = string.Empty;
    }
}
