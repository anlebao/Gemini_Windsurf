using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// CC-S6 (Sprint 6 v1.2): Fraud review admin endpoints.
    /// List pending flags, get detail, confirm/dismiss, get stats.
    /// Auth: SystemAdmin Bearer JWT (platform-level, cross-tenant).
    /// </summary>
    [ApiController]
    [Route("api/admin/community")]
    public class FraudFlagController(
        IFraudReviewService fraudReviewService,
        ILogger<FraudFlagController> logger) : ControllerBase
    {
        private readonly IFraudReviewService _fraudReviewService = fraudReviewService;
        private readonly ILogger<FraudFlagController> _logger = logger;

        /// <summary>
        /// GET /api/admin/community/fraud-flags?status=Pending&amp;page=1&amp;pageSize=20
        /// List fraud flags by status (default Pending), sorted by RiskScore desc.
        /// </summary>
        [HttpGet("fraud-flags")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetFraudFlags(
            [FromQuery] string status = "Pending",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _fraudReviewService.GetFlagsAsync(status, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// GET /api/admin/community/fraud-flags/{id}
        /// Get fraud flag detail with related entities.
        /// </summary>
        [HttpGet("fraud-flags/{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetFraudFlagDetail(Guid id)
        {
            var detail = await _fraudReviewService.GetDetailAsync(id);
            if (detail == null)
                return NotFound(new { error = $"FraudFlag {id} not found." });
            return Ok(detail);
        }

        /// <summary>
        /// POST /api/admin/community/fraud-flags/{id}/confirm
        /// Confirm a fraud flag. Side effects: reject related entity, wallet reversal if paid, 3-strike ban.
        /// </summary>
        [HttpPost("fraud-flags/{id:guid}/confirm")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ConfirmFraudFlag(Guid id)
        {
            try
            {
                var adminId = GetAdminUserId();
                var result = await _fraudReviewService.ConfirmAsync(id, adminId);

                _logger.LogInformation("ConfirmFraudFlag: {Id} confirmed by {AdminId}. Banned: {Banned}. SideEffects: {Count}",
                    id, adminId, result.CustomerBanned, result.SideEffects.Count);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("ConfirmFraudFlag failed: {Message}", ex.Message);
                if (ex.Message.Contains("not found"))
                    return NotFound(new { error = ex.Message });
                if (ex.Message.Contains("already"))
                    return Conflict(new { error = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/community/fraud-flags/{id}/dismiss
        /// Dismiss a fraud flag (false positive). Side effects: whitelist device, no strike.
        /// </summary>
        [HttpPost("fraud-flags/{id:guid}/dismiss")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DismissFraudFlag(Guid id)
        {
            try
            {
                var adminId = GetAdminUserId();
                var result = await _fraudReviewService.DismissAsync(id, adminId);

                _logger.LogInformation("DismissFraudFlag: {Id} dismissed by {AdminId}. SideEffects: {Count}",
                    id, adminId, result.SideEffects.Count);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("DismissFraudFlag failed: {Message}", ex.Message);
                if (ex.Message.Contains("not found"))
                    return NotFound(new { error = ex.Message });
                if (ex.Message.Contains("already"))
                    return Conflict(new { error = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/admin/community/fraud-stats
        /// Get fraud stats dashboard: pending/confirmed/dismissed counts, loss prevented, top flagged customers.
        /// </summary>
        [HttpGet("fraud-stats")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetFraudStats()
        {
            var stats = await _fraudReviewService.GetStatsAsync();
            return Ok(stats);
        }

        private Guid GetAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }
    }
}
