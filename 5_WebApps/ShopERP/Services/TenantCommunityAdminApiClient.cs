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
    /// R2 (2026-09-04): ShopERP client for Gateway Tenant-Community Admin APIs (Owner-scoped).
    /// Calls /api/v1/tenant-community/* with Owner Bearer JWT (tenant_id claim + Owner role).
    /// DIFFERS from CommunityAdminApiClient (which mints SystemAdmin JWT for /api/admin/community/*).
    /// Reuses EligibleCustomersResult + EligibleCustomerItem + ActivateRoleResult DTOs from CommunityAdminApiClient.
    /// </summary>
    public sealed class TenantCommunityAdminApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly ILogger<TenantCommunityAdminApiClient> _logger;

        public TenantCommunityAdminApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<TenantCommunityAdminApiClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("GatewayClient");
            _jwtTokenService = jwtTokenService;
            _authStateProvider = authStateProvider;
            _logger = logger;

            string baseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5001";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public async Task<EligibleCustomersResult> GetEligibleAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/v1/tenant-community/eligible?page={page}&pageSize={pageSize}");
            return await SendAndReadAsync<EligibleCustomersResult>(_httpClient, req, ct) ?? new();
        }

        public async Task<ActivateRoleResult> ActivateRoleAsync(Guid customerId, string role, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/tenant-community/{customerId}/activate-role", new { Role = role });
            return await SendAndReadAsync<ActivateRoleResult>(_httpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned empty response.");
        }

        public async Task DeactivateRoleAsync(Guid customerId, string role, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/tenant-community/{customerId}/deactivate-role", new { Role = role });
            var resp = await _httpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        public async Task<List<CustomerRoleItem>> GetCustomerRolesAsync(Guid customerId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/v1/tenant-community/{customerId}/roles");
            return await SendAndReadAsync<List<CustomerRoleItem>>(_httpClient, req, ct) ?? new();
        }

        /// <summary>
        /// Mint a short-lived Owner JWT for the current authenticated user.
        /// Reads tenant_id from current user's claims — required by RequireOwnerRole policy.
        /// </summary>
        private async Task<string> MintOwnerTokenAsync()
        {
            AuthenticationState authState = await _authStateProvider.GetAuthenticationStateAsync();
            ClaimsPrincipal user = authState.User;

            string userId = user.FindFirst("sub")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Guid.NewGuid().ToString();

            string email = user.FindFirst("email")?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value
                ?? "owner@vanan.vn";

            string? tenantIdStr = user.FindFirst("tenant_id")?.Value
                ?? user.FindFirst("TenantId")?.Value;
            Guid tenantId = Guid.TryParse(tenantIdStr, out Guid tid) ? tid : Guid.Empty;

            _logger.LogDebug("Minting Owner JWT for user {UserId} ({Email}), tenant {TenantId}", userId, email, tenantId);

            return _jwtTokenService.GenerateToken(
                Guid.TryParse(userId, out Guid id) ? id : Guid.NewGuid(),
                email,
                "Owner",
                tenantId);
        }

        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativeUri, object? body = null)
        {
            string token = await MintOwnerTokenAsync();
            var request = new HttpRequestMessage(method, relativeUri);
            request.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
            if (body != null)
            {
                request.Content = JsonContent.Create(body, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                });
            }
            return request;
        }

        private static readonly JsonSerializerOptions GatewayJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        private static async Task<T?> SendAndReadAsync<T>(HttpClient client, HttpRequestMessage request, CancellationToken ct = default)
        {
            HttpResponseMessage response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(GatewayJsonOptions, ct);
        }
    }

    /// <summary>DTO for GetCustomerRoles response (camelCase from Gateway).</summary>
    public class CustomerRoleItem
    {
        public Guid Id { get; set; }
        public string RoleType { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime ActivatedAt { get; set; }
        public DateTime? DeactivatedAt { get; set; }
        public string? SalesmanCode { get; set; }
    }
}
