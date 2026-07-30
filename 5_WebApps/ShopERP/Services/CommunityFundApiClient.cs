using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Sprint 7 Q3: ShopERP client for Gateway Community Fund admin APIs.
    /// Calls /api/admin/community-fund/* with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class CommunityFundApiClient : GatewayAdminApiClientBase
    {
        public CommunityFundApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<CommunityFundApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<CommunityFundBalanceDto> GetBalanceAsync(CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, "api/admin/community-fund/balance");
            return await SendAndReadAsync<CommunityFundBalanceDto>(HttpClient, req, ct) ?? new();
        }

        public async Task<SpendResultDto> SpendAsync(decimal amount, string reason, string recipient, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/admin/community-fund/spend", new
            {
                Amount = amount,
                Reason = reason,
                Recipient = recipient
            });
            return await SendAndReadAsync<SpendResultDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned empty response.");
        }

        public async Task<SpendHistoryResult> GetHistoryAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Get, $"api/admin/community-fund/history?page={page}&pageSize={pageSize}");
            return await SendAndReadAsync<SpendHistoryResult>(HttpClient, req, ct) ?? new();
        }
    }

    public class CommunityFundBalanceDto
    {
        public decimal Balance { get; set; }
        public decimal TotalCollected { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class SpendResultDto
    {
        public Guid TransactionId { get; set; }
        public Guid SpendRecordId { get; set; }
        public decimal BalanceAfter { get; set; }
    }

    public class SpendHistoryResult
    {
        public int Total { get; set; }
        public List<SpendHistoryItem> Items { get; set; } = new();
    }

    public class SpendHistoryItem
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public Guid ApprovedBy { get; set; }
        public DateTime SpentAt { get; set; }
        public Guid WalletTransactionId { get; set; }
    }
}
