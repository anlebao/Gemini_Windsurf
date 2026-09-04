using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// R2 (2026-09-04): Tenant-scoped community admin endpoints for Owner (Reseller owner).
    /// Auth: RequireOwnerRole policy (tenant_id claim + Owner role). Tenant ID pulled from JWT — NOT route param (IDOR safe).
    /// Owner can only activate/deactivate roles for customers of their own tenant.
    /// SystemAdmin cross-tenant flow uses existing CommunityAdminController (/api/admin/community/*) — unchanged.
    /// </summary>
    [ApiController]
    [Route("api/v1/tenant-community")]
    [Authorize(Policy = "RequireOwnerRole", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class TenantCommunityAdminController(
        ICommunityAdminService communityAdminService,
        ILogger<TenantCommunityAdminController> logger) : ControllerBase
    {
        private readonly ICommunityAdminService _communityAdminService = communityAdminService;
        private readonly ILogger<TenantCommunityAdminController> _logger = logger;

        /// <summary>
        /// GET /api/v1/tenant-community/eligible?page=1&amp;pageSize=20
        /// Returns customers of the calling tenant eligible for community role activation.
        /// tenant_id is read from JWT claim (NOT route param — IDOR safe).
        /// </summary>
        [HttpGet("eligible")]
        public async Task<IActionResult> GetEligible([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            var result = await _communityAdminService.GetEligibleCustomersForTenantAsync(tenantId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// POST /api/v1/tenant-community/{customerId}/activate-role
        /// Activate a community role (Shipper or Salesman) for a customer of the calling tenant.
        /// IDOR guard: service throws UnauthorizedAccessException if customer.TenantId != JWT tenant_id.
        /// </summary>
        [HttpPost("{customerId}/activate-role")]
        public async Task<IActionResult> ActivateRole(Guid customerId, [FromBody] ActivateRoleRequest request)
        {
            if (!Enum.TryParse<CommunityRoleType>(request.Role, ignoreCase: true, out var roleType))
                return BadRequest(new { error = $"Invalid role: {request.Role}. Must be 'Shipper' or 'Salesman'." });

            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            try
            {
                var ownerId = GetOwnerUserId();
                var role = await _communityAdminService.ActivateRoleForTenantAsync(tenantId, customerId, roleType, ownerId);

                _logger.LogInformation(
                    "ActivateRole (Owner): {Role} activated for customer {CustomerId} of tenant {TenantId} by owner {OwnerId}",
                    roleType, customerId, tenantId, ownerId);

                return Ok(new
                {
                    communityRoleId = role.Id,
                    roleType = role.RoleType.ToString(),
                    activatedAt = role.ActivatedAt
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("ActivateRole (Owner) IDOR blocked: {Message}", ex.Message);
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("ActivateRole (Owner) failed: {Message}", ex.Message);
                if (ex.Message.Contains("already has an active"))
                    return Conflict(new { error = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/v1/tenant-community/{customerId}/deactivate-role
        /// Deactivate an active community role for a customer of the calling tenant.
        /// IDOR guard: service throws UnauthorizedAccessException if role.TenantId != JWT tenant_id.
        /// </summary>
        [HttpPost("{customerId}/deactivate-role")]
        public async Task<IActionResult> DeactivateRole(Guid customerId, [FromBody] ActivateRoleRequest request)
        {
            if (!Enum.TryParse<CommunityRoleType>(request.Role, ignoreCase: true, out var roleType))
                return BadRequest(new { error = $"Invalid role: {request.Role}. Must be 'Shipper' or 'Salesman'." });

            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            try
            {
                await _communityAdminService.DeactivateRoleForTenantAsync(tenantId, customerId, roleType);

                _logger.LogInformation(
                    "DeactivateRole (Owner): {Role} deactivated for customer {CustomerId} of tenant {TenantId}",
                    roleType, customerId, tenantId);

                return Ok(new { deactivatedAt = DateTime.UtcNow });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("DeactivateRole (Owner) IDOR blocked: {Message}", ex.Message);
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("DeactivateRole (Owner) failed: {Message}", ex.Message);
                if (ex.Message.Contains("No active"))
                    return NotFound(new { error = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/v1/tenant-community/{customerId}/roles
        /// Get all community roles (active + inactive) for a customer of the calling tenant.
        /// IDOR guard: service throws UnauthorizedAccessException if customer.TenantId != JWT tenant_id.
        /// </summary>
        [HttpGet("{customerId}/roles")]
        public async Task<IActionResult> GetCustomerRoles(Guid customerId)
        {
            var tenantId = GetTenantIdFromClaim();
            if (tenantId == Guid.Empty)
                return Unauthorized(new { error = "Missing or invalid tenant_id claim." });

            try
            {
                var roles = await _communityAdminService.GetCustomerRolesForTenantAsync(tenantId, customerId);
                return Ok(roles.Select(r => new
                {
                    id = r.Id,
                    roleType = r.RoleType.ToString(),
                    isActive = r.IsActive,
                    activatedAt = r.ActivatedAt,
                    deactivatedAt = r.DeactivatedAt,
                    salesmanCode = r.SalesmanCode
                }));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("GetCustomerRoles (Owner) IDOR blocked: {Message}", ex.Message);
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("GetCustomerRoles (Owner) failed: {Message}", ex.Message);
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Read tenant_id from JWT claim. Returns Guid.Empty if missing or invalid.
        /// tenant_id claim is required by RequireOwnerRole policy — should always be present for Owner users.
        /// </summary>
        private Guid GetTenantIdFromClaim()
        {
            var tenantIdClaim = User.FindFirst("tenant_id")?.Value
                ?? User.FindFirst("tenantId")?.Value;
            return Guid.TryParse(tenantIdClaim, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Read owner user ID from JWT sub claim. Used as activatedBy in CommunityRole audit.
        /// </summary>
        private Guid GetOwnerUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }
    }
}
