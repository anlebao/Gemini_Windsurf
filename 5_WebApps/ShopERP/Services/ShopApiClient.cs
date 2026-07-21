using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    // DTO mirrors Gateway Shop JSON shape.
    // tenantId is a value object {"value":"..."} in JSON — use JsonElement + helper.
    public record ShopDto
    {
        public Guid Id { get; set; }
        [JsonPropertyName("tenantId")]
        public JsonElement TenantIdElement { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [JsonIgnore]
        public Guid TenantId => ExtractGuid(TenantIdElement);

        private static Guid ExtractGuid(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("value", out var v))
                return v.GetGuid();
            if (el.ValueKind == JsonValueKind.String)
                return el.GetGuid();
            return Guid.Empty;
        }
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
