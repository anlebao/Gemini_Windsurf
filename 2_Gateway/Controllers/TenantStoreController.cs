using Microsoft.AspNetCore.Authorization;
using VanAn.Gateway.Services;
using Microsoft.AspNetCore.Mvc;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Public Store Finder + Tenant Profile Page endpoints.
    /// Replaced ShopsController (Shop entity removed 2026-07-21).
    /// Queries Tenant.Settings.Latitude/Longitude for Store Finder functionality.
    /// All endpoints are anonymous (KhachLink customer app is unauthenticated).
    ///
    /// W3-2d (anti-drift): mọi tenant-read logic (search/nearby/store-info/by-slug +
    /// batch flag/domain lookups) đã extract sang ITenantSearchService — controller
    /// chỉ còn HTTP concerns (routing, status codes, logging). Mọi search surface
    /// mới PHẢI dùng service này — xem docs/design/mobile-search-ux-pattern.md.
    /// </summary>
    [ApiController]
    [Route("api/tenants")]
    public class TenantStoreController(
        ITenantSearchService tenantSearch,
        IShopFeatureSettingsService featureSettingsService,
        ILogger<TenantStoreController> logger) : ControllerBase
    {
        private readonly ITenantSearchService _tenantSearch = tenantSearch;
        private readonly IShopFeatureSettingsService _featureSettingsService = featureSettingsService;
        private readonly ILogger<TenantStoreController> _logger = logger;

        /// <summary>
        /// Get tenant store info (name, address, phone, lat/lng, slug) by TenantId.
        /// Replaces GET /api/shops/by-tenant/{tenantId}.
        /// </summary>
        [HttpGet("{tenantId:guid}/store-info")]
        [AllowAnonymous]
        public async Task<ActionResult<TenantStoreDto>> GetStoreInfo(Guid tenantId, CancellationToken ct)
        {
            try
            {
                var dto = await _tenantSearch.GetStoreInfoAsync(tenantId, ct);
                return dto == null ? NotFound() : Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting store info for tenant {TenantId}", tenantId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Tenant Profile Page (2026-07-21): Get tenant store info by URL slug.
        /// Returns 404 if slug is null or not found.
        /// Used by KhachLink /store/{slug} route.
        ///
        /// Crawl-to-Onboard (2026-08-25, M3): Pending tenants return Phone=null + Email=null
        /// (HIDE SĐT section entirely per Luật 91/2025 Điều 16 — không "công khai" dữ liệu cá nhân chưa consent).
        /// Active tenants return full profile. Suspended/Inactive/Converted → 404 (don't expose).
        /// </summary>
        [HttpGet("by-slug/{slug}")]
        [AllowAnonymous]
        public async Task<ActionResult<TenantStoreDto>> GetBySlug(string slug, CancellationToken ct)
        {
            try
            {
                var dto = await _tenantSearch.GetBySlugAsync(slug, ct);
                return dto == null ? NotFound() : Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant by slug {Slug}", slug);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Tenant Profile Page (2026-07-21): Get public feature settings for a tenant.
        /// Returns which sections (Campaign, VibeShowcase, GoogleMap, SocialHub, AIChat) are enabled.
        /// Anonymous — KhachLink needs this to render /store/{slug} page.
        /// </summary>
        [HttpGet("{tenantId:guid}/feature-settings")]
        [AllowAnonymous]
        public async Task<ActionResult<ShopFeatureSettingsDto>> GetFeatureSettings(Guid tenantId)
        {
            try
            {
                var settings = await _featureSettingsService.GetSettingsAsync(tenantId);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting feature settings for tenant {TenantId}", tenantId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Store Finder — find tenants near a GPS location.
        /// Replaces GET /api/shops/nearby.
        /// Crawl-to-Onboard (2026-08-26): includes Pending tenants (name only, no phone/address per Luật 91/2025).
        /// Fix #190 (2026-10-14): trả về TenantSearchResultDto WRAPPER (nhất quán với /api/tenants/search —
        /// trước đây trả bare array → KhachLink StoreFinder deserialize SearchResultDto vỡ
        /// "DeserializeUnableToConvertValue Path: $ ... BytePositionInLine: 1").
        /// </summary>
        [HttpGet("nearby")]
        [AllowAnonymous]
        public async Task<ActionResult<TenantSearchResultDto>> GetNearby(
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] double radiusKm = 10.0,
            CancellationToken ct = default)
        {
            if (!lat.HasValue || !lng.HasValue)
                return BadRequest("lat and lng query parameters are required");

            try
            {
                return Ok(await _tenantSearch.GetNearbyAsync(lat.Value, lng.Value, radiusKm, ct));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding nearby tenants");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Search tenants by name OR by FeaturedProduct.DisplayName (PG-only, no ShopERP call).
        /// Replaces GET /api/shops/search.
        ///
        /// Issue #166 comment (2026-08-27) — multi-level search + ordering:
        /// 1. Search: exact → contains → token-split → prefix → suggested (avoid empty results).
        ///    If no match found, returns top Active tenants + SuggestedKeywords extracted from
        ///    tenant name tokens that are similar to the input keyword.
        /// 2. Ordering: Active (verified) first, then Pending. Within each status group:
        ///    name-match-relevance (exact > starts-with > contains > token > prefix) → distance ascending.
        ///
        /// Crawl-to-Onboard (2026-08-26): includes Pending tenants (name only, IsPending=true, no phone/address per Luật 91/2025).
        /// W3-2c: Pattern B — normalized (bỏ dấu) contains/token/fuzzy + DidYouMean.
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<TenantSearchResultDto>> Search(
            [FromQuery] string? name,
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            CancellationToken ct = default)
        {
            try
            {
                return Ok(await _tenantSearch.SearchAsync(name, lat, lng, ct));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tenants by name");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
