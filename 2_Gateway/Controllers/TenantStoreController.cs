using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Public Store Finder + Tenant Profile Page endpoints.
    /// Replaced ShopsController (Shop entity removed 2026-07-21).
    /// Queries Tenant.Settings.Latitude/Longitude for Store Finder functionality.
    /// All endpoints are anonymous (KhachLink customer app is unauthenticated).
    /// </summary>
    [ApiController]
    [Route("api/tenants")]
    public class TenantStoreController(
        IVanAnDbContext dbContext,
        IShopFeatureSettingsService featureSettingsService,
        ILogger<TenantStoreController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IShopFeatureSettingsService _featureSettingsService = featureSettingsService;
        private readonly ILogger<TenantStoreController> _logger = logger;

        /// <summary>
        /// Get tenant store info (name, address, phone, lat/lng, slug) by TenantId.
        /// Replaces GET /api/shops/by-tenant/{tenantId}.
        /// </summary>
        [HttpGet("{tenantId:guid}/store-info")]
        [AllowAnonymous]
        public async Task<ActionResult<TenantStoreDto>> GetStoreInfo(Guid tenantId)
        {
            try
            {
                // Use IgnoreQueryFilters in case any global filter blocks the query.
                // Tenant.Id is a TenantId value object with HasConversion — compare by constructing
                // a TenantId from the Guid parameter (matches TenantManagementService pattern).
                // NEVER use EF.Property<Guid> for Tenant.Id (Known Error Pattern #1: IConvertible).
                // NEVER use t.Id.Value == tenantId (LINQ translation fails for value object member).
                var tenant = await _dbContext.Tenants
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId));

                if (tenant == null)
                    return NotFound();

                return Ok(MapToStoreDto(tenant));
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
        /// </summary>
        [HttpGet("by-slug/{slug}")]
        [AllowAnonymous]
        public async Task<ActionResult<TenantStoreDto>> GetBySlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return NotFound();

            try
            {
                // Normalize: lowercase, trim (matches Tenant.UpdateSlug normalization)
                var normalizedSlug = slug.Trim().ToLowerInvariant();
                var tenant = await _dbContext.Tenants
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Settings != null && t.Settings.Slug == normalizedSlug);

                if (tenant == null)
                    return NotFound();

                return Ok(MapToStoreDto(tenant));
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
        /// </summary>
        [HttpGet("nearby")]
        [AllowAnonymous]
        public async Task<ActionResult<List<TenantStoreDto>>> GetNearby(
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] double radiusKm = 10.0)
        {
            if (!lat.HasValue || !lng.HasValue)
                return BadRequest("lat and lng query parameters are required");

            try
            {
                var tenants = await _dbContext.Tenants
                    .AsNoTracking()
                    .IgnoreQueryFilters() // public endpoint — show all tenants regardless of caller's tenant context
                    .Where(t => t.Status == TenantStatus.Active)
                    .ToListAsync();

                var nearby = tenants
                    .Where(t => t.Settings?.Latitude.HasValue == true && t.Settings?.Longitude.HasValue == true)
                    .Select(t => new { Tenant = t, Distance = HaversineKm(lat.Value, lng.Value, t.Settings!.Latitude!.Value, t.Settings!.Longitude!.Value) })
                    .Where(x => x.Distance <= radiusKm)
                    .OrderBy(x => x.Distance)
                    .Select(x => MapToStoreDto(x.Tenant))
                    .ToList();

                return Ok(nearby);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding nearby tenants");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Search tenants by name or slug.
        /// Replaces GET /api/shops/search.
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<List<TenantStoreDto>>> Search([FromQuery] string? name)
        {
            try
            {
                var query = _dbContext.Tenants
                    .AsNoTracking()
                    .IgnoreQueryFilters() // public endpoint — show all tenants regardless of caller's tenant context
                    .Where(t => t.Status == TenantStatus.Active);
                if (!string.IsNullOrWhiteSpace(name))
                    query = query.Where(t => EF.Functions.ILike(t.Name, $"%{name}%"));

                var tenants = await query.Take(50).ToListAsync();
                return Ok(tenants.Select(MapToStoreDto).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tenants by name");
                return StatusCode(500, "Internal server error");
            }
        }

        private static TenantStoreDto MapToStoreDto(Tenant t) => new()
        {
            Id = t.Id.Value,
            Name = t.Name,
            Address = t.Settings?.Address ?? string.Empty,
            Phone = t.Settings?.ContactPhone ?? string.Empty,
            Email = t.Settings?.ContactEmail ?? string.Empty,
            Latitude = t.Settings?.Latitude,
            Longitude = t.Settings?.Longitude,
            Slug = t.Settings?.Slug,
            SocialLinksFb = t.Settings?.SocialLinksFb,
            SocialLinksTiktok = t.Settings?.SocialLinksTiktok,
            BrandStory = t.Settings?.BrandStory,
            LogoUrl = t.Settings?.LogoUrl
        };

        private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371.0; // Earth radius km
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLng = (lng2 - lng1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }

    public record TenantStoreDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        /// <summary>Tenant Profile Page (2026-07-21): URL slug for /store/{slug}. Null if not set.</summary>
        public string? Slug { get; init; }
        public string? SocialLinksFb { get; init; }
        public string? SocialLinksTiktok { get; init; }
        public string? BrandStory { get; init; }
        public string? LogoUrl { get; init; }
    }
}
