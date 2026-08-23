using Microsoft.Extensions.Caching.Memory;
using VanAn.Shared.Domain;

namespace VanAn.Directory.Services;

/// <summary>
/// Fetch ShopConfig from Gateway by tenant ID.
/// Server-side cache (IMemoryCache, 5 min TTL).
/// Adapt từ KhachLink ShopConfigHttpService — bỏ IHttpClientFactory, dùng typed HttpClient.
/// </summary>
public class ShopConfigService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ShopConfigService> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public ShopConfigService(HttpClient httpClient, IMemoryCache cache, ILogger<ShopConfigService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public static ShopConfig DefaultShopConfig => new();

    /// <summary>
    /// Get ShopConfig by tenant ID. Calls GET /api/tenants/{tenantId}/store-info.
    /// Returns DefaultShopConfig on 404 or error.
    /// </summary>
    public async Task<ShopConfig> GetByTenantIdAsync(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            return DefaultShopConfig;

        var cacheKey = $"shopconfig:{tenantId}";
        if (_cache.TryGetValue(cacheKey, out ShopConfig? cached))
            return cached!;

        try
        {
            var resp = await _httpClient.GetAsync($"api/tenants/{tenantId}/store-info");
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogDebug("GetByTenantIdAsync: {Status} for tenant {TenantId}", resp.StatusCode, tenantId);
                return DefaultShopConfig;
            }

            var store = await resp.Content.ReadFromJsonAsync<TenantStoreDto>();
            if (store == null) return DefaultShopConfig;

            var config = BuildShopConfigFromStore(store);
            _cache.Set(cacheKey, config, CacheTtl);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetByTenantIdAsync: error for tenant {TenantId}", tenantId);
            return DefaultShopConfig;
        }
    }

    private static ShopConfig BuildShopConfigFromStore(TenantStoreDto store)
    {
        return DefaultShopConfig with
        {
            TenantId = store.Id,
            ShopName = string.IsNullOrWhiteSpace(store.Name) ? DefaultShopConfig.ShopName : store.Name,
            Address = store.Address,
            Phone = store.Phone,
            Email = store.Email,
            Latitude = store.Latitude,
            Longitude = store.Longitude,
            SocialLinksFb = store.SocialLinksFb,
            SocialLinksTiktok = store.SocialLinksTiktok,
            ActiveTheme = store.Theme,
            NavColor = store.NavColor,
            HeaderColor = store.HeaderColor,
            FooterColor = store.FooterColor,
            LogoUrl = !string.IsNullOrWhiteSpace(store.LogoUrl) && Uri.TryCreate(store.LogoUrl, UriKind.Absolute, out var logoUri)
                ? logoUri
                : DefaultShopConfig.LogoUrl
        };
    }
}

// DTO matching Gateway TenantStoreDto (2_Gateway/Controllers/TenantStoreController.cs L289-316)
public class TenantStoreDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? DistanceKm { get; init; }
    public string? Slug { get; init; }
    public string? SocialLinksFb { get; init; }
    public string? SocialLinksTiktok { get; init; }
    public string? BrandStory { get; init; }
    public string? LogoUrl { get; init; }
    public ThemeType Theme { get; init; } = ThemeType.Classic;
    public string? NavColor { get; init; }
    public string? HeaderColor { get; init; }
    public string? FooterColor { get; init; }
    public string? KhachLinkDomain { get; init; }
}
