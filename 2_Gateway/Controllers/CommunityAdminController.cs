using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// CC-S6 (Sprint 6): Community admin endpoints — eligible customer list, activate/deactivate roles.
    /// Auth: SystemAdmin Bearer JWT (platform-level, cross-tenant).
    /// </summary>
    [ApiController]
    [Route("api/admin/community")]
    public class CommunityAdminController(
        ICommunityAdminService communityAdminService,
        ILogger<CommunityAdminController> logger) : ControllerBase
    {
        private readonly ICommunityAdminService _communityAdminService = communityAdminService;
        private readonly ILogger<CommunityAdminController> _logger = logger;

        /// <summary>
        /// GET /api/admin/community/eligible?page=1&amp;pageSize=20
        /// Returns customers eligible for community role activation.
        /// </summary>
        [HttpGet("eligible")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetEligible([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _communityAdminService.GetEligibleCustomersAsync(page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// POST /api/admin/community/{customerId}/activate-role
        /// Activate a community role (Shipper or Salesman) for a customer.
        /// </summary>
        [HttpPost("{customerId}/activate-role")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ActivateRole(Guid customerId, [FromBody] ActivateRoleRequest request)
        {
            if (!Enum.TryParse<CommunityRoleType>(request.Role, ignoreCase: true, out var roleType))
                return BadRequest(new { error = $"Invalid role: {request.Role}. Must be 'Shipper' or 'Salesman'." });

            try
            {
                // Get admin user ID from JWT claims
                var adminId = GetAdminUserId();
                var role = await _communityAdminService.ActivateRoleAsync(customerId, roleType, adminId);

                _logger.LogInformation("ActivateRole: {Role} activated for customer {CustomerId} by admin {AdminId}",
                    roleType, customerId, adminId);

                return Ok(new
                {
                    communityRoleId = role.Id,
                    roleType = role.RoleType.ToString(),
                    activatedAt = role.ActivatedAt
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("ActivateRole failed: {Message}", ex.Message);
                if (ex.Message.Contains("already has an active"))
                    return Conflict(new { error = ex.Message });
                if (ex.Message.Contains("does not meet eligibility") || ex.Message.Contains("not found") || ex.Message.Contains("not active"))
                    return BadRequest(new { error = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/community/{customerId}/deactivate-role
        /// Deactivate an active community role for a customer.
        /// </summary>
        [HttpPost("{customerId}/deactivate-role")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeactivateRole(Guid customerId, [FromBody] ActivateRoleRequest request)
        {
            if (!Enum.TryParse<CommunityRoleType>(request.Role, ignoreCase: true, out var roleType))
                return BadRequest(new { error = $"Invalid role: {request.Role}. Must be 'Shipper' or 'Salesman'." });

            try
            {
                await _communityAdminService.DeactivateRoleAsync(customerId, roleType);

                _logger.LogInformation("DeactivateRole: {Role} deactivated for customer {CustomerId}",
                    roleType, customerId);

                return Ok(new { deactivatedAt = DateTime.UtcNow });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("DeactivateRole failed: {Message}", ex.Message);
                if (ex.Message.Contains("No active"))
                    return NotFound(new { error = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
        }

        private Guid GetAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }
    }

    public class ActivateRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }
}
