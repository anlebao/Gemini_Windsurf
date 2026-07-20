using System.Net.Http.Json;
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

    // DTOs mirror Gateway CampaignsController DTOs
    public record CampaignDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ShopId { get; set; }
        public string CampaignName { get; set; } = "";
        public string UtmSource { get; set; } = "";
        public string TrackingCode { get; set; } = "";
        public int TotalClicks { get; set; }
        public int ConvertedOrders { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public record CreateCampaignRequest
    {
        public Guid TenantId { get; init; }
        public Guid ShopId { get; init; }
        public string CampaignName { get; init; } = "";
        public string UtmSource { get; init; } = "";
        public string? TrackingCode { get; init; }
    }

    public record UpdateCampaignRequest
    {
        public string? CampaignName { get; init; }
        public string? UtmSource { get; init; }
        public string? TrackingCode { get; init; }
        public bool IsActive { get; init; } = true;
    }
}
