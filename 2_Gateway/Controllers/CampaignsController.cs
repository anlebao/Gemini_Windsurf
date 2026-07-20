using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CampaignsController(
        ISocialCampaignService socialCampaignService,
        ILogger<CampaignsController> logger) : ControllerBase
    {
        private readonly ISocialCampaignService _socialCampaignService = socialCampaignService;
        private readonly ILogger<CampaignsController> _logger = logger;

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<SocialCampaign>>> GetAllCampaigns()
        {
            try
            {
                var campaigns = await _socialCampaignService.GetAllCampaignsAsync();
                return Ok(campaigns.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all campaigns");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{trackingCode}")]
        [AllowAnonymous]
        public async Task<ActionResult<SocialCampaign>> GetCampaignByTrackingCode(string trackingCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(trackingCode))
                {
                    return BadRequest(new { error = "Tracking code is required" });
                }

                SocialCampaign? campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(trackingCode);
                if (campaign == null)
                {
                    return NotFound(new { error = "Campaign not found" });
                }

                return Ok(campaign);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching campaign by tracking code {TrackingCode}", trackingCode);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("click/{code}")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> RecordClick(string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    return BadRequest(new { error = "Tracking code is required" });
                }

                bool result = await _socialCampaignService.RecordClickAsync(code);
                return Ok(new { Recorded = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording campaign click for {TrackingCode}", code);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // P2 FIX: Missing endpoints referenced by KhachLink SocialCampaignHttpService

        [HttpGet("{campaignId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<SocialCampaign>> GetCampaignById(Guid campaignId)
        {
            try
            {
                SocialCampaign? campaign = await _socialCampaignService.GetCampaignByIdAsync(campaignId);
                return campaign == null ? NotFound() : Ok(campaign);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching campaign {CampaignId}", campaignId);
                return StatusCode(500, new { error = "Internal server error" });
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
                _logger.LogError(ex, "Error fetching campaigns for shop {ShopId}", shopId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Home page personalization: fetch active campaigns by tenantId.
        // SocialCampaign implements IMustHaveTenant, so GetCampaignsByShopAsync
        // actually queries by TenantId internally (parameter name is legacy).
        [HttpGet("by-tenant/{tenantId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<List<SocialCampaign>>> GetByTenant(Guid tenantId)
        {
            try
            {
                if (tenantId == Guid.Empty)
                {
                    return Ok(new List<SocialCampaign>());
                }

                List<SocialCampaign> campaigns = await _socialCampaignService.GetCampaignsByShopAsync(tenantId);
                return Ok(campaigns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching campaigns for tenant {TenantId}", tenantId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{campaignId:guid}/tracking-url")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> GenerateTrackingUrl(Guid campaignId)
        {
            try
            {
                string url = await _socialCampaignService.GenerateTrackingUrlAsync(campaignId);
                return Ok(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating tracking URL for campaign {CampaignId}", campaignId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("{campaignId:guid}/increment-conversion")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> IncrementConversion(Guid campaignId)
        {
            try
            {
                bool result = await _socialCampaignService.IncrementConvertedOrdersAsync(campaignId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing conversion for campaign {CampaignId}", campaignId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPut("{campaignId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<SocialCampaign>> UpdateCampaign(Guid campaignId, [FromBody] SocialCampaign campaign)
        {
            try
            {
                SocialCampaign updated = await _socialCampaignService.UpdateCampaignAsync(campaign);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating campaign {CampaignId}", campaignId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpDelete("{campaignId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<bool>> DeleteCampaign(Guid campaignId)
        {
            try
            {
                bool result = await _socialCampaignService.DeleteCampaignAsync(campaignId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting campaign {CampaignId}", campaignId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
