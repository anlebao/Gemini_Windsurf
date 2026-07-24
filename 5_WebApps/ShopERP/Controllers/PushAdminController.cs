using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// Phase 5: Admin endpoints for campaign bulk push + push job tracking.
    /// All endpoints require ShopERP admin auth (cookie-based).
    /// Routes:
    ///   POST /api/push/send          — send bulk push to customer segment
    ///   GET  /api/push/jobs          — list campaign push jobs (history)
    ///   GET  /api/push/jobs/{id}     — get push job detail + delivery stats
    /// </summary>
    [ApiController]
    [Route("api/push")]
    [Authorize]
    public class PushAdminController(
        ICustomerSegmentationService customerSegmentationService,
        PushNotificationService pushNotificationService,
        IVanAnDbContext dbContext,
        ILogger<PushAdminController> logger) : ControllerBase
    {
        private readonly ICustomerSegmentationService _customerSegmentationService = customerSegmentationService;
        private readonly PushNotificationService _pushNotificationService = pushNotificationService;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<PushAdminController> _logger = logger;

        /// <summary>
        /// Phase 5: POST /api/push/send — send bulk push to customer segment.
        /// Admin selects criteria → service filters customers → sends push to each.
        /// Creates CampaignPushJob record for tracking.
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendBulkPush([FromBody] SendBulkPushRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
                return BadRequest(new { error = "Title and Body are required." });

            try
            {
                // Build segmentation criteria
                var criteria = new CustomerSegmentCriteria(
                    CustomerTier: string.IsNullOrWhiteSpace(request.CustomerTier) ? null : request.CustomerTier,
                    MinIdentityLevel: request.MinIdentityLevel,
                    MinTotalSpent: request.MinTotalSpent,
                    MaxTotalSpent: request.MaxTotalSpent,
                    LastOrderAfter: request.LastOrderAfter,
                    LastOrderBefore: request.LastOrderBefore,
                    HasPushSubscription: true); // Always filter to customers with push subscriptions

                // Get matching customers
                var customers = await _customerSegmentationService.GetCustomersBySegmentAsync(criteria);

                if (customers.Count == 0)
                    return Ok(new { message = "Không có khách hàng phù hợp với tiêu chí.", sentCount = 0, failedCount = 0, totalCustomers = 0 });

                // Create CampaignPushJob record (CampaignId required — use provided or generate a sentinel)
                var tenantId = new TenantId(Guid.Empty); // Will be set by EF interceptor from current tenant context
                var campaignId = request.CampaignId ?? Guid.NewGuid();
                var job = new CampaignPushJob(
                    tenantId,
                    campaignId,
                    System.Text.Json.JsonSerializer.Serialize(request));

                _dbContext.CampaignPushJobs.Add(job);
                await _dbContext.SaveChangesAsync();

                // Send bulk push
                var customerIds = customers.Select(c => c.Id).ToList();
                var (sentCount, failedCount) = await _pushNotificationService.SendBulkNotificationAsync(
                    customerIds,
                    request.Title,
                    request.Body,
                    request.ActionUrl,
                    job.Id);

                // Update job stats
                job.MarkAsCompleted(sentCount, failedCount);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Bulk push complete: JobId={JobId}, Sent={Sent}, Failed={Failed}, Total={Total}",
                    job.Id, sentCount, failedCount, customers.Count);

                return Ok(new {
                    jobId = job.Id,
                    sentCount,
                    failedCount,
                    totalCustomers = customers.Count,
                    message = $"Đã gửi push tới {sentCount}/{customers.Count} khách hàng."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending bulk push");
                return StatusCode(500, new { error = "Lỗi khi gửi push." });
            }
        }

        /// <summary>
        /// Phase 5: GET /api/push/jobs — list campaign push jobs (history).
        /// </summary>
        [HttpGet("jobs")]
        public async Task<IActionResult> ListJobs()
        {
            try
            {
                var jobs = await _dbContext.CampaignPushJobs
                    .OrderByDescending(j => j.CreatedAt)
                    .Take(50)
                    .Select(j => new {
                        j.Id,
                        j.CampaignId,
                        j.Status,
                        j.SentCount,
                        j.FailedCount,
                        j.ClickedCount,
                        j.SentAt,
                        j.ErrorMessage,
                        j.CreatedAt,
                        CTR = j.SentCount > 0 ? Math.Round((double)j.ClickedCount / j.SentCount * 100, 1) : 0
                    })
                    .ToListAsync();

                return Ok(jobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing push jobs");
                return StatusCode(500, new { error = "Lỗi khi lấy danh sách push jobs." });
            }
        }

        /// <summary>
        /// Phase 5: GET /api/push/jobs/{id} — get push job detail + delivery stats.
        /// </summary>
        [HttpGet("jobs/{id}")]
        public async Task<IActionResult> GetJob(Guid id)
        {
            try
            {
                var job = await _dbContext.CampaignPushJobs
                    .FirstOrDefaultAsync(j => j.Id == id);

                if (job == null)
                    return NotFound(new { error = "Push job not found." });

                // Get delivery stats
                var deliveries = await _dbContext.PushNotificationDeliveries
                    .Where(d => d.CampaignPushJobId == job.Id)
                    .GroupBy(d => d.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                return Ok(new {
                    job.Id,
                    job.CampaignId,
                    job.Status,
                    job.SentCount,
                    job.FailedCount,
                    job.ClickedCount,
                    job.SentAt,
                    job.ErrorMessage,
                    job.CreatedAt,
                    CTR = job.SentCount > 0 ? Math.Round((double)job.ClickedCount / job.SentCount * 100, 1) : 0,
                    deliveryStats = deliveries
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting push job {JobId}", id);
                return StatusCode(500, new { error = "Lỗi khi lấy chi tiết push job." });
            }
        }
    }

    /// <summary>
    /// Phase 5: Request body for POST /api/push/send (bulk push to segment).
    /// </summary>
    public class SendBulkPushRequest
    {
        public Guid? CampaignId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
        public string? CustomerTier { get; set; }
        public IdentityLevel? MinIdentityLevel { get; set; }
        public decimal? MinTotalSpent { get; set; }
        public decimal? MaxTotalSpent { get; set; }
        public DateTime? LastOrderAfter { get; set; }
        public DateTime? LastOrderBefore { get; set; }
    }
}
