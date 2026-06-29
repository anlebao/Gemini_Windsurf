using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services.Http
{
    /// <summary>
    /// Thin HTTP client for ShopERP product catalog API.
    /// KhachLink calls Gateway/shoperp/api/products — YARP forwards to ShopERP.
    /// </summary>
    public class ProductHttpService(IHttpClientFactory httpClientFactory, ILogger<ProductHttpService> logger)
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
        private readonly ILogger<ProductHttpService> _logger = logger;

        public async Task<List<ProductDto>> GetProductsAsync(Guid? shopId = null)
        {
            try
            {
                string url = "shoperp/api/products";
                if (shopId.HasValue)
                {
                    url += $"?shopId={shopId.Value}";
                }

                List<ProductDto>? result = await _httpClient.GetFromJsonAsync<List<ProductDto>>(url);
                return result ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products catalog from ShopERP");
                return [];
            }
        }

        /// <summary>
        /// Get personalized product recommendations for a customer
        /// </summary>
        public async Task<List<RecommendedProductDto>> GetRecommendedProductsAsync(Guid customerId, Guid tenantId, int topN = 10)
        {
            try
            {
                string url = $"shoperp/api/products/recommended?customerId={customerId}&tenantId={tenantId}&topN={topN}";
                List<RecommendedProductDto>? result = await _httpClient.GetFromJsonAsync<List<RecommendedProductDto>>(url);
                return result ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recommended products for customer {CustomerId}", customerId);
                return [];
            }
        }
    }
}
