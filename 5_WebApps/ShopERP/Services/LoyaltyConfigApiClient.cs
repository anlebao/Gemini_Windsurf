using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Loyalty Alliance Phase 5A: ShopERP client for the Gateway LoyaltyConfig admin API.
    /// Calls /api/platform/loyalty/* with SystemAdmin Bearer JWT.
    /// Backed by PostgreSQL (Gateway DB) — NOT ShopERP SQLite.
    /// Endpoints: GET/PUT global config, GET/PUT per-tenant config, POST migrate (Phase 4 wiring).
    /// Spec: docs/specs/loyalty-alliance-spec.md v1.0.
    /// </summary>
    public sealed class LoyaltyConfigApiClient : GatewayAdminApiClientBase
    {
        public LoyaltyConfigApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<LoyaltyConfigApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        // === Global Config ===

        public async Task<GlobalConfigDto> GetGlobalConfigAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/platform/loyalty/config");
            return await SendAndReadAsync<GlobalConfigDto>(HttpClient, req, ct) ?? new GlobalConfigDto();
        }

        public async Task<GlobalConfigDto> UpdateGlobalConfigAsync(UpdateGlobalConfigRequest body, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, "api/platform/loyalty/config", body);
            return await SendAndReadAsync<GlobalConfigDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty global config response.");
        }

        // === Per-Tenant Config ===

        public async Task<TenantConfigDto> GetTenantConfigAsync(Guid tenantId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/platform/loyalty/tenant/{tenantId}/config");
            return await SendAndReadAsync<TenantConfigDto>(HttpClient, req, ct)
                ?? new TenantConfigDto { TenantId = tenantId };
        }

        public async Task<TenantConfigDto> UpdateTenantConfigAsync(Guid tenantId, UpdateTenantConfigRequest body, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Put, $"api/platform/loyalty/tenant/{tenantId}/config", body);
            return await SendAndReadAsync<TenantConfigDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty tenant config response.");
        }

        // === Mode Switch Migration (Phase 5A — wires Phase 4) ===

        /// <summary>
        /// Silo→Alliance: caller supplies customer balances from ShopERP SQLite.
        /// Gateway creates/credits AllianceWallet + ADJUST tx per customer.
        /// </summary>
        public async Task<MigrationResultDto> MigrateConsolidateAsync(
            Guid tenantId, List<CustomerBalanceInputDto> balances, CancellationToken ct = default)
        {
            var body = new MigrateRequest
            {
                Direction = "consolidate",
                TenantId = tenantId,
                CustomerBalances = balances
            };
            var req = await CreateRequestAsync(HttpMethod.Post, "api/platform/loyalty/migrate", body);
            return await SendAndReadAsync<MigrationResultDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty migration response.");
        }

        /// <summary>
        /// Alliance→Silo: Gateway splits PG wallet proportionally + freezes it, returns allocations.
        /// Caller applies allocations to ShopERP SQLite LoyaltyRewards.
        /// </summary>
        public async Task<MigrationResultDto> MigrateSplitAsync(Guid tenantId, CancellationToken ct = default)
        {
            var body = new MigrateRequest { Direction = "split", TenantId = tenantId };
            var req = await CreateRequestAsync(HttpMethod.Post, "api/platform/loyalty/migrate", body);
            return await SendAndReadAsync<MigrationResultDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned an empty migration response.");
        }
    }

    // === DTOs mirror Gateway LoyaltyConfigController DTOs ===

    public sealed class GlobalConfigDto
    {
        public LoyaltyMode Mode { get; set; }
        public int PointsRate { get; set; }
        public int MinPointsPerOrder { get; set; }
        public int MaxPointsPerOrder { get; set; }
        public int MaxWalletPoints { get; set; }
        public DateTime? LastChangedAt { get; set; }
        public string? LastChangedBy { get; set; }
    }

    public sealed class UpdateGlobalConfigRequest
    {
        public LoyaltyMode Mode { get; set; }
        public int PointsRate { get; set; } = 1;          // Issue #118: editable (1 = 1% of order total)
        public int MinPointsPerOrder { get; set; } = 10;  // Issue #118: editable
        public int MaxPointsPerOrder { get; set; }
        public int MaxWalletPoints { get; set; }
    }

    public sealed class TenantConfigDto
    {
        public Guid TenantId { get; set; }
        /// <summary>null = inherit global.</summary>
        public LoyaltyMode? Mode { get; set; }
        public bool IsAllianceMember { get; set; }
        /// <summary>null = inherit global.</summary>
        public int? MaxWalletPoints { get; set; }
        public DateTime? LastChangedAt { get; set; }
        public string? LastChangedBy { get; set; }
    }

    public sealed class UpdateTenantConfigRequest
    {
        /// <summary>null = inherit global.</summary>
        public LoyaltyMode? Mode { get; set; }
        public bool IsAllianceMember { get; set; }
        /// <summary>null = inherit global.</summary>
        public int? MaxWalletPoints { get; set; }
    }

    public sealed class MigrateRequest
    {
        public string Direction { get; set; } = "consolidate";
        public Guid TenantId { get; set; }
        public List<CustomerBalanceInputDto>? CustomerBalances { get; set; }
    }

    public sealed class CustomerBalanceInputDto
    {
        public Guid CustomerDeviceId { get; set; }
        public int PointBalance { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public sealed class MigrationResultDto
    {
        public int CustomersProcessed { get; set; }
        public int TotalPointsTransferred { get; set; }
        public List<WalletAllocationDto> Allocations { get; set; } = new();
        public string? Error { get; set; }
        public bool Success { get; set; }
    }

    public sealed class WalletAllocationDto
    {
        public Guid CustomerDeviceId { get; set; }
        public Guid TenantId { get; set; }
        public int Points { get; set; }
    }
}
