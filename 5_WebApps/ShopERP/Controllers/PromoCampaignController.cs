using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// WS-2: Promo campaign admin controller — create + list + cancel + track bulk push campaigns.
    /// Auth: Cookie auth, [Authorize(Policy = "OwnerOnly")] (Owner/SystemAdmin only — AF-P0-T1).
    /// </summary>
    [ApiController]
    [Route("api/promo-campaigns")]
    [Authorize(Policy = "OwnerOnly")]
    public class PromoCampaignController : ControllerBase
    {
        private readonly IPromoCampaignService _campaignService;
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<PromoCampaignController> _logger;

        public PromoCampaignController(
            IPromoCampaignService campaignService,
            ICustomerRepository customerRepository,
            ILogger<PromoCampaignController> logger)
        {
            _campaignService = campaignService;
            _customerRepository = customerRepository;
            _logger = logger;
        }

        /// <summary>List campaigns (paginated, newest first).</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var campaigns = await _campaignService.GetCampaignsAsync(page, pageSize);
            return Ok(new
            {
                items = campaigns.Select(MapCampaignDto).ToList(),
                page,
                pageSize
            });
        }

        /// <summary>Get campaign detail + recipient status summary.</summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var campaign = await _campaignService.GetCampaignAsync(id);
            if (campaign == null) return NotFound(new { error = "Không tìm thấy chiến dịch." });

            var (pending, sent, failed) = await _campaignService.GetRecipientStatusSummaryAsync(id);
            return Ok(new
            {
                campaign = MapCampaignDto(campaign),
                recipientSummary = new { pending, sent, failed, total = pending + sent + failed }
            });
        }

        /// <summary>Create a new campaign (Pending → processed async by PromoCampaignJob).</summary>
        /// <remarks>
        /// AF-P2-T1/T2: If <see cref="CreateCampaignRequest.SelectedCustomerIds"/> is non-empty, the campaign
        /// targets that explicit list (per-row "Gửi" + bulk select). Otherwise falls back to segment criteria.
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { error = "Tiêu đề không được để trống." });
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { error = "Nội dung thông báo không được để trống." });

            try
            {
                PromoCampaign campaign;
                if (request.SelectedCustomerIds is { Count: > 0 } ids)
                {
                    // Explicit recipient list (per-row + bulk select flows)
                    campaign = await _campaignService.CreateCampaignAsync(
                        request.Title, request.Message, request.Url, ids);
                }
                else
                {
                    // Segment-criteria flow (existing behavior)
                    var criteria = CustomerController.BuildCriteria(request.Segment);
                    campaign = await _campaignService.CreateCampaignAsync(
                        request.Title, request.Message, request.Url, criteria);
                }

                _logger.LogInformation("PromoCampaign created: {CampaignId} ('{Title}') with {Count} recipients",
                    campaign.Id, campaign.Title, campaign.TotalRecipients);
                return Ok(new { campaignId = campaign.Id, totalRecipients = campaign.TotalRecipients });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create promo campaign");
                return StatusCode(500, new { error = "Lỗi hệ thống khi tạo chiến dịch." });
            }
        }

        /// <summary>Cancel a pending/processing campaign.</summary>
        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            bool ok = await _campaignService.CancelCampaignAsync(id);
            if (!ok) return BadRequest(new { error = "Không thể hủy chiến dịch (không tồn tại hoặc đã hoàn thành)." });
            return Ok(new { success = true });
        }

        /// <summary>List recipients for a campaign (paginated, with delivery status).</summary>
        [HttpGet("{id:guid}/recipients")]
        public async Task<IActionResult> GetRecipients(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var recipients = await _campaignService.GetRecipientsAsync(id, page, pageSize);

            // Enrich with customer names (lookup in single query)
            var customerIds = recipients.Select(r => r.CustomerId).Distinct().ToList();
            var customerNames = new Dictionary<Guid, string>();
            foreach (var cid in customerIds)
            {
                var c = await _customerRepository.GetByIdAsync(cid);
                if (c != null) customerNames[cid] = c.FullName;
            }

            return Ok(new
            {
                items = recipients.Select(r => new
                {
                    r.Id,
                    r.CustomerId,
                    customerName = customerNames.TryGetValue(r.CustomerId, out var name) ? name : "(unknown)",
                    r.Status,
                    r.SentAt,
                    r.ErrorMessage
                }).ToList(),
                page,
                pageSize
            });
        }

        private static CampaignDto MapCampaignDto(PromoCampaign c) => new()
        {
            Id = c.Id,
            Title = c.Title,
            Message = c.Message,
            Url = c.Url,
            Status = c.Status,
            TotalRecipients = c.TotalRecipients,
            SentCount = c.SentCount,
            FailedCount = c.FailedCount,
            CreatedAt = c.CreatedAt,
            StartedAt = c.StartedAt,
            CompletedAt = c.CompletedAt,
            ErrorMessage = c.ErrorMessage
        };
    }

    // === DTOs ===

    public class CreateCampaignRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Url { get; set; }
        public SegmentRequest Segment { get; set; } = new();

        /// <summary>
        /// AF-P2-T1/T2: Explicit recipient list (per-row "Gửi" + bulk select).
        /// When non-empty, takes precedence over <see cref="Segment"/> criteria.
        /// </summary>
        public List<Guid>? SelectedCustomerIds { get; set; }
    }

    public class CampaignDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalRecipients { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
