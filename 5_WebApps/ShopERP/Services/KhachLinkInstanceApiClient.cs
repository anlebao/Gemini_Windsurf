using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// ShopERP client for the Gateway KhachLinkInstance admin API.
    /// Calls /api/v1/khachlink-instances with SystemAdmin Bearer JWT.
    /// Uses PostgreSQL (Gateway DB) — NOT ShopERP SQLite.
    /// </summary>
    public sealed class KhachLinkInstanceApiClient : GatewayAdminApiClientBase
    {
        public KhachLinkInstanceApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<KhachLinkInstanceApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<List<KhachLinkInstanceDto>> ListAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/khachlink-instances");
            return await SendAndReadAsync<List<KhachLinkInstanceDto>>(HttpClient, req, ct) ?? new();
        }

        public async Task<KhachLinkInstanceDto> CreateAsync(CreateKhachLinkInstanceRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/v1/khachlink-instances", request);
            return await SendAndReadAsync<KhachLinkInstanceDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty KhachLinkInstance response.");
        }

        public async Task UpdateAsync(Guid id, UpdateKhachLinkInstanceRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/v1/khachlink-instances/{id}", request);
            var response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Delete, $"api/v1/khachlink-instances/{id}");
            var response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>#134: Activate a previously deactivated KhachLinkInstance.</summary>
        public async Task ActivateAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/v1/khachlink-instances/{id}/activate");
            var response = await HttpClient.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
        }
    }

    // ── DTOs (mirror Gateway KhachLinkInstanceController DTOs) ───────────────

    public sealed class KhachLinkInstanceDto
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public KhachLinkProfile Profile { get; set; }
        public string CustomDomain { get; set; } = string.Empty;
        public Guid? OwnerTenantId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public KhachLinkNavFlagsDto NavFlags { get; set; } = new();
        // Issue #143: style override fields (null = inherit from tenant ShopConfig)
        public string? Theme { get; set; }
        public string? LogoUrl { get; set; }
        public string? NavColor { get; set; }
        public string? HeaderColor { get; set; }
        public string? FooterColor { get; set; }
    }

    public sealed class KhachLinkNavFlagsDto
    {
        public bool ShowHome { get; set; } = true;
        public bool ShowCart { get; set; } = true;
        public bool ShowOrders { get; set; } = true;
        public bool ShowLoyaltyHistory { get; set; } = true;
        public bool ShowMissions { get; set; } = true;
        public bool ShowRewards { get; set; } = true;
        public bool ShowAllianceWallet { get; set; } = true;
        public bool ShowStores { get; set; } = true;
        public bool ShowCampaigns { get; set; } = true;
        public bool ShowScan { get; set; } = true;
        public bool ShowQrClaim { get; set; } = true;
        public bool ShowCommunity { get; set; } = true;
        public bool ShowJobs { get; set; } = false;
        public bool ShowProfile { get; set; } = true;
        public bool ShowStaffDashboard { get; set; } = true;
    }

    public sealed class CreateKhachLinkInstanceRequest
    {
        public string Label { get; set; } = string.Empty;
        public KhachLinkProfile Profile { get; set; } = KhachLinkProfile.FullCommerce;
        public string CustomDomain { get; set; } = string.Empty;
        public Guid? OwnerTenantId { get; set; }
        public KhachLinkNavFlagsDto? NavFlagsOverride { get; set; }
    }

    public sealed class UpdateKhachLinkInstanceRequest
    {
        public KhachLinkProfile Profile { get; set; } = KhachLinkProfile.FullCommerce;
        public KhachLinkNavFlagsDto NavFlags { get; set; } = new();
        // Issue #143: style override fields (null/empty = clear override, inherit from tenant ShopConfig)
        public string? Theme { get; set; }
        public string? LogoUrl { get; set; }
        public string? NavColor { get; set; }
        public string? HeaderColor { get; set; }
        public string? FooterColor { get; set; }
    }
}
