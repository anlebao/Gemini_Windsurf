using System.Text.Json;

namespace VanAn.Directory.Services;

/// <summary>
/// Fetch store directory from Gateway.
/// GET /api/tenants/search?name=&lat=&lng= — search by name/location
/// GET /api/tenants/nearby?lat=&lng=&radiusKm= — nearby stores
/// </summary>
public class CatalogService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = jsonOptions;
    }

    /// <summary>Search stores by name. Returns search result with stores + suggested keywords.
    /// Issue #166 comment: Gateway now returns TenantSearchResultDto wrapper (Results + SuggestedKeywords + MatchStrategy).</summary>
    public async Task<TenantSearchResultDto> SearchStoresAsync(string? name, double? lat = null, double? lng = null)
    {
        var query = "api/tenants/search?";
        if (!string.IsNullOrWhiteSpace(name))
            query += $"name={Uri.EscapeDataString(name)}&";
        if (lat.HasValue && lng.HasValue)
            query += $"lat={lat}&lng={lng}&";
        query = query.TrimEnd('&', '?');

        try
        {
            var resp = await _httpClient.GetAsync(query);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("SearchStoresAsync: {Status}", resp.StatusCode);
                return new TenantSearchResultDto();
            }
            return await resp.Content.ReadFromJsonAsync<TenantSearchResultDto>(_jsonOptions)
                ?? new TenantSearchResultDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchStoresAsync: error");
            return new TenantSearchResultDto();
        }
    }

    /// <summary>Find nearby stores by user location.</summary>
    public async Task<List<TenantStoreDto>> GetNearbyStoresAsync(double lat, double lng, int radiusKm = 5)
    {
        try
        {
            var resp = await _httpClient.GetAsync($"api/tenants/nearby?lat={lat}&lng={lng}&radiusKm={radiusKm}");
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetNearbyStoresAsync: {Status}", resp.StatusCode);
                return [];
            }
            return await resp.Content.ReadFromJsonAsync<List<TenantStoreDto>>(_jsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetNearbyStoresAsync: error");
            return [];
        }
    }

    /// <summary>Search FeaturedProducts by keyword (DisplayName ILIKE contains + token fallback).
    /// Open-closed: new method — does NOT modify SearchStoresAsync.
    /// Calls GET /api/catalog/search?q=&page=&pageSize=.</summary>
    public async Task<ProductSearchResultDto> SearchProductsAsync(string? keyword, int page = 1, int pageSize = 20)
    {
        var query = "api/catalog/search?";
        if (!string.IsNullOrWhiteSpace(keyword))
            query += $"q={Uri.EscapeDataString(keyword)}&";
        query += $"page={page}&pageSize={pageSize}";
        query = query.TrimEnd('&', '?');

        try
        {
            var resp = await _httpClient.GetAsync(query);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("SearchProductsAsync: {Status}", resp.StatusCode);
                return new ProductSearchResultDto();
            }
            return await resp.Content.ReadFromJsonAsync<ProductSearchResultDto>(_jsonOptions)
                ?? new ProductSearchResultDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchProductsAsync: error");
            return new ProductSearchResultDto();
        }
    }
}

/// <summary>Product search result — mirrors Gateway RecommendedCatalogResponse.</summary>
public class ProductSearchResultDto
{
    public List<ProductSearchItem> Products { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>Single product search result item — mirrors Gateway RecommendedProductDto.</summary>
public class ProductSearchItem
{
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal DisplayPrice { get; set; }
    public decimal VatRate { get; set; } = 0.10m;
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string Source { get; set; } = "Featured";
    public string TenantName { get; set; } = string.Empty;
}
