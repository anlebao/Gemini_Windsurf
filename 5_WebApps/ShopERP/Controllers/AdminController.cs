using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
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
///
/// Option C fix (2026-07-23): Tenant validation delegates to Gateway HTTP
/// (GET /api/v1/tenants/{id}) because Gateway PG is the Single Source of Truth for Tenants.
/// ShopERP SQLite has no Tenants table — querying it locally caused 500 errors.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "SystemAdmin")]
public class AdminController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        ILogger<AdminController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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
        // Option C fix: validate tenant via Gateway HTTP (PG source of truth) — not local SQLite.
        var tenant = await GetTenantFromGatewayAsync(tenantId);
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

    /// <summary>
    /// Option C fix: fetch tenant from Gateway PG via HTTP instead of querying local SQLite.
    /// Mints a short-lived SystemAdmin JWT from the current user's claims and calls
    /// GET /api/v1/tenants/{tenantId} on the Gateway.
    /// Returns null if tenant not found or Gateway unreachable.
    /// </summary>
    private async Task<GatewayTenantDto?> GetTenantFromGatewayAsync(Guid tenantId)
    {
        string baseUrl = _configuration["Gateway:BaseUrl"] ?? "http://localhost:5001";
        var client = _httpClientFactory.CreateClient("GatewayClient");
        client.BaseAddress = new Uri(baseUrl);

        // Mint a SystemAdmin JWT from current user claims so Gateway authorizes the call.
        var user = HttpContext.User;
        string userId = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Guid.NewGuid().ToString();
        string email = user.FindFirst("email")?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? "sysadmin@vanan.vn";

        string token = _jwtTokenService.GenerateToken(
            Guid.TryParse(userId, out Guid id) ? id : Guid.NewGuid(),
            email,
            "SystemAdmin",
            Guid.Empty);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/tenants/{tenantId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _logger.LogInformation("Impersonate: calling Gateway GET {BaseUrl}api/v1/tenants/{TenantId}", baseUrl, tenantId);

        HttpResponseMessage response = await client.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GatewayTenantDto>();
    }

    /// <summary>Minimal DTO matching Gateway TenantsController.TenantDto (fields used by Impersonate).</summary>
    private sealed class GatewayTenantDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public TenantStatus Status { get; init; }
    }
}
