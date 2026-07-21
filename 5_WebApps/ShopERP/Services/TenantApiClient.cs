using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// ShopERP client for the Gateway Tenants admin API.
    /// Calls /api/v1/tenants with SystemAdmin Bearer JWT.
    /// Uses PostgreSQL (Gateway DB) — NOT ShopERP SQLite.
    /// </summary>
    public sealed class TenantApiClient : GatewayAdminApiClientBase
    {
        public TenantApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<TenantApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<List<TenantApiDto>> ListAllAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/v1/tenants");
            return await SendAndReadAsync<List<TenantApiDto>>(HttpClient, req, ct) ?? new();
        }

        public async Task UpdateProfileAsync(Guid tenantId, UpdateTenantProfileApiRequest request, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/v1/tenants/{tenantId}/profile", request);
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        public async Task AssignShopInstanceAsync(Guid tenantId, Guid shopInstanceId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/v1/tenants/{tenantId}/shop-instance",
                new { ShopInstanceId = shopInstanceId });
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        /// <summary>Tenant Profile Page (2026-07-21): Update URL slug for /store/{slug} route.</summary>
        public async Task UpdateSlugAsync(Guid tenantId, string? slug, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/v1/tenants/{tenantId}/slug",
                new { Slug = slug });
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }
    }

    public record TenantApiDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public BusinessType BusinessType { get; init; }
        public TenantStatus Status { get; init; }
        public Guid? ShopInstanceId { get; init; }
        public string? ContactEmail { get; init; }
        public string? ContactPhone { get; init; }
        public string? Address { get; init; }
        public string? TaxCode { get; init; }
        /// <summary>Tenant Profile Page (2026-07-21): URL slug for /store/{slug}. Null if not set.</summary>
        public string? Slug { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    public record UpdateTenantProfileApiRequest
    {
        public string Name { get; init; } = "";
        public string? ContactEmail { get; init; }
        public string? ContactPhone { get; init; }
        public string? Address { get; init; }
        public string? TaxCode { get; init; }
    }
}
