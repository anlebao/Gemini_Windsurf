using Microsoft.AspNetCore.Authorization;
using VanAn.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// API surface for KhachLink social-campaign operations.
    /// Hosted in ShopERP so that KhachLink never references CoreHub services directly.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SocialCampaignsController(
        ISocialCampaignService socialCampaignService,
        ILogger<SocialCampaignsController> logger) : ControllerBase
    {
        private readonly ISocialCampaignService _socialCampaignService = socialCampaignService;
        private readonly ILogger<SocialCampaignsController> _logger = logger;

        [HttpGet("by-tracking-code/{trackingCode}")]
        [AllowAnonymous]
        public async Task<ActionResult<SocialCampaign>> GetByTrackingCode(string trackingCode)
        {
            try
            {
                SocialCampaign? campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(trackingCode);
                return campaign == null ? NotFound() : Ok(campaign);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting campaign by tracking code {TrackingCode}", trackingCode);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("record-click/{trackingCode}")]
        [AllowAnonymous]
        public async Task<ActionResult> RecordClick(string trackingCode)
        {
            try
            {
                bool success = await _socialCampaignService.RecordClickAsync(trackingCode);
                return success ? Ok() : NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording click for tracking code {TrackingCode}", trackingCode);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("by-shop/{shopId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<List<SocialCampaign>>> GetByShop(Guid shopId)
        {
            try
            {
                List<SocialCampaign> campaigns = await _socialCampaignService.GetCampaignsByShopAsync(shopId);
                return Ok(campaigns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting campaigns for shop {ShopId}", shopId);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
