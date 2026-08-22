using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Phase 6: Shared base class for ShopERP API clients that call Gateway admin endpoints.
    /// Mints a short-lived SystemAdmin JWT for the current user (same pattern as TenantOnboardingApiClient).
    /// Reduces duplication across FeaturedProductApiClient, ShopInstanceApiClient, etc.
    /// </summary>
    public abstract class GatewayAdminApiClientBase
    {
        protected readonly HttpClient HttpClient;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly ILogger _logger;

        protected GatewayAdminApiClientBase(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger logger)
        {
            HttpClient = httpClientFactory.CreateClient("GatewayClient");
            _jwtTokenService = jwtTokenService;
            _authStateProvider = authStateProvider;
            _logger = logger;

            string baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5001";
            HttpClient.BaseAddress = new Uri(baseUrl);
        }

        /// <summary>Mint a short-lived SystemAdmin JWT for the current authenticated user.</summary>
        protected async Task<string> MintSystemAdminTokenAsync()
        {
            AuthenticationState authState = await _authStateProvider.GetAuthenticationStateAsync();
            ClaimsPrincipal user = authState.User;

            string userId = user.FindFirst("sub")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Guid.NewGuid().ToString();

            string email = user.FindFirst("email")?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value
                ?? "systemadmin@vanan.vn";

            // VA-FI-MVP2 Bug fix: extract REAL tenant_id from current user's claims (not Guid.Empty).
            // Guid.Empty → JWT has tenant_id=00000000-0000-0000-0000-000000000000 → Gateway controller
            // returns 401 "Missing tenant_id claim" → all Financial Intelligence UI calls fail silently.
            // Dual-read: "tenant_id" (snake_case OIDC) + legacy "TenantId" (PascalCase).
            string? tenantIdStr = user.FindFirst("tenant_id")?.Value
                ?? user.FindFirst("TenantId")?.Value;
            Guid tenantId = Guid.TryParse(tenantIdStr, out Guid tid) ? tid : Guid.Empty;

            _logger.LogDebug("Minting SystemAdmin JWT for user {UserId} ({Email}), tenant {TenantId}", userId, email, tenantId);

            return _jwtTokenService.GenerateToken(
                Guid.TryParse(userId, out Guid id) ? id : Guid.NewGuid(),
                email,
                "SystemAdmin",
                tenantId);
        }

        /// <summary>Create an HttpRequestMessage with SystemAdmin Bearer auth.</summary>
        protected async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativeUri, object? body = null)
        {
            string token = await MintSystemAdminTokenAsync();
            var request = new HttpRequestMessage(method, relativeUri);
            request.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
            if (body != null)
            {
                request.Content = JsonContent.Create(body, options: new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) }
                });
            }
            return request;
        }

        // Fix #101: Gateway serializes enums as strings (camelCase) via System.Text.Json defaults.
        // ShopERP's HttpClient must use matching options (JsonStringEnumConverter + CamelCase)
        // to deserialize DTOs containing enums (e.g., TenantDto.BusinessType).
        protected static readonly JsonSerializerOptions GatewayJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        protected static async Task<T?> SendAndReadAsync<T>(HttpClient client, HttpRequestMessage request, CancellationToken ct = default)
        {
            HttpResponseMessage response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(GatewayJsonOptions, ct);
        }
    }
}
