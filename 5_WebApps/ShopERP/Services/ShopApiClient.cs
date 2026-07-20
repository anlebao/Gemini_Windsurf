using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// ShopERP client for the Gateway Shops admin API.
    /// Calls /api/shops with SystemAdmin Bearer JWT.
    /// Uses PostgreSQL (Gateway DB) — NOT ShopERP SQLite.
    /// </summary>
    public sealed class ShopApiClient : GatewayAdminApiClientBase
    {
        public ShopApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<ShopApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<List<ShopDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/shops/by-tenant/{tenantId}");
            // by-tenant returns a single shop or 404. For admin list, we need all shops.
            // Use the search endpoint with empty query to get all.
            return await ListAllAsync(ct);
        }

        public async Task<List<ShopDto>> ListAllAsync(CancellationToken ct = default)
        {
            // Gateway doesn't have a "list all shops" endpoint.
            // Use search with empty name to get all.
            var req = await CreateRequestAsync(HttpMethod.Get, "api/shops/search");
            return await SendAndReadAsync<List<ShopDto>>(HttpClient, req, ct) ?? new();
        }

        public async Task<ShopDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/shops/{id}");
            return await SendAndReadAsync<ShopDto>(HttpClient, req, ct);
        }

        public async Task<ShopDto> CreateAsync(CreateShopRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/shops", request);
            return await SendAndReadAsync<ShopDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty Shop response.");
        }

        public async Task<ShopDto?> UpdateAsync(Guid id, UpdateShopRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/shops/{id}", request);
            return await SendAndReadAsync<ShopDto>(HttpClient, req, ct);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Delete, $"api/shops/{id}");
            HttpResponseMessage response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }
    }

    // DTOs mirror ShopERP ShopsController response shape
    public record ShopDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public record CreateShopRequest
    {
        public Guid TenantId { get; init; }
        public string Name { get; init; } = "";
        public string Address { get; init; } = "";
        public string Phone { get; init; } = "";
        public string Email { get; init; } = "";
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }

    public record UpdateShopRequest
    {
        public string Name { get; init; } = "";
        public string Address { get; init; } = "";
        public string Phone { get; init; } = "";
        public string Email { get; init; } = "";
        public bool IsActive { get; init; } = true;
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }
}
