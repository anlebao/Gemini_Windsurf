using Microsoft.AspNetCore.Authorization;
using VanAn.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CampaignsController(
        ISocialCampaignService socialCampaignService,
        IHttpClientFactory httpClientFactory,
        ILogger<CampaignsController> logger) : ControllerBase
    {
        private readonly ISocialCampaignService _socialCampaignService = socialCampaignService;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
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
            // Shop entity removed 2026-07-21 — redirect to by-tenant endpoint.
            // shopId parameter is now interpreted as tenantId for backward compat.
            try
            {
                List<SocialCampaign> campaigns = await _socialCampaignService.GetCampaignsByTenantAsync(shopId);
                return Ok(campaigns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching campaigns for tenant (legacy shopId) {ShopId}", shopId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Home page personalization: fetch active campaigns by tenantId.
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

                List<SocialCampaign> campaigns = await _socialCampaignService.GetCampaignsByTenantAsync(tenantId);
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

        // POST create — SystemAdmin only (admin operations)
        [HttpPost]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<ActionResult<SocialCampaign>> CreateCampaign([FromBody] CreateCampaignRequest request)
        {
            try
            {
                if (request.TenantId == Guid.Empty)
                {
                    return BadRequest(new { error = "TenantId is required" });
                }

                if (string.IsNullOrWhiteSpace(request.CampaignName))
                {
                    return BadRequest(new { error = "CampaignName is required" });
                }

                // ShopId removed 2026-07-21 — campaigns are tenant-wide only.

                // Generate tracking code if not provided
                var trackingCode = string.IsNullOrWhiteSpace(request.TrackingCode)
                    ? $"camp_{Guid.NewGuid():N}"[..24]
                    : request.TrackingCode;

                var campaign = new SocialCampaign(
                    new TenantId(request.TenantId),
                    request.UtmSource ?? string.Empty,
                    request.CampaignName,
                    trackingCode);
                campaign.SetMedia(request.ImageUrl, request.VideoUrl);

                var created = await _socialCampaignService.CreateCampaignAsync(campaign);
                return CreatedAtAction(nameof(GetCampaignById), new { campaignId = created.Id }, created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating campaign");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPut("{campaignId:guid}")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<ActionResult<SocialCampaign>> UpdateCampaign(Guid campaignId, [FromBody] UpdateCampaignRequest request)
        {
            try
            {
                // Fetch existing campaign
                var existing = await _socialCampaignService.GetCampaignByIdAsync(campaignId);
                if (existing == null)
                {
                    return NotFound(new { error = "Campaign not found" });
                }

                // Build updated campaign with same Id/TenantId
                var updated = new SocialCampaign(
                    existing.TenantId,
                    request.UtmSource ?? existing.UtmSource,
                    request.CampaignName ?? existing.CampaignName,
                    request.TrackingCode ?? existing.TrackingCode);
                typeof(BaseEntity).GetProperty("Id")!.SetValue(updated, existing.Id);
                typeof(BaseEntity).GetProperty("CreatedAt")!.SetValue(updated, existing.CreatedAt);
                updated.SetMedia(request.ImageUrl ?? existing.ImageUrl, request.VideoUrl ?? existing.VideoUrl);

                var result = await _socialCampaignService.UpdateCampaignAsync(updated);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating campaign {CampaignId}", campaignId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpDelete("{campaignId:guid}")]
        [Authorize(Policy = "SystemAdmin")]
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

        /// <summary>
        /// Phase 5 SC10: POST /api/campaigns/{campaignId}/send-push — send bulk push to customers
        /// matching the segment criteria, associated with a specific campaign.
        /// Forwards to ShopERP POST /api/push/send (PushAdminController) which owns
        /// CampaignPushJob creation + PushNotificationService.SendBulkNotificationAsync.
        /// SystemAdmin only (JWT policy enforced at Gateway; ShopERP re-checks admin cookie).
        /// </summary>
        [HttpPost("{campaignId:guid}/send-push")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> SendCampaignPush(Guid campaignId, [FromBody] SendCampaignPushRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
                    return BadRequest(new { error = "Title and Body are required." });

                var client = _httpClientFactory.CreateClient("shoperp");
                var payload = new
                {
                    CampaignId = campaignId,
                    request.Title,
                    request.Body,
                    request.ActionUrl,
                    request.CustomerTier,
                    request.MinIdentityLevel,
                    request.MinTotalSpent,
                    request.MaxTotalSpent,
                    request.LastOrderAfter,
                    request.LastOrderBefore
                };
                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/push/send")
                {
                    Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
                // Forward admin JWT so ShopERP PushAdminController [Authorize] accepts the call.
                if (Request.Headers.TryGetValue("Authorization", out var authHeader))
                    reqMsg.Headers.Add("Authorization", authHeader.ToString());

                var response = await client.SendAsync(reqMsg);
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding campaign push for {CampaignId}", campaignId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    /// <summary>
    /// Phase 5 SC10: Request body for POST /api/campaigns/{id}/send-push.
    /// Mirrors ShopERP PushAdminController.SendBulkPushRequest.
    /// </summary>
    public record SendCampaignPushRequest
    {
        public string Title { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public string? ActionUrl { get; init; }
        public string? CustomerTier { get; init; }
        public IdentityLevel? MinIdentityLevel { get; init; }
        public decimal? MinTotalSpent { get; init; }
        public decimal? MaxTotalSpent { get; init; }
        public DateTime? LastOrderAfter { get; init; }
        public DateTime? LastOrderBefore { get; init; }
    }

    // DTO for create campaign request — SystemAdmin admin UI
    public record CreateCampaignRequest
    {
        public Guid TenantId { get; init; }
        public string CampaignName { get; init; } = string.Empty;
        public string UtmSource { get; init; } = string.Empty;
        public string? TrackingCode { get; init; }
        public string? ImageUrl { get; init; }
        public string? VideoUrl { get; init; }
    }

    // DTO for update campaign request — SystemAdmin admin UI
    public record UpdateCampaignRequest
    {
        public string? CampaignName { get; init; }
        public string? UtmSource { get; init; }
        public string? TrackingCode { get; init; }
        public bool IsActive { get; init; } = true;
        public string? ImageUrl { get; init; }
        public string? VideoUrl { get; init; }
    }
}
