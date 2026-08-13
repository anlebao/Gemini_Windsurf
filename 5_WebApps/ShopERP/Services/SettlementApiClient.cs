using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Sprint B: ShopERP client for Gateway Settlement admin API.
    /// Calls /api/admin/settlements with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class SettlementApiClient : GatewayAdminApiClientBase
    {
        public SettlementApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<SettlementApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<SettlementListResponse> ListAsync(
            Guid? tenantId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            var query = $"api/admin/settlements?page={page}&pageSize={pageSize}";
            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
                query += $"&tenantId={tenantId.Value}";
            if (fromDate.HasValue)
                query += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
            if (toDate.HasValue)
                query += $"&toDate={toDate.Value:yyyy-MM-dd}";

            var req = await CreateRequestAsync(HttpMethod.Get, query);
            return await SendAndReadAsync<SettlementListResponse>(HttpClient, req, ct) ?? new();
        }
    }

    public class SettlementListResponse
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<SettlementItemDto> Items { get; set; } = new();
    }

    public class SettlementItemDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid OwnerId { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Description { get; set; } = string.Empty;
        public Guid? RelatedOrderId { get; set; }
        public Guid? RelatedTransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
