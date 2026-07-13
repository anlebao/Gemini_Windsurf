using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.ShopERP.Controllers;

/// <summary>
/// AM-T8: SystemAdmin tenant impersonation endpoints.
/// Allows cross-tenant SystemAdmin to select a tenant and act as that tenant
/// by setting the tenant_id claim in the auth cookie.
///
/// JWT re-issuance: Impersonate() also mints a new JWT with the selected tenant_id
/// so API clients (using Bearer token, not Cookie) can access Gateway tenant-scoped endpoints.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "SystemAdmin")]
public class AdminController : ControllerBase
{
    private readonly ITenantManagementService _tenantService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ITenantManagementService tenantService,
        IJwtTokenService jwtTokenService,
        ILogger<AdminController> logger)
    {
        _tenantService = tenantService;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    /// <summary>
    /// Impersonate a tenant — sets tenant_id claim in the auth cookie.
    /// SystemAdmin can then access tenant-scoped pages/data filtered by that TenantId.
    /// </summary>
    [HttpPost("impersonate/{tenantId:guid}")]
    public async Task<IActionResult> Impersonate(Guid tenantId)
    {
        var tenantIdValue = new TenantId(tenantId);

        // Validate tenant exists and is active
        var tenant = await _tenantService.GetTenantByIdAsync(tenantIdValue);
        if (tenant == null)
        {
            _logger.LogWarning("SystemAdmin attempted to impersonate non-existent tenant {TenantId}", tenantId);
            return NotFound(new { success = false, message = $"Tenant {tenantId} not found." });
        }

        if (tenant.Status != TenantStatus.Active)
        {
            _logger.LogWarning("SystemAdmin attempted to impersonate inactive tenant {TenantId} ({Status})", tenantId, tenant.Status);
            return BadRequest(new { success = false, message = $"Tenant '{tenant.Name}' is not active (status: {tenant.Status})." });
        }

        // Get current SystemAdmin identity
        var user = HttpContext.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var emailClaim = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("sub")?.Value ?? "sysadmin@vanan.vn";
        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value ?? "SystemAdmin";

        // Build new claims: copy existing + add tenant_id
        var claims = new List<Claim>(user.Claims.Where(c => c.Type != "tenant_id"))
        {
            new("tenant_id", tenantId.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            AllowRefresh = true,
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);

        // JWT re-issuance: mint a new JWT with the selected tenant_id so API clients
        // using Bearer token (not Cookie) can access Gateway tenant-scoped endpoints.
        // Without this, SystemAdmin JWT has tenant_id="system" (not a valid Guid) and
        // Gateway controllers reject it with 401 "Tenant ID required in JWT claim".
        Guid.TryParse(userIdClaim, out Guid parsedUserId);
        string impersonatedJwt = _jwtTokenService.GenerateToken(
            userId: parsedUserId,
            email: emailClaim,
            role: roleClaim,
            tenantId: tenantId);

        // EDR-AM-6: Log impersonation event
        _logger.LogInformation("IMPERSONATE | SystemAdmin {UserId} | TenantId={TenantId} | TenantName={TenantName}",
            userIdClaim, tenantId, tenant.Name);

        return Ok(new
        {
            success = true,
            tenantId = tenantId.ToString(),
            tenantName = tenant.Name,
            token = impersonatedJwt
        });
    }

    /// <summary>
    /// Exit impersonation — clears tenant_id claim, returns to cross-tenant SystemAdmin mode.
    /// </summary>
    [HttpPost("exit-impersonation")]
    public async Task<IActionResult> ExitImpersonation()
    {
        var user = HttpContext.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var currentTenantId = user.FindFirst("tenant_id")?.Value;

        // Build claims without tenant_id
        var claims = user.Claims
            .Where(c => c.Type != "tenant_id")
            .ToList();

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            AllowRefresh = true,
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties);

        // EDR-AM-6: Log exit event
        _logger.LogInformation("EXIT_IMPERSONATION | SystemAdmin {UserId} | Was TenantId={TenantId}",
            userIdClaim, currentTenantId);

        return Ok(new { success = true, message = "Exited impersonation" });
    }
}
