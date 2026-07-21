using System.Net.Http.Json;
using VanAn.KhachLink.Models;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Services.Http
{
    /// <summary>
    /// HTTP-backed ShopConfig loader for KhachLink.
    /// Approach 2 (product-based): products → extract TenantId → GET /api/shops/by-tenant/{tenantId}
    /// → build ShopConfig from real Shop entity. No direct CoreHub DI — calls Gateway via HTTP only.
    /// Branding fields (PrimaryColor, SecondaryColor, Theme) remain at ShopConfig defaults
    /// because they are not stored on the Shop entity.
    /// Fallbacks to DefaultShopConfig on any error / empty / not-found case.
    /// </summary>
    public class ShopConfigHttpService(IHttpClientFactory httpClientFactory, ILogger<ShopConfigHttpService> logger)
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
        private readonly ILogger<ShopConfigHttpService> _logger = logger;

        /// <summary>
        /// Default ShopConfig used when no products are available, shop is not found,
        /// or any API error occurs. Preserves the original stub defaults so KhachLink
        /// always renders a usable layout.
        /// </summary>
        public static ShopConfig DefaultShopConfig => new();

        /// <summary>
        /// SC1: Build ShopConfig from a list of products. Extracts the TenantId from the
        /// first product, then loads the Shop entity for that tenant. Caller is responsible
        /// for loading products (separation of concerns — no ProductHttpService injection).
        /// </summary>
        public async Task<ShopConfig> GetShopConfigFromProductsAsync(List<ProductDto> products)
        {
            if (products is null || products.Count == 0)
            {
                _logger.LogDebug("GetShopConfigFromProductsAsync: no products supplied, returning DefaultShopConfig");
                return DefaultShopConfig;
            }

            Guid tenantId = products[0].TenantId;
            if (tenantId == Guid.Empty)
            {
                _logger.LogWarning("GetShopConfigFromProductsAsync: first product has empty TenantId, returning DefaultShopConfig");
                return DefaultShopConfig;
            }

            return await GetShopConfigByTenantIdAsync(tenantId);
        }

        /// <summary>
        /// SC2: Load ShopConfig by TenantId. Calls GET /api/tenants/{tenantId}/store-info
        /// (TenantStoreController — replaces old shops/by-tenant endpoint, 2026-07-21).
        /// Returns DefaultShopConfig on 404 or any error (SC3).
        /// Builds ShopConfig from real Tenant store data (SC4); branding fields keep defaults (SC5).
        /// </summary>
        public async Task<ShopConfig> GetShopConfigByTenantIdAsync(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
            {
                return DefaultShopConfig;
            }

            try
            {
                var response = await _httpClient.GetAsync($"api/tenants/{tenantId}/store-info");
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogInformation("Tenant store-info not found for tenant {TenantId}, returning DefaultShopConfig", tenantId);
                    }
                    else
                    {
                        _logger.LogWarning("store-info endpoint returned {Status} for tenant {TenantId}, returning DefaultShopConfig", response.StatusCode, tenantId);
                    }
                    return DefaultShopConfig;
                }

                ShopDto? shop = await response.Content.ReadFromJsonAsync<ShopDto>();
                if (shop is null)
                {
                    _logger.LogWarning("store-info endpoint returned empty body for tenant {TenantId}, returning DefaultShopConfig", tenantId);
                    return DefaultShopConfig;
                }

                return BuildShopConfigFromShop(shop);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading ShopConfig for tenant {TenantId}, returning DefaultShopConfig", tenantId);
                return DefaultShopConfig;
            }
        }

        /// <summary>
        /// SC4: Map ShopDto → ShopConfig. Real tenant store data (Name, Address, Phone, Email,
        /// Latitude, Longitude, SocialLinks, LogoUrl) overrides defaults. Branding fields
        /// (PrimaryColor, SecondaryColor, Theme, Features, LoyaltyConfig) stay at ShopConfig
        /// defaults because they are not stored on the Tenant entity (SC5).
        /// </summary>
        private static ShopConfig BuildShopConfigFromShop(ShopDto shop)
        {
            return DefaultShopConfig with
            {
                TenantId = shop.Id,
                ShopName = string.IsNullOrWhiteSpace(shop.Name) ? DefaultShopConfig.ShopName : shop.Name,
                Address = shop.Address,
                Phone = shop.Phone,
                Email = shop.Email,
                Latitude = shop.Latitude,
                Longitude = shop.Longitude,
                SocialLinksFb = shop.SocialLinksFb,
                SocialLinksTiktok = shop.SocialLinksTiktok
            };
        }
    }
}
