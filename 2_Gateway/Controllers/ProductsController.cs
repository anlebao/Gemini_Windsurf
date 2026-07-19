using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

// ReadAsByteArrayAsync extension method
using System.Net.Http;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// FIX-BATCH-2: Gateway forward for product QR code generation.
    /// Phase 3 (Multi-VPS Checkout): Added catalog forwarding — lookup Tenant.ShopInstanceId → ShopInstance.BaseUrl,
    /// forward HTTP to correct ShopERP. In single-VPS mode, falls back to default "shoperp" HttpClient.
    /// W12-G7: Class-level [Authorize]; public forwarding endpoints opt out via [AllowAnonymous].
    /// </summary>
    [ApiController]
    [Route("api/products")]
    [Authorize]
    public class ProductsController(
        IHttpClientFactory httpClientFactory,
        IShopInstanceService shopInstanceService,
        IVanAnDbContext? dbContext,
        ILogger<ProductsController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly IShopInstanceService _shopInstanceService = shopInstanceService;
        private readonly IVanAnDbContext? _dbContext = dbContext;
        private readonly ILogger<ProductsController> _logger = logger;

        /// <summary>
        /// Resolve the correct ShopERP BaseUrl for a given tenantId.
        /// Phase 3: Multi-VPS routing — lookup Tenant.ShopInstanceId → ShopInstance.BaseUrl.
        /// Falls back to default "shoperp" named client when tenant has no ShopInstance (single-VPS compat).
        /// </summary>
        private async Task<HttpClient> ResolveShopErpClientAsync(Guid? tenantId)
        {
            if (!tenantId.HasValue || _dbContext == null)
                return _httpClientFactory.CreateClient("shoperp");

            var shopInstanceId = await _dbContext.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Id == new TenantId(tenantId.Value) && t.ShopInstanceId.HasValue)
                .Select(t => t.ShopInstanceId!.Value)
                .FirstOrDefaultAsync();

            if (shopInstanceId == Guid.Empty)
                return _httpClientFactory.CreateClient("shoperp");

            // Note: shopInstanceId is Guid here (from Select), not TenantId VO

            var shopInstance = await _shopInstanceService.GetByIdAsync(shopInstanceId);
            if (shopInstance == null || !shopInstance.IsActive || string.IsNullOrEmpty(shopInstance.BaseUrl))
                return _httpClientFactory.CreateClient("shoperp");

            // Create a client with the resolved BaseUrl
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(shopInstance.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            return client;
        }

        /// <summary>
        /// Forward GET /api/products/{id}/qr → ShopERP. Returns PNG image.
        /// Public: KhachLink scanner calls this without JWT (FIX-BATCH-2 forwarding contract).
        /// </summary>
        [HttpGet("{id:guid}/qr")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProductQrCode(Guid id, [FromQuery] Guid? tenantId)
        {
            try
            {
                var client = await ResolveShopErpClientAsync(tenantId);
                string url = tenantId.HasValue
                    ? $"/api/products/{id}/qr?tenantId={tenantId.Value}"
                    : $"/api/products/{id}/qr";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                byte[] pngBytes = await response.Content.ReadAsByteArrayAsync();
                return File(pngBytes, "image/png", $"qr-{id}.png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetProductQrCode to ShopERP for ProductId: {ProductId}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Phase 3: Forward GET /api/products?shopId={tenantId} → correct ShopERP (multi-VPS routing).
        /// Public: KhachLink catalog browse + Scan.razor product fetch.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetProducts([FromQuery] Guid? shopId)
        {
            try
            {
                var client = await ResolveShopErpClientAsync(shopId);
                string url = shopId.HasValue
                    ? $"/api/products?shopId={shopId.Value}"
                    : "/api/products";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetProducts to ShopERP for shopId: {ShopId}", shopId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Phase 3: Forward GET /api/products/grouped-by-tenant → default ShopERP.
        /// Public: KhachLink Home.razor catalog preview.
        /// </summary>
        [HttpGet("grouped-by-tenant")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProductsGroupedByTenant([FromQuery] int tenantsCount = 5, [FromQuery] int productsPerTenant = 2)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                string url = $"/api/products/grouped-by-tenant?tenantsCount={tenantsCount}&productsPerTenant={productsPerTenant}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetProductsGroupedByTenant to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Phase 3: Forward GET /api/products/recommended → correct ShopERP (multi-VPS routing by tenantId).
        /// Public: KhachLink Home.razor personalized recommendations.
        /// </summary>
        [HttpGet("recommended")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRecommendedProducts([FromQuery] Guid customerId, [FromQuery] Guid tenantId, [FromQuery] int topN = 10)
        {
            try
            {
                var client = await ResolveShopErpClientAsync(tenantId);
                string url = $"/api/products/recommended?customerId={customerId}&tenantId={tenantId}&topN={topN}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetRecommendedProducts to ShopERP for tenantId: {TenantId}", tenantId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Phase 3: Forward GET /api/products/{id}/validate-price → correct ShopERP (multi-VPS routing by tenantId).
        /// Public: KhachLink checkout price validation.
        /// </summary>
        [HttpGet("{id:guid}/validate-price")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateProductPrice(Guid id, [FromQuery] decimal unitPrice, [FromQuery] decimal vatRate, [FromQuery] Guid? tenantId)
        {
            try
            {
                var client = await ResolveShopErpClientAsync(tenantId);
                string url = $"/api/products/{id}/validate-price?unitPrice={unitPrice}&vatRate={vatRate}";
                if (tenantId.HasValue)
                    url += $"&tenantId={tenantId.Value}";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, errorContent);
                }

                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding ValidateProductPrice to ShopERP for ProductId: {ProductId}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
