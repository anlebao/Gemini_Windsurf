using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// #121.3: ShopERP client for Gateway Global Redemption Catalog admin API.
    /// Calls /api/redemption/catalog/global with SystemAdmin Bearer JWT.
    /// Backed by PostgreSQL (Gateway DB) — global catalog items (TenantId=Empty, IsGlobal=true).
    /// </summary>
    public sealed class GlobalRedemptionCatalogApiClient : GatewayAdminApiClientBase
    {
        public GlobalRedemptionCatalogApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<GlobalRedemptionCatalogApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        /// <summary>GET /api/redemption/catalog/global — list all active global catalog items.</summary>
        public async Task<List<GlobalCatalogItemDto>> GetActiveAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/redemption/catalog/global");
            return await SendAndReadAsync<List<GlobalCatalogItemDto>>(HttpClient, req, ct) ?? new();
        }

        /// <summary>POST /api/redemption/catalog/global — create a new global catalog item.</summary>
        public async Task<bool> CreateAsync(GlobalCatalogItemDto item, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/redemption/catalog/global", item);
            var resp = await HttpClient.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }

        /// <summary>PUT /api/redemption/catalog/global/{id} — update a global catalog item.</summary>
        public async Task<bool> UpdateAsync(Guid id, GlobalCatalogItemDto item, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/redemption/catalog/global/{id}", item);
            var resp = await HttpClient.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }

        /// <summary>DELETE /api/redemption/catalog/global/{id} — deactivate a global catalog item.</summary>
        public async Task<bool> DeactivateAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Delete, $"api/redemption/catalog/global/{id}");
            var resp = await HttpClient.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
    }

    /// <summary>#121.3: DTO for global redemption catalog items (stored in Gateway PG).</summary>
    public sealed class GlobalCatalogItemDto
    {
        public Guid Id { get; set; }
        public string ProductName { get; set; } = "";
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int PointsRequired { get; set; }
        public int? StockCount { get; set; }
        public DateTime? ValidTo { get; set; }
        public int VoucherExpiryDays { get; set; } = 30;
        public bool IsGlobal { get; set; } = true;
    }
}
