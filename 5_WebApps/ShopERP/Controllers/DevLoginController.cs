using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// T-20: Development-only login endpoint for Playwright E2E tests.
    /// Issues a real Cookie auth session with a fixed TenantId + role claims,
    /// bypassing OIDC (which requires an external identity server unavailable in test env).
    ///
    /// SECURITY: This controller ONLY registers in Development environment (Program.cs guard).
    /// It is completely absent from Production/Staging builds.
    /// </summary>
    [ApiController]
    [Route("dev")]
    public class DevLoginController : ControllerBase
    {
        // Fixed test tenant — matches the seed tenant used by ShopERP dev SQLite DB
        private static readonly Guid TestTenantId = new("11111111-1111-1111-1111-111111111111");
        private const string TestUserEmail = "admin@vanan.vn";
        private const string TestUserName  = "Dev Admin";
        private const string TestRole      = "Owner";

        /// <summary>POST /dev/login — issues Cookie auth session for E2E tests.</summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,  TestUserName),
                new(ClaimTypes.Email, TestUserEmail),
                new(ClaimTypes.Role,  TestRole),
                // tenant_id — standard OIDC snake_case (read by HttpContextTenantProvider)
                new("tenant_id",      TestTenantId.ToString()),
                // Legacy claim name — dual-read support in HttpContextTenantProvider
                new("TenantId",       TestTenantId.ToString()),
                new("sub",            TestUserEmail),
                new("role",           TestRole),
            };

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true,
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);

            return Ok(new
            {
                success  = true,
                tenantId = TestTenantId,
                email    = TestUserEmail,
                role     = TestRole,
                message  = "Dev login successful — cookie issued",
            });
        }

        /// <summary>GET /dev/login — smoke-check the endpoint is reachable.</summary>
        [HttpGet("login")]
        public IActionResult LoginInfo() =>
            Ok(new
            {
                available = true,
                env       = "Development",
                note      = "POST to /dev/login to create an auth session for E2E tests",
            });

        /// <summary>POST /dev/logout — clears the dev session cookie.</summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { success = true });
        }
    }
}
