using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.ShopERP.Pages
{
    [ValidateAntiForgeryToken]
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
                ModelState.AddModelError(string.Empty, "Email hoặc password không đúng");
                return Page();
            }

            // Lookup tenant mapping from UserTenant table
            var userTenant = await _dbContext.UserTenants
                .AsNoTracking()
                .FirstOrDefaultAsync(ut => ut.UserId == user.Id && ut.IsActive);

            // Fallback to default tenant if no mapping exists (E2E testing / dev)
            Guid tenantId = userTenant?.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

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
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Set JWT in HttpOnly cookie (.VanAn.Jwt) for API calls via Bearer token
            Response.Cookies.Append(".VanAn.Jwt", jwtToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            });

            // Redirect based on role — role-specific landing pages [Wave4-T5]
            return user.Role switch
            {
                UserRole.Guard => RedirectToPage("/Guard/Scan"),
                UserRole.Owner => RedirectToPage("/Index"),
                UserRole.StoreKeeper => RedirectToPage("/Index"),
                UserRole.Staff => RedirectToPage("/Kitchen/Index"),
                UserRole.Masterchef => RedirectToPage("/Kitchen/Index"),
                UserRole.None => throw new NotImplementedException(),
                _ => RedirectToPage("/Index")
            };
        }
    }
}
