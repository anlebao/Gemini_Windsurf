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
        private readonly AuthenticationStateProvider _authStateProvider;

        public LoyaltyConfigApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<LoyaltyConfigApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
        {
            _authStateProvider = authStateProvider;
        }

        /// <summary>
        /// P1: these ops are SystemAdmin-only on Gateway AND the base class mints a
        /// SystemAdmin JWT for any caller — so the role check must live client-side too,
        /// otherwise an Owner session could invoke them through a crafted call.
        /// </summary>
        private async Task RequireSystemAdminAsync()
        {
            var auth = await _authStateProvider.GetAuthenticationStateAsync();
            if (!auth.User.IsInRole("SystemAdmin"))
            {
                throw new UnauthorizedAccessException("Chức năng này chỉ dành cho SystemAdmin.");
            }
        }

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

        /// <summary>
        /// Batch 3: reset this tenant's runtime budget counters (PointsIssuedToday / PointsIssuedThisMonth).
        /// Scope: "daily" | "monthly".
        /// </summary>
        public async Task<TenantConfigDto> ResetTenantCountersAsync(Guid tenantId, string scope, CancellationToken ct = default)
        {
            var body = new ResetTenantCountersRequest { Scope = scope };
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/platform/loyalty/tenant/{tenantId}/reset-counters", body);
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

        // === P1.2 Cross-tenant settlement report + P1.1 member exit ===

        /// <summary>
        /// P1.2 (Q4): all-members settlement report — per tenant EarnCredit / RedeemCost /
        /// AdjustNet / NetSettlement. HTX settles money off-system from these numbers.
        /// </summary>
        public async Task<List<SettlementRowDto>> GetSettlementReportAsync(
            DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        {
            string url = "api/platform/loyalty/settlement-report";
            var query = new List<string>();
            if (from.HasValue) query.Add($"from={Uri.EscapeDataString(from.Value.ToUniversalTime().ToString("o"))}");
            if (to.HasValue) query.Add($"to={Uri.EscapeDataString(to.Value.ToUniversalTime().ToString("o"))}");
            if (query.Count > 0) url += "?" + string.Join("&", query);

            await RequireSystemAdminAsync();
            var req = await CreateRequestAsync(HttpMethod.Get, url);
            return await SendAndReadAsync<List<SettlementRowDto>>(HttpClient, req, ct) ?? new List<SettlementRowDto>();
        }

        /// <summary>
        /// P1.1 (Q3): settle-then-burn — burns all points attributed to an exited member.
        /// Tenant must already be opted out of the alliance. settlementNote required (audit).
        /// </summary>
        public async Task<MigrationResultDto> MemberExitAsync(Guid tenantId, string settlementNote, CancellationToken ct = default)
        {
            await RequireSystemAdminAsync();
            var body = new MemberExitRequest { TenantId = tenantId, SettlementNote = settlementNote };
            var req = await CreateRequestAsync(HttpMethod.Post, "api/platform/loyalty/member-exit", body);

            // BadRequest also returns MigrationResultDto{Error} — read it instead of throwing
            // so the UI can show the Vietnamese rejection reason (e.g. "still a member").
            var response = await HttpClient.SendAsync(req, ct);
            var dto = await response.Content.ReadFromJsonAsync<MigrationResultDto>(GatewayJsonOptions, ct);
            return dto ?? new MigrationResultDto
            {
                Error = $"Gateway returned {(int)response.StatusCode} with an unreadable body."
            };
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
        // Batch 3 — budget caps (null = unlimited / no cap)
        public int? MonthlyPointsBudget { get; set; }
        public int? DailyPointsBudget { get; set; }
        public int? PerCustomerDailyLimit { get; set; }
        public decimal? PerOrderRateCap { get; set; } // fraction, e.g. 0.03m = 3%
        // Batch 3 — runtime counters
        public int PointsIssuedThisMonth { get; set; }
        public int PointsIssuedToday { get; set; }
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
        // Batch 3 — budget caps (null = unlimited / no cap)
        public int? MonthlyPointsBudget { get; set; }
        public int? DailyPointsBudget { get; set; }
        public int? PerCustomerDailyLimit { get; set; }
        public decimal? PerOrderRateCap { get; set; } // fraction, e.g. 0.03m = 3%
    }

    public sealed class ResetTenantCountersRequest
    {
        /// <summary>"daily" or "monthly".</summary>
        public string Scope { get; set; } = "daily";
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

    // === P1.1/P1.2 DTOs mirror Gateway LoyaltyConfigController ===

    public sealed class MemberExitRequest
    {
        public Guid TenantId { get; set; }
        public string SettlementNote { get; set; } = string.Empty;
    }

    public sealed class SettlementRowDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public int EarnCredit { get; set; }
        public int RedeemCost { get; set; }
        public int AdjustNet { get; set; }
        public int NetSettlement { get; set; }
    }
}
