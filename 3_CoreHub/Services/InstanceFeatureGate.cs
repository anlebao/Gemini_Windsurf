using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Instance Feature Gate (2026-11-27): resolve the calling portal's KhachLinkNavFlags from
    /// the <see cref="DomainHeader"/> request header, then apply kill-switch overrides onto
    /// tenant ShopFeatureSettings — SysAdmin (KhachLinkInstance) OFF wins over tenant ON.
    ///
    /// Semantics: effective = tenantFlag AND instanceFlag. Instance ON does NOT force ON
    /// (tenant autonomy preserved). Missing/unknown/inactive domain → null → no override
    /// (backward compat for direct API consumers without the header).
    ///
    /// Used by anonymous read endpoints that power customer UI
    /// (feature-settings, campaigns) — NOT by owner/admin write paths.
    /// Registered scoped in both Gateway and ShopERP (both resolve concrete VanAnDbContext → PG).
    /// </summary>
    public interface IInstanceFeatureGate
    {
        /// <summary>NavFlags of the portal instance for the current request, or null when
        /// header absent / domain unknown / instance inactive.</summary>
        Task<KhachLinkNavFlags?> GetNavFlagsAsync(CancellationToken ct = default);

        /// <summary>Apply kill-switch overrides for concepts shared between NavFlags and
        /// ShopFeatureSettings. Returns the same DTO instance with mapped flags ANDed.</summary>
        ShopFeatureSettingsDto ApplyOverrides(ShopFeatureSettingsDto settings, KhachLinkNavFlags? flags);
    }

    public sealed class InstanceFeatureGate(
        IHttpContextAccessor httpContextAccessor,
        VanAnDbContext dbContext,
        IMemoryCache cache,
        ILogger<InstanceFeatureGate> logger) : IInstanceFeatureGate
    {
        public const string DomainHeader = "X-KhachLink-Domain";
        private const string CachePrefix = "instance-navflags:";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        public async Task<KhachLinkNavFlags?> GetNavFlagsAsync(CancellationToken ct = default)
        {
            string? domain = httpContextAccessor.HttpContext?.Request.Headers[DomainHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(domain))
                return null;

            string normalized = domain.Trim().ToLowerInvariant();

            if (cache.TryGetValue(CachePrefix + normalized, out KhachLinkNavFlags? cached))
                return cached;

            try
            {
                // KhachLinkInstance is platform-level (TenantId=Empty sentinel) — excluded from
                // tenant query filter. IsActive=false → treat as unregistered (matches
                // by-domain endpoint 404 → client FullCommerce fallback).
                var instance = await dbContext.KhachLinkInstances
                    .Where(i => i.IsActive && i.CustomDomain == normalized)
                    .FirstOrDefaultAsync(ct);

                var flags = instance?.NavFlags;
                cache.Set(CachePrefix + normalized, flags, CacheTtl);
                return flags;
            }
            catch (Exception ex)
            {
                // Fail-open: never break customer-facing reads because gate lookup failed.
                logger.LogWarning(ex, "InstanceFeatureGate: lookup failed for domain {Domain} — no override applied", normalized);
                return null;
            }
        }

        public ShopFeatureSettingsDto ApplyOverrides(ShopFeatureSettingsDto settings, KhachLinkNavFlags? flags)
        {
            if (flags is null)
                return settings;

            // Concept map (approved 2026-11-27): instance flag is the master kill-switch.
            settings.Campaign_Section_Enabled = settings.Campaign_Section_Enabled && flags.ShowCampaigns;
            settings.Home_CampaignSection_Enabled = settings.Home_CampaignSection_Enabled && flags.ShowCampaigns;
            settings.Home_StoreSection_Enabled = settings.Home_StoreSection_Enabled && flags.ShowStores;
            settings.QR_TableNumber_Enabled = settings.QR_TableNumber_Enabled && flags.ShowScan;
            return settings;
        }
    }
}
