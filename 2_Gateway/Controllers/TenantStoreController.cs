using Microsoft.AspNetCore.Authorization;
using VanAn.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
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
        ///
        /// Crawl-to-Onboard (2026-08-25, M3): Pending tenants return Phone=null + Email=null
        /// (HIDE SĐT section entirely per Luật 91/2025 Điều 16 — không "công khai" dữ liệu cá nhân chưa consent).
        /// Active tenants return full profile. Suspended/Inactive/Converted → 404 (don't expose).
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

                // Crawl-to-Onboard (M3): hide Pending profile SĐT, 404 for non-Active/Pending
                if (tenant.Status == TenantStatus.Suspended
                    || tenant.Status == TenantStatus.Inactive
                    || tenant.Status == TenantStatus.Converted)
                {
                    return NotFound();
                }

                var dto = MapToStoreDto(tenant);

                if (tenant.Status == TenantStatus.Pending)
                {
                    // M3: HIDE SĐT section — Phone=null, Email=null (tránh "công khai" per Luật 91/2025 Điều 16)
                    dto = dto with
                    {
                        Phone = null,
                        Email = null,
                        IsPending = true,
                        ClaimUrl = $"/store/{normalizedSlug}/claim"
                    };
                }

                return Ok(dto);
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
                    .Where(t => t.Status == TenantStatus.Active || t.Status == TenantStatus.Pending)
                    .ToListAsync();

                var nearby = tenants
                    .Where(t => t.Settings?.Latitude.HasValue == true && t.Settings?.Longitude.HasValue == true)
                    .Select(t => new { Tenant = t, Distance = HaversineKm(lat.Value, lng.Value, t.Settings!.Latitude!.Value, t.Settings!.Longitude!.Value) })
                    .Where(x => x.Distance <= radiusKm)
                    .OrderBy(x => x.Distance)
                    .ToList();

                // Batch query KhachLink instances for nearby tenants (1 query, no N+1)
                var nearbyTenantIds = nearby.Select(x => x.Tenant.Id.Value).ToList();
                var khachLinkDomainMap = await BuildKhachLinkDomainMapAsync(nearbyTenantIds);

                return Ok(nearby.Select(x => MapToStoreDto(x.Tenant, x.Distance, khachLinkDomainMap)).ToList());
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
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<TenantSearchResultDto>> Search([FromQuery] string? name, [FromQuery] double? lat, [FromQuery] double? lng)
        {
            try
            {
                var baseQuery = _dbContext.Tenants
                    .AsNoTracking()
                    .IgnoreQueryFilters() // public endpoint — show all tenants regardless of caller's tenant context
                    .Where(t => t.Status == TenantStatus.Active || t.Status == TenantStatus.Pending);

                string matchStrategy = "all"; // all | contains | token | prefix | suggested
                List<Tenant> tenants;

                if (string.IsNullOrWhiteSpace(name))
                {
                    // No keyword — return all active tenants (legacy behavior, Take 50)
                    tenants = await baseQuery.Take(50).ToListAsync();
                    matchStrategy = "all";
                }
                else
                {
                    var q = name.Trim();

                    // ── Level 1: ILIKE contains on Name OR FeaturedProduct.DisplayName ──
                    var matched = await baseQuery
                        .Where(t => EF.Functions.ILike(t.Name, $"%{q}%")
                                    || _dbContext.FeaturedProducts.Any(fp => fp.TenantId == t.Id && fp.IsActive && EF.Functions.ILike(fp.DisplayName, $"%{q}%")))
                        .Take(50)
                        .ToListAsync();

                    if (matched.Count > 0)
                    {
                        matchStrategy = "contains";
                        tenants = matched;
                    }
                    else
                    {
                        // ── Level 2: Token-based match — split keyword by spaces, match ANY token ──
                        var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length > 1)
                        {
                            // Build OR predicate: Name ILIKE %token1% OR Name ILIKE %token2% OR ...
                            matched = await baseQuery
                                .Where(t => tokens.Any(tok => EF.Functions.ILike(t.Name, $"%{tok}%")))
                                .Take(50)
                                .ToListAsync();
                        }

                        if (matched.Count > 0)
                        {
                            matchStrategy = "token";
                            tenants = matched;
                        }
                        else
                        {
                            // ── Level 3: Prefix match — Name starts with keyword (first 3+ chars) ──
                            var prefix = q.Length >= 3 ? q : q;
                            matched = await baseQuery
                                .Where(t => EF.Functions.ILike(t.Name, $"{prefix}%"))
                                .Take(50)
                                .ToListAsync();

                            if (matched.Count > 0)
                            {
                                matchStrategy = "prefix";
                                tenants = matched;
                            }
                            else
                            {
                                // ── Level 4: Suggested — return top 10 Active tenants (avoid empty) ──
                                tenants = await baseQuery
                                    .Where(t => t.Status == TenantStatus.Active)
                                    .OrderBy(t => t.Name)
                                    .Take(10)
                                    .ToListAsync();
                                matchStrategy = "suggested";
                            }
                        }
                    }
                }

                // Batch query KhachLink instances for all matched tenants (1 query, no N+1)
                var tenantIds = tenants.Select(t => t.Id.Value).ToList();
                var khachLinkDomainMap = await BuildKhachLinkDomainMapAsync(tenantIds);

                // Compute distance for each store + sort by Issue #166 ordering rules
                double? userLat = lat, userLng = lng;
                var qForSort = name?.Trim() ?? "";

                var dtos = tenants.Select(t =>
                {
                    double? dist = null;
                    if (userLat.HasValue && userLng.HasValue
                        && t.Settings?.Latitude.HasValue == true && t.Settings?.Longitude.HasValue == true)
                    {
                        dist = HaversineKm(userLat.Value, userLng.Value, t.Settings!.Latitude!.Value, t.Settings!.Longitude!.Value);
                    }
                    return MapToStoreDto(t, dist, khachLinkDomainMap);
                }).ToList();

                // Issue #166 ordering: Active first, then Pending; within each: relevance, then distance
                dtos = dtos
                    .OrderByDescending(d => !d.IsPending) // Active (false→1) before Pending (true→0)
                    .ThenByDescending(d =>
                        string.Equals(d.Name, qForSort, StringComparison.OrdinalIgnoreCase) ? 5
                        : d.Name.StartsWith(qForSort, StringComparison.OrdinalIgnoreCase) ? 4
                        : d.Name.Contains(qForSort, StringComparison.OrdinalIgnoreCase) ? 3
                        : matchStrategy == "token" ? 2
                        : matchStrategy == "prefix" ? 1
                        : 0)
                    .ThenBy(d => d.DistanceKm ?? double.MaxValue) // closer first (if location provided)
                    .ThenBy(d => d.Name)
                    .ToList();

                // Build suggested keywords (only when no direct match found)
                List<string> suggestedKeywords = new();
                if (matchStrategy is "token" or "prefix" or "suggested")
                {
                    // Extract distinct meaningful words from matched tenant names that are similar to input
                    var inputTokens = qForSort.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.ToLowerInvariant())
                        .ToHashSet();

                    suggestedKeywords = tenants
                        .SelectMany(t => t.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Where(w => w.Length >= 3)
                        .Select(w => w.ToLowerInvariant())
                        .Where(w => !inputTokens.Contains(w))
                        .Distinct()
                        .Take(5)
                        .ToList();
                }

                return Ok(new TenantSearchResultDto
                {
                    Results = dtos,
                    SuggestedKeywords = suggestedKeywords,
                    MatchStrategy = matchStrategy
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tenants by name");
                return StatusCode(500, "Internal server error");
            }
        }

        private static TenantStoreDto MapToStoreDto(Tenant t, double? distanceKm = null,
            Dictionary<Guid, string>? khachLinkDomainMap = null)
        {
            var isPending = t.Status == TenantStatus.Pending;
            return new()
            {
                Id = t.Id.Value,
                Name = t.Name,
                // Pending: hide address/phone/email per Luật 91/2025 Điều 16 — only show name
                Address = isPending ? string.Empty : (t.Settings?.Address ?? string.Empty),
                Phone = isPending ? string.Empty : (t.Settings?.ContactPhone ?? string.Empty),
                Email = isPending ? string.Empty : (t.Settings?.ContactEmail ?? string.Empty),
                Latitude = isPending ? null : t.Settings?.Latitude,
                Longitude = isPending ? null : t.Settings?.Longitude,
                DistanceKm = distanceKm,
                Slug = t.Settings?.Slug,
                SocialLinksFb = isPending ? null : t.Settings?.SocialLinksFb,
                SocialLinksTiktok = isPending ? null : t.Settings?.SocialLinksTiktok,
                BrandStory = isPending ? null : t.Settings?.BrandStory,
                LogoUrl = isPending ? null : t.Settings?.LogoUrl,
                Theme = t.Settings?.Theme ?? ThemeType.Classic,
                NavColor = t.Settings?.NavColor,
                HeaderColor = t.Settings?.HeaderColor,
                FooterColor = t.Settings?.FooterColor,
                // Pending: no KhachLink domain link (don't redirect to profile)
                KhachLinkDomain = isPending ? null : khachLinkDomainMap?.GetValueOrDefault(t.Id.Value),
                IsPending = isPending,
                ClaimUrl = isPending && !string.IsNullOrEmpty(t.Settings?.Slug)
                    ? $"/store/{t.Settings.Slug}/claim"
                    : null
            };
        }

        /// <summary>
        /// Batch query KhachLink instances for given tenant IDs.
        /// Returns map of OwnerTenantId → CustomDomain for active non-Directory instances.
        /// Uses GroupBy to handle tenants with multiple instances (prefers FullCommerce > Reseller).
        /// </summary>
        private async Task<Dictionary<Guid, string>> BuildKhachLinkDomainMapAsync(List<Guid> tenantIds)
        {
            if (tenantIds.Count == 0)
                return new Dictionary<Guid, string>();

            return (await _dbContext.KhachLinkInstances
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(i => i.OwnerTenantId != null
                    && tenantIds.Contains(i.OwnerTenantId.Value)
                    && i.IsActive
                    && i.Profile != KhachLinkProfile.Directory)
                .OrderBy(i => i.Profile) // FullCommerce=0, Reseller=4 — prefer FullCommerce
                .ToListAsync())
                .GroupBy(i => i.OwnerTenantId!.Value)
                .ToDictionary(g => g.Key, g => g.First().CustomDomain);
        }

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
        /// <summary>Distance from user location (km). Null if user location not shared or store has no coordinates.</summary>
        public double? DistanceKm { get; init; }
        /// <summary>Tenant Profile Page (2026-07-21): URL slug for /store/{slug}. Null if not set.</summary>
        public string? Slug { get; init; }
        public string? SocialLinksFb { get; init; }
        public string? SocialLinksTiktok { get; init; }
        public string? BrandStory { get; init; }
        public string? LogoUrl { get; init; }
        public ThemeType Theme { get; init; } = ThemeType.Classic;
        /// <summary>#93 — KhachLink style customization colors.</summary>
        public string? NavColor { get; init; }
        public string? HeaderColor { get; init; }
        public string? FooterColor { get; init; }

        /// <summary>Directory redirect: KhachLink instance CustomDomain for this tenant (if any).
        /// Null = tenant has no KhachLink instance → "Tìm hiểu" button hidden.
        /// Non-null = button redirects to https://{KhachLinkDomain}/store/{slug}</summary>
        public string? KhachLinkDomain { get; init; }

        /// <summary>Crawl-to-Onboard (2026-08-25, M3): True if tenant is Pending (crawled, not yet verified).
        /// KhachLink hides commerce UI + shows Pending banner + Claim button when true.
        /// SĐT section HIDDEN (Phone=null) per Luật 91/2025 Điều 16.</summary>
        public bool IsPending { get; init; }

        /// <summary>Crawl-to-Onboard (2026-08-25): URL to Claim form for Pending tenants.
        /// Null for Active tenants. Format: /store/{slug}/claim</summary>
        public string? ClaimUrl { get; init; }
    }

    /// <summary>
    /// Issue #166 comment (2026-08-27): Search response wrapper with results + suggested keywords.
    /// When MatchStrategy is "suggested"/"token"/"prefix", SuggestedKeywords contains distinct
    /// words from matched tenant names that are similar to the input keyword (for "Did you mean...?" UI).
    /// </summary>
    public record TenantSearchResultDto
    {
        public List<TenantStoreDto> Results { get; init; } = new();
        /// <summary>Suggested keywords extracted from tenant names when no direct match found.
        /// Empty list when matchStrategy is "all" or "contains" (direct match — no suggestions needed).</summary>
        public List<string> SuggestedKeywords { get; init; } = new();
        /// <summary>How results were matched: "all" (no keyword) | "contains" | "token" | "prefix" | "suggested".
        /// UI uses this to show "Không tìm thấy kết quả chính xác, gợi ý cho bạn:" message.</summary>
        public string MatchStrategy { get; init; } = "all";
    }
}
