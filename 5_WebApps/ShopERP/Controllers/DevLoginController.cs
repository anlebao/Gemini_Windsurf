using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Common;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

#if DEBUG
namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// T-20: Development-only login endpoint for Playwright E2E tests.
    /// Issues a real Cookie auth session with a fixed TenantId + role claims,
    /// bypassing OIDC (which requires an external identity server unavailable in test env).
    ///
    /// Wave 0: Also issues JWT token in response body for E2E tests that need Bearer auth.
    /// Wave 5: Added SystemAdmin support for platform-level testing.
    ///
    /// SECURITY (W5 hardening): This controller is wrapped in <c>#if DEBUG</c> so the entire
    /// class is compiled out of Release builds. This is a compile-time guarantee — the
    /// controller cannot exist in any Production/Staging binary regardless of runtime
    /// environment configuration. The <c>VanAn.Architecture.Tests</c> suite enforces this
    /// via <c>DevLoginControllerReleaseBuildGuardTests</c>.
    /// </summary>
    [ApiController]
    [Route("dev")]
    public class DevLoginController(IJwtTokenService jwtTokenService) : ControllerBase
    {
        private readonly IJwtTokenService _jwtTokenService = jwtTokenService;

        // Fixed test tenant — matches the seed tenant used by ShopERP dev SQLite DB
        private static readonly Guid TestTenantId = new("11111111-1111-1111-1111-111111111111");
        private static readonly Guid TestUserId   = new("11111111-1111-1111-1111-111111111111");
        private const string TestUserEmail = "admin@vanan.vn";
        private const string TestUserName  = "Dev Admin";
        private const string TestRole      = "Owner";

        /// <summary>POST /dev/login — issues Cookie auth session + JWT token for E2E tests.</summary>
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

            // Wave 0: Issue JWT token for E2E tests that call API endpoints via Bearer token
            var jwtToken = _jwtTokenService.GenerateToken(
                userId: TestUserId,
                email: TestUserEmail,
                role: UserRole.Owner,
                tenantId: TestTenantId);

            return Ok(new
            {
                success  = true,
                tenantId = TestTenantId,
                email    = TestUserEmail,
                role     = TestRole,
                token    = jwtToken,
                message  = "Dev login successful — cookie and JWT issued",
            });
        }

        /// <summary>GET /dev/login — smoke-check the endpoint is reachable.</summary>
        [HttpGet("login")]
        public IActionResult LoginInfo() =>
            Ok(new
            {
                available = true,
                env       = "Development",
                note      = "POST to /dev/login to create an auth session + get JWT token for E2E tests",
            });

        /// <summary>POST /dev/logout — clears the dev session cookie.</summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { success = true });
        }

        /// <summary>POST /dev/login/{role} — issues Cookie auth session for a specific tenant role.</summary>
        /// <remarks>SaaS W3: Enables multi-role RBAC E2E tests (Staff, StoreKeeper, Guard).</remarks>
        [HttpPost("login/{role}")]
        public async Task<IActionResult> LoginAsRole(string role)
        {
            // Map route slug → (UserRole enum, display name)
            var (userRole, displayName) = role.ToLowerInvariant() switch
            {
                "staff"       => (UserRole.Staff,       "Dev Staff"),
                "storekeeper" => (UserRole.StoreKeeper, "Dev StoreKeeper"),
                "guard"       => (UserRole.Guard,       "Dev Guard"),
                "owner"       => (UserRole.Owner,       "Dev Owner"),
                _             => (UserRole.None,        "Unknown"),
            };

            if (userRole == UserRole.None)
            {
                return BadRequest(new { success = false, message = $"Unknown role '{role}'. Valid: owner, staff, storekeeper, guard." });
            }

            var roleString = userRole.ToString();

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,  displayName),
                new(ClaimTypes.Email, TestUserEmail),
                new(ClaimTypes.Role,  roleString),
                new("tenant_id",      TestTenantId.ToString()),
                new("TenantId",       TestTenantId.ToString()),
                new("sub",            TestUserEmail),
                new("role",           roleString),
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

            var jwtToken = _jwtTokenService.GenerateToken(
                userId: TestUserId,
                email: TestUserEmail,
                role: userRole,
                tenantId: TestTenantId);

            return Ok(new
            {
                success  = true,
                tenantId = TestTenantId,
                email    = TestUserEmail,
                role     = roleString,
                token    = jwtToken,
                message  = $"{roleString} dev login successful — cookie and JWT issued",
            });
        }

        /// <summary>POST /dev/login/systemadmin — SystemAdmin login for platform-level testing.</summary>
        [HttpPost("login/systemadmin")]
        public async Task<IActionResult> LoginAsSystemAdmin()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,  "System Admin"),
                new(ClaimTypes.Email, "systemadmin@vanan.vn"),
                new(ClaimTypes.Role,  PlatformRole.SystemAdmin.ToString()),
                // SystemAdmin doesn't need tenant_id (cross-tenant)
                new("sub", "systemadmin@vanan.vn"),
                new("role", PlatformRole.SystemAdmin.ToString()),
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

            // SystemAdmin JWT without tenant constraint
            var jwtToken = _jwtTokenService.GenerateToken(
                userId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
                email: "systemadmin@vanan.vn",
                role: PlatformRole.SystemAdmin.ToString(),
                tenantId: Guid.Empty);  // SystemAdmin has no tenant

            return Ok(new
            {
                success  = true,
                email    = "systemadmin@vanan.vn",
                role     = PlatformRole.SystemAdmin.ToString(),
                token    = jwtToken,
                message  = "SystemAdmin login successful — cross-tenant access granted",
            });
        }

        /// <summary>POST /dev/login/systemadmin/{tenantId} — SystemAdmin impersonate a specific tenant.</summary>
        /// <remarks>
        /// Entry point check fix (Nhóm 3B): SystemAdmin cross-tenant login issues JWT with tenant_id="system"
        /// which fails Guid.TryParse in Gateway controllers → 401. This endpoint issues Cookie + JWT with
        /// the real tenant_id GUID so SystemAdmin can access tenant-scoped endpoints after impersonation.
        /// </remarks>
        [HttpPost("login/systemadmin/{tenantId:guid}")]
        public async Task<IActionResult> LoginAsSystemAdminForTenant(Guid tenantId)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,  "System Admin"),
                new(ClaimTypes.Email, "systemadmin@vanan.vn"),
                new(ClaimTypes.Role,  PlatformRole.SystemAdmin.ToString()),
                // Impersonated tenant_id — real GUID so Gateway controllers can parse it
                new("tenant_id",      tenantId.ToString()),
                new("TenantId",       tenantId.ToString()),
                new("sub",            "systemadmin@vanan.vn"),
                new("role",           PlatformRole.SystemAdmin.ToString()),
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

            // Issue JWT with real tenant_id GUID (not Guid.Empty → "system")
            var jwtToken = _jwtTokenService.GenerateToken(
                userId:   Guid.Parse("00000000-0000-0000-0000-000000000001"),
                email:    "systemadmin@vanan.vn",
                role:     PlatformRole.SystemAdmin.ToString(),
                tenantId: tenantId);

            return Ok(new
            {
                success   = true,
                tenantId  = tenantId,
                email     = "systemadmin@vanan.vn",
                role      = PlatformRole.SystemAdmin.ToString(),
                token     = jwtToken,
                message   = $"SystemAdmin impersonation successful — tenant {tenantId} cookie and JWT issued",
            });
        }

        /// <summary>POST /dev/login/vas — issues Cookie auth session for the VAS Enterprise seed tenant.</summary>
        /// <remarks>
        /// W8 smoke-test convenience: the default <c>/dev/login</c> endpoint issues a session for
        /// tenant <c>11111111-1111-1111-1111-111111111111</c>, which has NO VAS seed data. The VAS
        /// sample data seeder (<see cref="CoreHub.Infrastructure.Seed.VasSampleDataSeeder"/>) seeds
        /// 31 journal entries + ~50 AccountingEntries for tenant
        /// <c>a5b6c7d8-1234-5678-9abc-def012345678</c> (Enterprise_SME, TT 133/2016).
        /// This endpoint lets a developer log in as that tenant to verify the 4 BCTC reports
        /// (Balance Sheet, Income Statement, Cash Flow, Trial Balance) against real seeded data
        /// without needing to wire up a second seed tenant.
        /// </remarks>
        [HttpPost("login/vas")]
        public async Task<IActionResult> LoginAsVasTenant()
        {
            // VAS Enterprise seed tenant — matches VasSampleDataSeeder.VasEnterpriseTenantGuid
            var vasTenantId = new Guid("a5b6c7d8-1234-5678-9abc-def012345678");
            var vasUserId   = new Guid("a5b6c7d8-1234-5678-9abc-def012345678");
            const string vasUserEmail = "vas-admin@vanan.vn";
            const string vasUserName  = "VAS Dev Admin";
            const string vasRole      = "Owner";

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,  vasUserName),
                new(ClaimTypes.Email, vasUserEmail),
                new(ClaimTypes.Role,  vasRole),
                new("tenant_id",      vasTenantId.ToString()),
                new("TenantId",       vasTenantId.ToString()),
                new("sub",            vasUserEmail),
                new("role",           vasRole),
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

            var jwtToken = _jwtTokenService.GenerateToken(
                userId:   vasUserId,
                email:    vasUserEmail,
                role:     UserRole.Owner,
                tenantId: vasTenantId);

            return Ok(new
            {
                success    = true,
                tenantId   = vasTenantId,
                tenantName = "Vạn An Trading Co. (DN vừa TT 133)",
                tenantType = "Enterprise_SME",
                standard   = "TT133_2016",
                email      = vasUserEmail,
                role       = vasRole,
                token      = jwtToken,
                message    = "VAS Enterprise tenant dev login successful — cookie and JWT issued (seed data available for 4 BCTC reports)",
            });
        }
    }
}
#endif
