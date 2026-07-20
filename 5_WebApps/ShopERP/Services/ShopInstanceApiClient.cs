using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Phase 6: ShopERP client for the Gateway ShopInstances admin API.
    /// Calls /api/v1/shop-instances with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class ShopInstanceApiClient : GatewayAdminApiClientBase
    {
        public ShopInstanceApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<ShopInstanceApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<List<ShopInstanceDto>> ListAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/shop-instances");
            return await SendAndReadAsync<List<ShopInstanceDto>>(HttpClient, req, ct) ?? new();
        }

        public async Task<ShopInstanceDto> CreateAsync(CreateShopInstanceRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/v1/shop-instances", request);
            return await SendAndReadAsync<ShopInstanceDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty ShopInstance response.");
        }

        public async Task UpdateAsync(Guid id, UpdateShopInstanceRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/v1/shop-instances/{id}", request);
            HttpResponseMessage response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }

        public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
        {
            string action = isActive ? "activate" : "deactivate";
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/v1/shop-instances/{id}/{action}");
            HttpResponseMessage response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }

        public async Task<ShopInstanceHealthResult?> HealthCheckAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/shop-instances/{id}/health-check");
            return await SendAndReadAsync<ShopInstanceHealthResult>(HttpClient, req, ct);
        }
    }

    // DTOs mirror Gateway ShopInstancesController DTOs
    public sealed class CreateShopInstanceRequest
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int MaxTenants { get; set; } = 50;
        public string? HealthCheckUrl { get; set; }
    }

    public sealed class UpdateShopInstanceRequest
    {
        public string Label { get; set; } = string.Empty;
        public int MaxTenants { get; set; } = 50;
    }

    public sealed class ShopInstanceDto
    {
        public Guid Id { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int MaxTenants { get; set; }
        public bool IsActive { get; set; }
        public string? HealthCheckUrl { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public string HealthStatus { get; set; } = "Unknown";
        public int TenantCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class ShopInstanceHealthResult
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = "Unknown";
        public DateTime CheckedAt { get; set; }
        public string? Error { get; set; }
    }
}
