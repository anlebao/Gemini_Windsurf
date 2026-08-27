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
}
