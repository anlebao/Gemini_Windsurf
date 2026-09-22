using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Settlement Batch-3 (TC-09): ShopERP client for Gateway withdrawal admin APIs.
    /// Calls /api/admin/withdrawals/* with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class WithdrawalApiClient : GatewayAdminApiClientBase
    {
        public WithdrawalApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<WithdrawalApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<WithdrawalListResultDto> ListAsync(string? status = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var uri = $"api/admin/withdrawals?page={page}&pageSize={pageSize}"
                + (string.IsNullOrWhiteSpace(status) ? string.Empty : $"&status={Uri.EscapeDataString(status)}");
            var req = await CreateRequestAsync(HttpMethod.Get, uri);
            return await SendAndReadAsync<WithdrawalListResultDto>(HttpClient, req, ct) ?? new();
        }

        public async Task<WithdrawalActionResultDto> ApproveAsync(Guid requestId, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/admin/withdrawals/{requestId}/approve");
            return await SendAndReadAsync<WithdrawalActionResultDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned empty response.");
        }

        public async Task<WithdrawalActionResultDto> RejectAsync(Guid requestId, string reason, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/admin/withdrawals/{requestId}/reject", new { Reason = reason });
            return await SendAndReadAsync<WithdrawalActionResultDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned empty response.");
        }

        public async Task<WithdrawalActionResultDto> PayAsync(Guid requestId, string bankReference, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, $"api/admin/withdrawals/{requestId}/pay", new { BankReference = bankReference });
            return await SendAndReadAsync<WithdrawalActionResultDto>(HttpClient, req, ct)
                ?? throw new InvalidOperationException("Gateway returned empty response.");
        }
    }

    public class WithdrawalListResultDto
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<WithdrawalItemDto> Items { get; set; } = new();
    }

    public class WithdrawalItemDto
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? BankReference { get; set; }
        public string? RejectReason { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public Guid? WalletTransactionId { get; set; }
    }

    public class WithdrawalActionResultDto
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid? WalletTransactionId { get; set; }
        public string? BankReference { get; set; }
    }
}
