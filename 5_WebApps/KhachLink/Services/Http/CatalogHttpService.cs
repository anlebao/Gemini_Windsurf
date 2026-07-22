using System.Net.Http.Json;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services.Http
{
    /// <summary>
    /// Phase 6: Thin HTTP client for Gateway catalog recommended API.
    /// Calls GET /api/catalog/recommended â€” public endpoint (no auth required).
    /// Returns union of FeaturedProducts + customer purchase history.
    /// </summary>
    public class CatalogHttpService(IHttpClientFactory httpClientFactory, ILogger<CatalogHttpService> logger)
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
        private readonly ILogger<CatalogHttpService> _logger = logger;

        /// <summary>Get recommended products (Featured + customer history). Anonymous-safe.</summary>
        public async Task<RecommendedCatalogResponse?> GetRecommendedAsync(Guid? customerId = null, int page = 1, int pageSize = 20)
        {
            try
            {
                string url = $"api/catalog/recommended?page={page}&pageSize={pageSize}";
                if (customerId.HasValue && customerId.Value != Guid.Empty)
                    url += $"&customerId={customerId.Value}";

                return await _httpClient.GetFromJsonAsync<RecommendedCatalogResponse>(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recommended catalog from Gateway");
                return null;
            }
        }
    }
}
