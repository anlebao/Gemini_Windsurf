using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Sprint 7 Q1: ShopERP client for Gateway Product Cost Price admin APIs.
    /// Calls /api/admin/product-cost-prices/* with SystemAdmin Bearer JWT.
    /// </summary>
    public sealed class ProductCostPriceApiClient : GatewayAdminApiClientBase
    {
        public ProductCostPriceApiClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IJwtTokenService jwtTokenService,
            AuthenticationStateProvider authStateProvider,
            ILogger<ProductCostPriceApiClient> logger)
            : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger) { }

        public async Task<ProductCostPriceListResult> GetListAsync(Guid? tenantId = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var url = $"api/admin/product-cost-prices?page={page}&pageSize={pageSize}";
            if (tenantId.HasValue) url += $"&tenantId={tenantId.Value}";
            var req = await CreateRequestAsync(HttpMethod.Get, url);
            return await SendAndReadAsync<ProductCostPriceListResult>(HttpClient, req, ct) ?? new();
        }

        public async Task UpsertAsync(Guid tenantId, Guid productId, decimal costPrice, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Post, "api/admin/product-cost-prices", new
            {
                TenantId = tenantId,
                ProductId = productId,
                CostPrice = costPrice
            });
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var req = await CreateRequestAsync(HttpMethod.Delete, $"api/admin/product-cost-prices/{id}");
            var resp = await HttpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }
    }

    public class ProductCostPriceListResult
    {
        public int Total { get; set; }
        public List<ProductCostPriceItem> Items { get; set; } = new();
    }

    public class ProductCostPriceItem
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ProductId { get; set; }
        public decimal CostPrice { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}
