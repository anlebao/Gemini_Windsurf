using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.ShopERP.Pages;

/// <summary>
/// Issue #103 fix: Razor Page for SystemAdmin tenant impersonation.
/// Replaces the broken HttpClient-based flow (AdminController + CookieForwarding)
/// which failed because HttpContext is null in Blazor Server events and
/// Set-Cookie from HttpClient responses never reaches the browser.
///
/// This Razor Page runs in a real HTTP request context:
///   1. Validates tenant via Gateway HTTP (PG source of truth — Option C)
///   2. Re-issues auth cookie with tenant_id + Owner role + impersonating marker
///   3. Updates .VanAn.Jwt cookie with tenant-scoped JWT
///   4. Redirects to /sitemap
///
/// Browser receives Set-Cookie headers directly → impersonation takes effect.
/// Pattern follows Logout.cshtml (Razor Page for auth state changes).
/// </summary>
[Authorize(Policy = "SystemAdmin")]
[IgnoreAntiforgeryToken]
public class ImpersonateModel(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IJwtTokenService jwtTokenService,
    ILogger<ImpersonateModel> logger) : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IConfiguration _configuration = configuration;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
    private readonly ILogger<ImpersonateModel> _logger = logger;

    public string StatusMessage { get; private set; } = "Đang chuyển đổi sang tenant...";

    [BindProperty(SupportsGet = true)]
    public Guid TenantId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (TenantId == Guid.Empty)
        {
            _logger.LogWarning("Impersonate: empty tenantId in route");
            return RedirectToPage("/Login");
        }

        // 1. Validate tenant via Gateway HTTP (PG source of truth — Option C)
        var tenant = await GetTenantFromGatewayAsync(TenantId);
        if (tenant == null)
        {
            _logger.LogWarning("Impersonate: tenant {TenantId} not found in Gateway", TenantId);
            StatusMessage = $"Tenant {TenantId} không tồn tại.";
            return Page();
        }

        if (tenant.Status != TenantStatus.Active)
        {
            _logger.LogWarning("Impersonate: tenant {TenantId} not active ({Status})", TenantId, tenant.Status);
            StatusMessage = $"Tenant '{tenant.Name}' không hoạt động ({tenant.Status}).";
            return Page();
        }

        // 2. Build new claims: copy existing + add tenant_id + Owner role + impersonating marker
        var user = HttpContext.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? "unknown";
        var emailClaim = user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? "sysadmin@vanan.vn";

        var claims = new List<Claim>(
            user.Claims.Where(c => c.Type != "tenant_id"
                                 && c.Type != "TenantId"
                                 && c.Type != "impersonating"
                                 && c.Type != "impersonated_tenant_name"))
        {
            new("tenant_id", TenantId.ToString()),
            new("TenantId", TenantId.ToString()),
            new(ClaimTypes.Role, UserRole.Owner.ToString()),
            new("impersonating", "true"),
            new("impersonated_tenant_name", tenant.Name),
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

        // 3. Mint new JWT with tenant_id so API clients (Bearer token) access tenant-scoped endpoints
        Guid.TryParse(userIdClaim, out Guid parsedUserId);
        string impersonatedJwt = _jwtTokenService.GenerateToken(
            userId: parsedUserId,
            email: emailClaim,
            role: UserRole.Owner.ToString(),
            tenantId: TenantId);

        // 4. Update .VanAn.Jwt cookie (HttpOnly — same pattern as Login.cshtml.cs)
        Response.Cookies.Append(".VanAn.Jwt", impersonatedJwt, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddHours(8),
        });

        // 5. Audit log
        _logger.LogInformation(
            "IMPERSONATE | SystemAdmin {UserId} | TenantId={TenantId} | TenantName={TenantName}",
            userIdClaim, TenantId, tenant.Name);

        // 6. Redirect to /sitemap — Blazor re-reads auth state from cookie
        return Redirect("/sitemap");
    }

    /// <summary>
    /// Fetch tenant from Gateway PG via HTTP (Option C — PG is source of truth).
    /// Mints a short-lived SystemAdmin JWT from current user claims.
    /// </summary>
    private async Task<GatewayTenantDto?> GetTenantFromGatewayAsync(Guid tenantId)
    {
        string baseUrl = _configuration["Gateway:BaseUrl"] ?? "http://localhost:5001";
        // Ensure trailing slash so relative URI combines correctly
        // (e.g., "http://host:80" + "api/v1/..." → "http://host:80api/v1/..." without slash)
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";
        var client = _httpClientFactory.CreateClient("GatewayClient");
        client.BaseAddress = new Uri(baseUrl);

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

        // Gateway serializes enums as camelCase strings — use matching JsonSerializerOptions
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        return await response.Content.ReadFromJsonAsync<GatewayTenantDto>(jsonOptions);
    }

    /// <summary>Minimal DTO matching Gateway TenantsController.TenantDto.</summary>
    private sealed class GatewayTenantDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public TenantStatus Status { get; init; }
    }
}
