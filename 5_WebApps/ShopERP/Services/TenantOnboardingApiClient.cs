using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Onboarding;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Wave 5: ShopERP client for the Gateway tenant onboarding API.
    /// Mints a short-lived SystemAdmin JWT for the current user and calls
    /// POST /api/v1/onboarding/tenants on the configured Gateway base URL.
    /// </summary>
    public sealed class TenantOnboardingApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly ILogger<TenantOnboardingApiClient> _logger;

        public TenantOnboardingApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<TenantOnboardingApiClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("GatewayClient");
            _jwtTokenService = jwtTokenService;
            _authStateProvider = authStateProvider;
            _logger = logger;

            string baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5001";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        /// <summary>
        /// Calls the Gateway onboarding endpoint with a SystemAdmin Bearer token.
        /// </summary>
        public async Task<TenantOnboardingResult> OnboardAsync(
            OnboardTenantRequest request,
            CancellationToken ct = default)
        {
            string token = await MintSystemAdminTokenAsync();
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "api/v1/onboarding/tenants")
            {
                Content = JsonContent.Create(request, options: new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                })
            };
            requestMessage.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);

            _logger.LogInformation("Calling Gateway onboarding API for tenant '{TenantName}'", request.Name);

            HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, ct);
            response.EnsureSuccessStatusCode();

            TenantOnboardingResult? result = await response.Content.ReadFromJsonAsync<TenantOnboardingResult>(ct);
            return result ?? throw new InvalidOperationException("Gateway returned an empty onboarding response.");
        }

        private async Task<string> MintSystemAdminTokenAsync()
        {
            AuthenticationState authState = await _authStateProvider.GetAuthenticationStateAsync();
            ClaimsPrincipal user = authState.User;

            string userId = user.FindFirst("sub")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Guid.NewGuid().ToString();

            string email = user.FindFirst("email")?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value
                ?? "systemadmin@vanan.vn";

            _logger.LogInformation("Minting SystemAdmin JWT for user {UserId} ({Email})", userId, email);

            return _jwtTokenService.GenerateToken(
                Guid.TryParse(userId, out Guid id) ? id : Guid.NewGuid(),
                email,
                "SystemAdmin",
                Guid.Empty);
        }
    }
}
