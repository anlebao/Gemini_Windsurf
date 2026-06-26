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
    }
}
