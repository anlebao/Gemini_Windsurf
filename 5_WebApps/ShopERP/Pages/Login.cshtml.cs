using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.ShopERP.Pages
{
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("LoginRateLimit")]
    public class LoginModel(
        IAntiforgery antiforgery,
        IVanAnDbContext dbContext,
        IJwtTokenService jwtTokenService) : PageModel
    {
        private readonly IAntiforgery _antiforgery = antiforgery;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IJwtTokenService _jwtTokenService = jwtTokenService;

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public bool RememberMe { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Lookup user from database by username (case-insensitive)
            // IgnoreQueryFilters: bypass global TenantId filter — login page has no JWT yet
            var user = await _dbContext.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username.ToLower() == Username.ToLower() && u.IsActive && !u.IsDeleted);

            // BCrypt verify: constant-time comparison, timing-attack resistant
            if (user == null || !BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash))
            {
                // Fix #3: Fall back to PlatformUsers table for SystemAdmin login via UI form.
                // SystemAdmin is NOT in the tenant-scoped Users table — they live in PlatformUsers.
                var platformUser = await TryLoginAsPlatformUserAsync(Username, Password);
                if (platformUser != null)
                {
                    return await SignInPlatformUserAsync(platformUser);
                }

                ModelState.AddModelError(string.Empty, "Email hoặc password không đúng");
                return Page();
            }

            // Lookup tenant mapping from UserTenant table
            var userTenant = await _dbContext.UserTenants
                .AsNoTracking()
                .FirstOrDefaultAsync(ut => ut.UserId == user.Id && ut.IsActive);

            // Bug fix: Fallback to user.TenantId (set during user creation) instead of hardcoded GUID.
            // Previously: hardcoded 00000000-...-001, causing users created for other tenants to
            // always login with the wrong tenant_id → see wrong tenant's products/orders.
            Guid tenantId = userTenant?.TenantId ?? user.TenantId.Value;

            // Issue JWT token with full claims
            var jwtToken = _jwtTokenService.GenerateToken(
                userId: user.Id,
                email: user.Username,
                role: user.Role,
                tenantId: tenantId);

            // Cookie claims for Blazor Server-side UI
            List<Claim> claims =
            [
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("DisplayName", user.DisplayName),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("TenantId", tenantId.ToString())
            ];

            ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            AuthenticationProperties authProperties = new()
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Set JWT in HttpOnly cookie (.VanAn.Jwt) for API calls via Bearer token
            // JWT expiry matches cookie expiry — 30 days if Remember Me, 8h otherwise
            Response.Cookies.Append(".VanAn.Jwt", jwtToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = Request.IsHttps,
                Expires = RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            });

            // Redirect to Blazor /sitemap — single entry point for all roles
            // Blazor Sitemap.razor handles role-based navigation (Owner, Staff, Guard, etc.)
            // Guard users: /sitemap has link to /guard/scan (legacy Razor Page, no Blazor equivalent yet)
            // Kitchen users: /sitemap has link to /Kitchen (legacy Razor Page, no Blazor equivalent yet)
            return Redirect("/sitemap");
        }

        /// <summary>
        /// Fix #3: Try to authenticate as a platform-level SystemAdmin.
        /// SystemAdmin users live in the PlatformUsers table (not tenant-scoped Users).
        /// </summary>
        private async Task<PlatformUser?> TryLoginAsPlatformUserAsync(string username, string password)
        {
            var platformUser = await _dbContext.PlatformUsers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && u.IsActive);

            if (platformUser == null || !BCrypt.Net.BCrypt.Verify(password, platformUser.PasswordHash))
                return null;

            return platformUser;
        }

        /// <summary>
        /// Fix #3: Sign in SystemAdmin with platform-level claims (no tenant_id) and redirect to /sitemap.
        /// SystemAdmin has no tenant until they impersonate one via /admin/tenants.
        /// </summary>
        private async Task<IActionResult> SignInPlatformUserAsync(PlatformUser platformUser)
        {
            var role = PlatformRole.SystemAdmin.ToString();

            // JWT with tenant_id=Guid.Empty (platform mode — no tenant until impersonation)
            var jwtToken = _jwtTokenService.GenerateToken(
                userId: platformUser.Id,
                email: platformUser.Email ?? platformUser.Username,
                role: role,
                tenantId: Guid.Empty);

            // Cookie claims — NO tenant_id (SystemAdmin must impersonate to access tenant data)
            List<Claim> claims =
            [
                new Claim(ClaimTypes.Name, platformUser.DisplayName),
                new Claim(ClaimTypes.NameIdentifier, platformUser.Id.ToString()),
                new Claim(ClaimTypes.Email, platformUser.Email ?? platformUser.Username),
                new Claim(ClaimTypes.Role, role),
                new Claim("DisplayName", platformUser.DisplayName),
                new Claim("sub", platformUser.Email ?? platformUser.Username),
                new Claim("role", role),
            ];

            ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            AuthenticationProperties authProperties = new()
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            Response.Cookies.Append(".VanAn.Jwt", jwtToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = Request.IsHttps,
                Expires = RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            });

            // Redirect to /sitemap — SystemAdmin landing page (platform overview)
            return Redirect("/sitemap");
        }
    }
}
