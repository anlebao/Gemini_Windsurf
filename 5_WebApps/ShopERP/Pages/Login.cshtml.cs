using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using BCrypt.Net;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.ShopERP.Pages
{
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("LoginRateLimit")]
    public class LoginModel(
        IAntiforgery antiforgery,
        IVanAnDbContext dbContext,
        IJwtTokenService jwtTokenService,
        IMfaService mfaService) : PageModel
    {
        private readonly IAntiforgery _antiforgery = antiforgery;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
        private readonly IMfaService _mfaService = mfaService;

        // MFA-1 (2026-11-10): tên cookie trusted device + config
        public const string TrustedMfaCookie = "va_mfa_trust";
        private const string PendingMfaKey = "PendingMfa";

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public bool RememberMe { get; set; }

        // ── MFA-1 step 2 ────────────────────────────────────────────────────
        [BindProperty]
        public string? MfaCode { get; set; }

        public bool MfaStep { get; set; }
        public string? MfaUsername { get; set; }
        public bool MfaEmailSent { get; set; }
        public bool MfaEmailAvailable { get; set; }

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
            Guid tenantId = userTenant?.TenantId ?? user.TenantId.Value;

            // MFA-1 (2026-11-10, user duyệt): Owner bật MFA → step 2 trước khi SignIn.
            // Trusted cookie hợp lệ → bỏ qua step 2 (đăng nhập thẳng).
            var trustedToken = Request.Cookies[TrustedMfaCookie];
            if (user.MfaEnabled && user.Role == UserRole.Owner)
            {
                if (!await _mfaService.IsTrustedAsync(user.Id.ToString(), trustedToken))
                {
                    TempData[PendingMfaKey] = JsonSerializer.Serialize(new PendingMfa(user.Id, RememberMe));
                    MfaUsername = user.Username;
                    MfaEmailAvailable = !string.IsNullOrWhiteSpace(user.Email);
                    MfaStep = true;
                    return Page();
                }
            }

            return await SignInUserAsync(user, tenantId, RememberMe);
        }

        /// <summary>MFA-1 step 2: verify TOTP hoặc email fallback → SignIn + set trusted cookie.</summary>
        public async Task<IActionResult> OnPostMfaAsync()
        {
            var pending = ReadPendingMfa();
            if (pending is null)
            {
                ModelState.AddModelError(string.Empty, "Phiên đăng nhập hết hạn — đăng nhập lại.");
                return Page();
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == pending.UserId && u.IsActive && !u.IsDeleted);

            if (user is null || !user.MfaEnabled)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản không hợp lệ.");
                return Page();
            }

            MfaUsername = user.Username;
            MfaEmailAvailable = !string.IsNullOrWhiteSpace(user.Email);
            MfaStep = true;

            var trustedToken = Request.Cookies[TrustedMfaCookie];
            var result = await _mfaService.VerifyLoginAsync(user.Id.ToString(), MfaCode, trustedToken);

            // Email fallback: nếu TOTP không hợp lệ, thử code email (single-use)
            if (result == MfaVerifyResult.Invalid && !string.IsNullOrWhiteSpace(MfaCode)
                && await _mfaService.VerifyEmailFallbackOtpAsync(user.Id.ToString(), MfaCode))
            {
                result = MfaVerifyResult.Success;
            }

            switch (result)
            {
                case MfaVerifyResult.Success:
                {
                    var userTenant = await _dbContext.UserTenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(ut => ut.UserId == user.Id && ut.IsActive);
                    Guid tenantId = userTenant?.TenantId ?? user.TenantId.Value;

                    // Trusted device cookie — 30 ngày (Mfa:TrustedDeviceDays)
                    var trusted = await _mfaService.IssueTrustedTokenAsync(user.Id.ToString());
                    var days = int.TryParse(HttpContext.RequestServices
                        .GetService<IConfiguration>()?.GetValue<string>("Mfa:TrustedDeviceDays"), out var d) && d > 0 ? d : 30;
                    Response.Cookies.Append(TrustedMfaCookie, trusted, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Lax,
                        Secure = Request.IsHttps,
                        Expires = DateTimeOffset.UtcNow.AddDays(days)
                    });

                    TempData.Remove(PendingMfaKey);
                    return await SignInUserAsync(user, tenantId, pending.RememberMe);
                }
                case MfaVerifyResult.Locked:
                    ModelState.AddModelError(string.Empty, "Quá nhiều lần thử sai. Vui lòng thử lại sau 15 phút.");
                    return Page();
                default:
                    ModelState.AddModelError(string.Empty, "Mã xác minh không đúng hoặc đã hết hạn.");
                    return Page();
            }
        }

        /// <summary>MFA-1: gửi email OTP fallback (Brevo 0đ) khi mất authenticator.</summary>
        public async Task<IActionResult> OnPostSendEmailOtpAsync()
        {
            var pending = ReadPendingMfa();
            if (pending is null)
            {
                ModelState.AddModelError(string.Empty, "Phiên đăng nhập hết hạn — đăng nhập lại.");
                return Page();
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == pending.UserId && u.IsActive && !u.IsDeleted);

            if (user is null || !user.MfaEnabled)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản không hợp lệ.");
                return Page();
            }

            MfaUsername = user.Username;
            MfaStep = true;
            MfaEmailAvailable = !string.IsNullOrWhiteSpace(user.Email);

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                ModelState.AddModelError(string.Empty, "Tài khoản chưa có email — liên hệ quản trị để reset MFA.");
                return Page();
            }

            var sent = await _mfaService.SendEmailFallbackOtpAsync(user.Id.ToString());
            if (!sent)
            {
                ModelState.AddModelError(string.Empty, "Không gửi được email xác minh — thử lại sau.");
                return Page();
            }

            MfaEmailSent = true;
            return Page();
        }

        /// <summary>SignIn DemoUser (JWT + cookie) — tách từ OnPostAsync cho MFA step 2 dùng lại.</summary>
        private async Task<IActionResult> SignInUserAsync(DemoUser user, Guid tenantId, bool rememberMe)
        {
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
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("DisplayName", user.DisplayName),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("TenantId", tenantId.ToString()),
                // Stable user identity for ShopInboxApiClient (voice/chat): without a GUID
                // sub/NameIdentifier, GetCurrentUserIdAsync returned Guid.Empty (dead call
                // button) and MintOwnerTokenAsync minted a random userId per request.
                new Claim("sub", user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? user.Username)
            ];

            // W4 (P1.1): client_ip/client_ua claims → audit IP/UserAgent trong Blazor circuit.
            SignInClaims.AppendClientInfo(claims, HttpContext);

            ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            AuthenticationProperties authProperties = new()
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
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
                Expires = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            });

            // Redirect to Blazor /sitemap — single entry point for all roles
            return Redirect("/sitemap");
        }

        private PendingMfa? ReadPendingMfa()
        {
            var raw = TempData[PendingMfaKey] as string;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                return JsonSerializer.Deserialize<PendingMfa>(raw);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private sealed record PendingMfa(Guid UserId, bool RememberMe);

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

            // W4 (P1.1): client_ip/client_ua claims → audit IP/UserAgent trong Blazor circuit.
            SignInClaims.AppendClientInfo(claims, HttpContext);

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
