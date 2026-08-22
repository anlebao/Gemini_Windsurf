using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.FinancialIntelligence;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.Gateway.Services
{
    /// <summary>
    /// VA-FI-MVP2 Bug 3 fix (2026-08-22): Fetches product catalog from ShopERP SQLite
    /// via HTTP, routed by Tenant.ShopInstanceId → ShopInstance.BaseUrl.
    ///
    /// Architecture: Gateway → HTTP → ShopERP (SQLite). Products live in ShopERP per
    /// Option C Phase 3 — Gateway PG Products table is empty. This service bridges the
    /// gap for Financial Intelligence calculations (1-2 uses/day, latency acceptable).
    ///
    /// Auth: mints a short-lived Owner JWT for the tenant so ShopERP's [Authorize(Policy="OwnerOnly")]
    /// on GET /api/products/manage accepts the request. Same JwtTokenService + secret as ShopERP login.
    ///
    /// Graceful degradation: returns empty list on any failure (HTTP error, timeout,
    /// ShopInstance not found, tenant not found). Financial Intelligence is non-critical.
    ///
    /// Precedent: ProductsController.ResolveShopErpClientAsync (same routing pattern).
    /// </summary>
    public sealed class ShopErpProductCatalogService : IShopErpProductCatalogService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IVanAnDbContext _dbContext;
        private readonly IShopInstanceService _shopInstanceService;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ILogger<ShopErpProductCatalogService> _logger;

        public ShopErpProductCatalogService(
            IHttpClientFactory httpClientFactory,
            IVanAnDbContext dbContext,
            IShopInstanceService shopInstanceService,
            IJwtTokenService jwtTokenService,
            ILogger<ShopErpProductCatalogService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _dbContext = dbContext;
            _shopInstanceService = shopInstanceService;
            _jwtTokenService = jwtTokenService;
            _logger = logger;
        }

        public async Task<List<ProductSnapshot>> GetProductsAsync(TenantId tenantId, CancellationToken ct = default)
        {
            try
            {
                HttpClient client = await ResolveShopErpClientAsync(tenantId, ct).ConfigureAwait(false);

                // Mint a short-lived Owner JWT for the tenant so ShopERP's OwnerOnly policy accepts.
                // userId/email are synthetic — ShopERP validates JWT signature + role + tenant_id claim,
                // not user existence in its SQLite Users table (TenantProvider reads claim only).
                string jwt = _jwtTokenService.GenerateToken(
                    userId: Guid.NewGuid(),
                    email: "financial-intelligence@vanan.vn",
                    role: UserRole.Owner,
                    tenantId: tenantId.Value);

                HttpRequestMessage req = new(HttpMethod.Get, "/api/products/manage");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

                HttpResponseMessage response = await client.SendAsync(req, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ShopErpProductCatalog: GET /api/products/manage returned {Status} for tenant {TenantId}",
                        response.StatusCode, tenantId.Value);
                    return new List<ProductSnapshot>();
                }

                List<ProductDetailDto>? products = await response.Content
                    .ReadFromJsonAsync<List<ProductDetailDto>>(cancellationToken: ct)
                    .ConfigureAwait(false);

                if (products is null || products.Count == 0)
                    return new List<ProductSnapshot>();

                // Map DTO → snapshot (only fields needed by Financial Intelligence)
                return products
                    .Select(p => new ProductSnapshot(
                        ProductId: p.ProductId,
                        Name: p.Name,
                        Price: p.Price,
                        CostPrice: p.CostPrice,
                        Category: p.Category ?? string.Empty))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ShopErpProductCatalog: failed to fetch products for tenant {TenantId}", tenantId.Value);
                return new List<ProductSnapshot>();
            }
        }

        /// <summary>
        /// Resolve the correct ShopERP BaseUrl for a tenant.
        /// Phase 3 Multi-VPS routing: Tenant.ShopInstanceId → ShopInstance.BaseUrl.
        /// Falls back to default "shoperp" named HttpClient when tenant has no ShopInstance.
        /// Precedent: ProductsController.ResolveShopErpClientAsync.
        /// </summary>
        private async Task<HttpClient> ResolveShopErpClientAsync(TenantId tenantId, CancellationToken ct)
        {
            var shopInstanceId = await _dbContext.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Id == tenantId && t.ShopInstanceId.HasValue)
                .Select(t => t.ShopInstanceId!.Value)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (shopInstanceId == Guid.Empty)
                return _httpClientFactory.CreateClient("shoperp");

            var shopInstance = await _shopInstanceService.GetByIdAsync(shopInstanceId, ct).ConfigureAwait(false);
            if (shopInstance == null || !shopInstance.IsActive || string.IsNullOrEmpty(shopInstance.BaseUrl))
                return _httpClientFactory.CreateClient("shoperp");

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(shopInstance.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            return client;
        }
    }
}
