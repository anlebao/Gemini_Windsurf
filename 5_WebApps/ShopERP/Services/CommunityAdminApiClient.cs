using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// CC-S6 (Sprint 6): ShopERP client for Gateway Community Admin + Fraud Review APIs.
    /// Calls /api/admin/community/* with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class CommunityAdminApiClient : GatewayAdminApiClientBase
    {
        public CommunityAdminApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<CommunityAdminApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        // === Community Admin ===
        public async Task<EligibleCustomersResult> GetEligibleAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/admin/community/eligible?page={page}&pageSize={pageSize}");
            return await SendAndReadAsync<EligibleCustomersResult>(HttpClient, req, ct) ?? new();
        }

        public async Task<ActivateRoleResult> ActivateRoleAsync(Guid customerId, string role, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/admin/community/{customerId}/activate-role", new { Role = role });
            return await SendAndReadAsync<ActivateRoleResult>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned empty response.");
        }

        public async Task DeactivateRoleAsync(Guid customerId, string role, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/admin/community/{customerId}/deactivate-role", new { Role = role });
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        // === Fraud Review ===
        public async Task<FraudFlagsResult> GetFraudFlagsAsync(string status = "Pending", int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/admin/community/fraud-flags?status={status}&page={page}&pageSize={pageSize}");
            return await SendAndReadAsync<FraudFlagsResult>(HttpClient, req, ct) ?? new();
        }

        public async Task<ShopErpFraudFlagDetail> GetFraudFlagDetailAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/admin/community/fraud-flags/{id}");
            return await SendAndReadAsync<ShopErpFraudFlagDetail>(HttpClient, req, ct) ?? new();
        }

        public async Task<ShopErpConfirmResult> ConfirmFraudFlagAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/admin/community/fraud-flags/{id}/confirm");
            return await SendAndReadAsync<ShopErpConfirmResult>(HttpClient, req, ct) ?? new();
        }

        public async Task<ShopErpDismissResult> DismissFraudFlagAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/admin/community/fraud-flags/{id}/dismiss");
            return await SendAndReadAsync<ShopErpDismissResult>(HttpClient, req, ct) ?? new();
        }

        public async Task<ShopErpFraudStats> GetFraudStatsAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/admin/community/fraud-stats");
            return await SendAndReadAsync<ShopErpFraudStats>(HttpClient, req, ct) ?? new();
        }
    }

    // DTOs matching Gateway response shapes
    public class EligibleCustomersResult
    {
        public int Total { get; set; }
        public List<EligibleCustomerItem> Items { get; set; } = new();
    }

    public class EligibleCustomerItem
    {
        public Guid CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int LoyaltyPoints { get; set; }
        public string IdentityLevel { get; set; } = string.Empty;
        public List<string> ExistingRoles { get; set; } = new();
    }

    public class ActivateRoleResult
    {
        public Guid CommunityRoleId { get; set; }
        public string RoleType { get; set; } = string.Empty;
        public DateTime ActivatedAt { get; set; }
    }

    public class FraudFlagsResult
    {
        public int Total { get; set; }
        public List<FraudFlagItem> Items { get; set; } = new();
    }

    public class FraudFlagItem
    {
        public Guid Id { get; set; }
        public Guid? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public int RiskScore { get; set; }
        public string RiskFactors { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ShopErpFraudFlagDetail : FraudFlagItem
    {
        public string Description { get; set; } = string.Empty;
        public string FlagType { get; set; } = string.Empty;
        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
    }

    public class ShopErpConfirmResult
    {
        public string Status { get; set; } = string.Empty;
        public List<string> SideEffects { get; set; } = new();
        public bool CustomerBanned { get; set; }
    }

    public class ShopErpDismissResult
    {
        public string Status { get; set; } = string.Empty;
        public List<string> SideEffects { get; set; } = new();
    }

    public class ShopErpFraudStats
    {
        public int Pending { get; set; }
        public int Confirmed { get; set; }
        public int Dismissed { get; set; }
        public int Reviewed { get; set; }
        public decimal TotalLossPrevented { get; set; }
        public List<TopFlaggedCustomer> TopFlaggedCustomers { get; set; } = new();
    }

    public class TopFlaggedCustomer
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int FlagCount { get; set; }
    }
}
