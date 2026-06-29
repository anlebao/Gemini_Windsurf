using VanAn.KhachLink.Models;
using VanAn.Shared.DTOs;

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

        public async Task<ProductDto?> GetProductByIdAsync(Guid productId, Guid? shopId = null)
        {
            try
            {
                string url = $"shoperp/api/products/{productId}";
                if (shopId.HasValue)
                {
                    url += $"?shopId={shopId.Value}";
                }

                ProductDto? result = await _httpClient.GetFromJsonAsync<ProductDto>(url);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product {ProductId} from ShopERP", productId);
                return null;
            }
        }
    }
}
