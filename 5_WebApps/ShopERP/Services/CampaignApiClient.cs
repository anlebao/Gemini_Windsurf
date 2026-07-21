using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// ShopERP client for the Gateway Campaigns admin API.
    /// Calls /api/campaigns with SystemAdmin Bearer JWT.
    /// Uses PostgreSQL (Gateway DB) — NOT ShopERP SQLite.
    /// </summary>
    public sealed class CampaignApiClient : GatewayAdminApiClientBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public CampaignApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<CampaignApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<List<CampaignDto>> ListAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/campaigns");
            return await SendAndReadAsync<List<CampaignDto>>(HttpClient, req, ct) ?? new();
        }

        public async Task<CampaignDto> CreateAsync(CreateCampaignRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/campaigns", request);
            return await SendAndReadAsync<CampaignDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty Campaign response.");
        }

        public async Task<CampaignDto?> UpdateAsync(Guid id, UpdateCampaignRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/campaigns/{id}", request);
            return await SendAndReadAsync<CampaignDto>(HttpClient, req, ct);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Delete, $"api/campaigns/{id}");
            HttpResponseMessage response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }
    }

    // DTO mirrors Gateway SocialCampaign JSON shape.
    // IMPORTANT: tenantId is a value object {"value":"..."} in JSON — use JsonElement + helper.
    public record CampaignDto
    {
        public Guid Id { get; set; }
        [JsonPropertyName("tenantId")]
        public JsonElement TenantIdElement { get; set; }
        public string CampaignName { get; set; } = "";
        public string UtmSource { get; set; } = "";
        public string TrackingCode { get; set; } = "";
        public string? ImageUrl { get; set; }
        public string? VideoUrl { get; set; }
        public int TotalClicks { get; set; }
        public int ConvertedOrders { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

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

    public record CreateCampaignRequest
    {
        public Guid TenantId { get; init; }
        public string CampaignName { get; init; } = "";
        public string UtmSource { get; init; } = "";
        public string? TrackingCode { get; init; }
        public string? ImageUrl { get; init; }
        public string? VideoUrl { get; init; }
    }

    public record UpdateCampaignRequest
    {
        public string? CampaignName { get; init; }
        public string? UtmSource { get; init; }
        public string? TrackingCode { get; init; }
        public bool IsActive { get; init; } = true;
        public string? ImageUrl { get; init; }
        public string? VideoUrl { get; init; }
    }
}
