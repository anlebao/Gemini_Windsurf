using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [AllowAnonymous]
    public class SocialAuthController(
        IGoogleAuthService googleAuthService,
        ICustomerTokenService customerTokenService,
        ICustomerRepository customerRepository,
        ICustomerMergeService customerMergeService,
        VanAnDbContext vanAnPgDbContext,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<SocialAuthController> logger) : ControllerBase
    {
        private readonly IGoogleAuthService _googleAuthService = googleAuthService;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly ICustomerMergeService _customerMergeService = customerMergeService;
        private readonly VanAnDbContext _vanAnPgDbContext = vanAnPgDbContext;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<SocialAuthController> _logger = logger;

        [HttpGet("google/login")]
        public IActionResult GoogleLogin([FromQuery] string? redirectTo = null, [FromQuery] string? klOrigin = null)
        {
            var redirectUri = GetCallbackUrl();
            // Encode both redirectTo (device_token for merge) and klOrigin (KhachLink instance host
            // for tenant resolution) into the OAuth state param, delimited by '|'.
            // redirectTo and klOrigin are hostnames/GUIDs — never contain '|'.
            var state = BuildState(redirectTo, klOrigin);
            var authUrl = _googleAuthService.GetAuthorizationUrl(redirectUri, state);
            _logger.LogInformation("[GoogleAuth] Redirecting to Google consent: {Url}", authUrl);
            return Redirect(authUrl);
        }

        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback([FromQuery] string? code, [FromQuery] string? error, [FromQuery] string? state)
        {
            // Parse state: format "{redirectTo}|{klOrigin}" (either part may be empty).
            var (deviceIdForMerge, klOrigin) = ParseState(state);
            var khachLinkLoginUrl = ResolveKhachLinkLoginUrl(klOrigin);

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("[GoogleAuth] OAuth error: {Error}", error);
                return Redirect($"{khachLinkLoginUrl}?error={Uri.EscapeDataString(error)}&provider=google");
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("[GoogleAuth] Callback missing authorization code.");
                return Redirect($"{khachLinkLoginUrl}?error=missing_code&provider=google");
            }

            var redirectUri = GetCallbackUrl();
            _logger.LogInformation("[GoogleAuth] Callback received. Code={CodePrefix} RedirectUri={RedirectUri} KlOrigin={KlOrigin}", code[..Math.Min(10, code.Length)], redirectUri, klOrigin);
            var authResponse = await _googleAuthService.ExchangeCodeForUserInfoAsync(code, redirectUri);

            if (!authResponse.Success || authResponse.UserInfo == null)
            {
                var errorReason = authResponse.Error?.Reason ?? "unknown";
                var errorDetails = authResponse.Error?.Details ?? "No details available";
                _logger.LogError("[GoogleAuth] Failed: {Reason} — {Details}", errorReason, errorDetails);
                if (env.IsDevelopment())
                    return Problem(
                        title: $"Google auth failed: {errorReason}",
                        detail: errorDetails,
                        statusCode: 500);
                return Redirect($"{khachLinkLoginUrl}?error=auth_failed&provider=google");
            }

            var userInfo = authResponse.UserInfo;

            // Find customer by email (PII-encrypted — load all and filter in-memory)
            var allCustomers = await _customerRepository.GetAllActiveAsync();
            var customer = allCustomers.FirstOrDefault(c => c.Email == userInfo.Email);

            bool isNewCustomer = false;
            if (customer == null)
            {
                // Create new customer with IdentityLevel = Social (default).
                // Resolve owning tenant from the KhachLink instance the user logged in from
                // (klOrigin → KhachLinkInstance.OwnerTenantId). Falls back to the configured
                // default tenant when the instance is not found or is platform-level.
                var tenantId = await ResolveTenantIdFromOriginAsync(klOrigin);
                var newCustomer = new Customer(
                    new TenantId(tenantId),
                    userInfo.FullName,
                    string.Empty,
                    userInfo.Email);
                customer = await _customerRepository.AddAsync(newCustomer);
                isNewCustomer = true;
                _logger.LogInformation("[GoogleAuth] New customer created via Google: {CustomerId} Email={Email} Tenant={TenantId} (klOrigin={KlOrigin})", customer.Id, userInfo.Email, tenantId, klOrigin);
            }
            else
            {
                _logger.LogInformation("[GoogleAuth] Existing customer logged in via Google: {CustomerId} IdentityLevel={Level}", customer.Id, customer.IdentityLevel);
            }

            var token = _customerTokenService.CreateToken(customer.Id);

            // TD-CUSTSYNC-001 / Issue #106: Merge DeviceId-based guest stubs into login customer.
            // The deviceId portion of the state param carries the device_token (localStorage).
            if (!string.IsNullOrEmpty(deviceIdForMerge) && Guid.TryParse(deviceIdForMerge, out var deviceId))
            {
                try
                {
                    var mergeResult = await _customerMergeService.MergeDeviceStubsIntoLoginAsync(customer.Id, deviceId);
                    if (mergeResult.StubsMerged > 0)
                    {
                        _logger.LogInformation("[GoogleAuth] TD-CUSTSYNC-001: Merged {Stubs} guest stub(s), transferred {Points} points to customer {CustomerId}",
                            mergeResult.StubsMerged, mergeResult.PointsTransferred, customer.Id);
                    }
                }
                catch (Exception mergeEx)
                {
                    // Non-blocking: merge failure should NOT prevent login
                    _logger.LogWarning(mergeEx, "[GoogleAuth] TD-CUSTSYNC-001: Merge failed for customer {CustomerId} — login proceeds, merge deferred", customer.Id);
                }
            }

            var redirectUrl = $"{khachLinkLoginUrl}?token={Uri.EscapeDataString(token)}&provider=google&customerId={Uri.EscapeDataString(customer.Id.ToString())}";

            return Redirect(redirectUrl);
        }

        private string GetCallbackUrl()
        {
            var baseUrl = _configuration["Google:CallbackBaseUrl"] ?? "http://localhost:5003";
            return $"{baseUrl}/api/auth/google/callback";
        }

        private Guid GetDefaultTenantId()
        {
            var tenantIdStr = _configuration["Seed:TenantId"] ?? "00000000-0000-0000-0000-000000000001";
            return Guid.TryParse(tenantIdStr, out var id) ? id : Guid.Empty;
        }

        // ── Customer-onboarding: state encoding + tenant resolution ──────────────

        /// <summary>
        /// Encode redirectTo (device_token) and klOrigin (KhachLink instance host) into a single
        /// OAuth state string, delimited by '|'. Either part may be empty.
        /// </summary>
        private static string BuildState(string? redirectTo, string? klOrigin)
        {
            return $"{redirectTo ?? string.Empty}|{klOrigin ?? string.Empty}";
        }

        /// <summary>
        /// Parse the OAuth state string back into (redirectTo, klOrigin).
        /// Format: "{redirectTo}|{klOrigin}". Missing '|' → whole string is redirectTo (legacy).
        /// </summary>
        private static (string? RedirectTo, string? KlOrigin) ParseState(string? state)
        {
            if (string.IsNullOrEmpty(state))
                return (null, null);

            var idx = state.IndexOf('|');
            if (idx < 0)
                return (state, null); // legacy: whole string was device_token

            var redirectTo = idx > 0 ? state[..idx] : null;
            var klOrigin = idx < state.Length - 1 ? state[(idx + 1)..] : null;
            return (redirectTo, klOrigin);
        }

        /// <summary>
        /// Resolve the KhachLink login URL to redirect back to after OAuth callback.
        /// When klOrigin is present, build https://{klOrigin}/login (the instance the user
        /// started from). Otherwise fall back to the configured Google:KhachLinkLoginUrl.
        /// </summary>
        private string ResolveKhachLinkLoginUrl(string? klOrigin)
        {
            if (!string.IsNullOrWhiteSpace(klOrigin))
                return $"https://{klOrigin}/login";
            return _configuration["Google:KhachLinkLoginUrl"] ?? "http://localhost:5002/login";
        }

        /// <summary>
        /// Resolve the owning tenant for a new customer from the KhachLink instance domain.
        /// Queries KhachLinkInstance (Gateway PG) by CustomDomain → OwnerTenantId.
        /// Falls back to the configured default tenant when the instance is not found or is
        /// platform-level (OwnerTenantId == null).
        /// </summary>
        private async Task<Guid> ResolveTenantIdFromOriginAsync(string? klOrigin)
        {
            if (string.IsNullOrWhiteSpace(klOrigin))
                return GetDefaultTenantId();

            try
            {
                var domain = klOrigin.ToLowerInvariant();
                var instance = await _vanAnPgDbContext.KhachLinkInstances
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.CustomDomain == domain);

                if (instance != null && instance.OwnerTenantId.HasValue && instance.OwnerTenantId.Value != Guid.Empty)
                {
                    _logger.LogInformation("[GoogleAuth] Resolved tenant {TenantId} from KhachLinkInstance domain {Domain}", instance.OwnerTenantId.Value, domain);
                    return instance.OwnerTenantId.Value;
                }
            }
            catch (Exception ex)
            {
                // Non-blocking: tenant resolution failure should NOT prevent customer creation.
                _logger.LogWarning(ex, "[GoogleAuth] Failed to resolve tenant from klOrigin={KlOrigin} — using default tenant", klOrigin);
            }

            return GetDefaultTenantId();
        }

        // CC-S1-T0c (v1.5): Facebook OAuth stub endpoints.
        // Sprint 7+ will config real Facebook OAuth credentials (AppId + AppSecret).
        // For now, redirect back to KhachLink login with informative error.
        [HttpGet("facebook/login")]
        public IActionResult FacebookLogin([FromQuery] string? redirectTo = null)
        {
            var khachLinkLoginUrl = _configuration["Google:KhachLinkLoginUrl"] ?? "http://localhost:5002/login";
            _logger.LogWarning("[FacebookAuth] Login stub — Facebook OAuth credentials not configured. Redirecting to login with error.");
            return Redirect($"{khachLinkLoginUrl}?error=facebook_not_configured&provider=facebook");
        }

        [HttpGet("facebook/callback")]
        public IActionResult FacebookCallback([FromQuery] string? code, [FromQuery] string? error, [FromQuery] string? state)
        {
            var khachLinkLoginUrl = _configuration["Google:KhachLinkLoginUrl"] ?? "http://localhost:5002/login";
            _logger.LogWarning("[FacebookAuth] Callback stub — Facebook OAuth not configured. Redirecting to login.");
            return Redirect($"{khachLinkLoginUrl}?error=facebook_not_configured&provider=facebook");
        }
    }
}
