using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Sprint 7 Q3 — Community fund admin endpoints. Balance + spend + history.
    /// Auth: SystemAdmin Bearer JWT (platform-level, cross-tenant).
    /// </summary>
    [ApiController]
    [Route("api/admin/community-fund")]
    public class CommunityFundController(
        ICommunityFundService communityFundService,
        ILogger<CommunityFundController> logger) : ControllerBase
    {
        private readonly ICommunityFundService _communityFundService = communityFundService;
        private readonly ILogger<CommunityFundController> _logger = logger;

        /// <summary>
        /// GET /api/admin/community-fund/balance
        /// Returns current balance + total collected + total spent.
        /// </summary>
        [HttpGet("balance")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetBalance()
        {
            var balance = await _communityFundService.GetBalanceAsync();
            return Ok(balance);
        }

        /// <summary>
        /// POST /api/admin/community-fund/spend
        /// Spend from community fund. Creates wallet tx + audit record.
        /// </summary>
        [HttpPost("spend")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Spend([FromBody] SpendRequest request)
        {
            if (request == null || request.Amount <= 0)
                return BadRequest(new { error = "Amount phải lớn hơn 0." });
            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { error = "Reason không được để trống." });
            if (string.IsNullOrWhiteSpace(request.Recipient))
                return BadRequest(new { error = "Recipient không được để trống." });

            try
            {
                var adminId = GetAdminUserId();
                var result = await _communityFundService.SpendAsync(request.Amount, request.Reason, request.Recipient, adminId);

                _logger.LogInformation("Community fund spend {Amount} by admin {AdminId} for {Reason} → recipient {Recipient}",
                    request.Amount, adminId, request.Reason, request.Recipient);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/admin/community-fund/history?page=1&amp;pageSize=20
        /// Returns paginated spend history.
        /// </summary>
        [HttpGet("history")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _communityFundService.GetHistoryAsync(page, pageSize);
            return Ok(result);
        }

        private Guid GetAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }
    }

    public class SpendRequest
    {
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
    }
}
