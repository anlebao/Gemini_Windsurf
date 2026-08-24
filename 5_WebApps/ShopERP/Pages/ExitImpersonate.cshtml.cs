using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Pages;

/// <summary>
/// Issue #103 fix: Razor Page for exiting SystemAdmin tenant impersonation.
/// Removes tenant_id + Owner role + impersonating marker from auth cookie,
/// re-issues .VanAn.Jwt with tenant_id=Guid.Empty (platform mode),
/// and redirects back to /admin/tenants.
///
/// Pattern follows Logout.cshtml (Razor Page for auth state changes).
/// </summary>
[Authorize]
[IgnoreAntiforgeryToken]
public class ExitImpersonateModel(
    IJwtTokenService jwtTokenService,
    ILogger<ExitImpersonateModel> logger) : PageModel
{
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
    private readonly ILogger<ExitImpersonateModel> _logger = logger;

    public string StatusMessage { get; private set; } = "Đang thoát impersonation...";

    public async Task<IActionResult> OnGetAsync()
    {
        var user = HttpContext.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? "unknown";
        var emailClaim = user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? "sysadmin@vanan.vn";
        var currentTenantId = user.FindFirst("tenant_id")?.Value;
        var wasImpersonating = user.FindFirst("impersonating")?.Value == "true";

        if (!wasImpersonating)
        {
            _logger.LogWarning("ExitImpersonate: user {UserId} not impersonating — redirect to /sitemap", userIdClaim);
            return Redirect("/sitemap");
        }

        // Build cleaned claims: remove tenant_id, TenantId, Owner role, impersonating marker, tenant name
        var claims = user.Claims
            .Where(c => c.Type != "tenant_id"
                     && c.Type != "TenantId"
                     && c.Type != "impersonating"
                     && c.Type != "impersonated_tenant_name"
                     && !(c.Type == ClaimTypes.Role && c.Value == "Owner"))
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

        // Re-issue .VanAn.Jwt with tenant_id=Guid.Empty (platform mode — same as SystemAdmin login)
        Guid.TryParse(userIdClaim, out Guid parsedUserId);
        string platformJwt = _jwtTokenService.GenerateToken(
            userId: parsedUserId,
            email: emailClaim,
            role: "SystemAdmin",
            tenantId: Guid.Empty);

        Response.Cookies.Append(".VanAn.Jwt", platformJwt, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddHours(8),
        });

        // Audit log
        _logger.LogInformation(
            "EXIT_IMPERSONATION | SystemAdmin {UserId} | Was TenantId={TenantId}",
            userIdClaim, currentTenantId);

        return Redirect("/admin/tenants");
    }
}
