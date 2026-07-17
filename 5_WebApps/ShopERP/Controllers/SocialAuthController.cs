using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using VanAn.CoreHub.Domain.Repositories;
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
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<SocialAuthController> logger) : ControllerBase
    {
        private readonly IGoogleAuthService _googleAuthService = googleAuthService;
        private readonly ICustomerTokenService _customerTokenService = customerTokenService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<SocialAuthController> _logger = logger;

        [HttpGet("google/login")]
        public IActionResult GoogleLogin([FromQuery] string? redirectTo = null)
        {
            var redirectUri = GetCallbackUrl();
            var authUrl = _googleAuthService.GetAuthorizationUrl(redirectUri, redirectTo);
            _logger.LogInformation("[GoogleAuth] Redirecting to Google consent: {Url}", authUrl);
            return Redirect(authUrl);
        }

        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback([FromQuery] string? code, [FromQuery] string? error, [FromQuery] string? state)
        {
            var khachLinkLoginUrl = _configuration["Google:KhachLinkLoginUrl"] ?? "http://localhost:5002/login";

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
            _logger.LogInformation("[GoogleAuth] Callback received. Code={CodePrefix} RedirectUri={RedirectUri}", code[..Math.Min(10, code.Length)], redirectUri);
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
                // Create new customer with IdentityLevel = Social (default)
                var defaultTenantId = GetDefaultTenantId();
                var newCustomer = new Customer(
                    new TenantId(defaultTenantId),
                    userInfo.FullName,
                    string.Empty,
                    userInfo.Email);
                customer = await _customerRepository.AddAsync(newCustomer);
                isNewCustomer = true;
                _logger.LogInformation("[GoogleAuth] New customer created via Google: {CustomerId} Email={Email}", customer.Id, userInfo.Email);
            }
            else
            {
                _logger.LogInformation("[GoogleAuth] Existing customer logged in via Google: {CustomerId} IdentityLevel={Level}", customer.Id, customer.IdentityLevel);
            }

            var token = _customerTokenService.CreateToken(customer.Id);

            var redirectUrl = $"{khachLinkLoginUrl}?token={Uri.EscapeDataString(token)}&provider=google&customerId={Uri.EscapeDataString(customer.Id.ToString())}";
            if (!string.IsNullOrEmpty(state))
                redirectUrl += $"&redirectTo={Uri.EscapeDataString(state)}";

            return Redirect(redirectUrl);
        }

        private string GetCallbackUrl()
        {
            var baseUrl = _configuration["Google:CallbackBaseUrl"] ?? "http://localhost:5003";
            return $"{baseUrl}/api/auth/google/callback";
        }

        private static Guid GetDefaultTenantId()
        {
            return Guid.TryParse("00000000-0000-0000-0000-000000000001", out var id) ? id : Guid.Empty;
        }
    }
}
