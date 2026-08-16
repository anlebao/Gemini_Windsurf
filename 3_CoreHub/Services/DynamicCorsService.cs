using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Dynamic CORS origin validator. Singleton — application-level cache, not request-level state.
/// Follows FeatureFlagService pattern: IMemoryCache + IConfiguration, no IServiceScopeFactory
/// (DB refresh is handled by DynamicCorsCacheHostedService, not this service).
///
/// IsOriginAllowed() is sync read-only — never calls DB, never blocks.
/// Cache is pre-warmed by DynamicCorsCacheHostedService on startup + every 5 min.
/// </summary>
public class DynamicCorsService : IDynamicCorsService
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DynamicCorsService> _logger;

    // Cache key for the registry snapshot (HashSet<string> of allowed origins)
    internal const string SnapshotKey = "cors_origin_snapshot";
    internal const string StaticOriginsKey = "cors_static_origins";

    public DynamicCorsService(
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<DynamicCorsService> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsOriginAllowed(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return false;

        var normalized = origin.TrimEnd('/').ToLowerInvariant();

        // 1. Check static origins (from config, loaded once with NeverRemove)
        var staticOrigins = GetStaticOrigins();
        if (staticOrigins.Contains(normalized))
            return true;

        // 2. Check dynamic snapshot (from KhachLinkInstance registry, refreshed by HostedService)
        if (_cache.TryGetValue<HashSet<string>>(SnapshotKey, out var snapshot))
            return snapshot.Contains(normalized);

        // Cache not yet warmed (startup race) → conservative: reject
        // HostedService will warm within seconds of startup
        _logger.LogDebug("IsOriginAllowed: cache not warmed for {Origin} (startup race) — rejecting", normalized);
        return false;
    }

    private HashSet<string> GetStaticOrigins()
    {
        return _cache.GetOrCreate(StaticOriginsKey, entry =>
        {
            entry.SetPriority(CacheItemPriority.NeverRemove);
            var origins = _configuration.GetSection("Cors:StaticOrigins").Get<string[]>() ?? [];
            return new HashSet<string>(
                origins.Select(o => o.TrimEnd('/').ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);
        })!;
    }
}
