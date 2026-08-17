namespace VanAn.CoreHub.Services;

/// <summary>
/// Dynamic CORS origin validator. Singleton — application-level cache, not request-level state.
/// Checks static origins (config) + dynamic origins (KhachLinkInstance registry, cached 5 min).
/// CORS callback calls IsOriginAllowed() which reads IMemoryCache only — no DB call, no blocking.
/// </summary>
public interface IDynamicCorsService
{
    /// <summary>
    /// Check if an origin is allowed for CORS. Sync, read-only cache lookup.
    /// Never calls DB — cache is pre-warmed by DynamicCorsCacheHostedService on startup + every 5 min.
    /// </summary>
    bool IsOriginAllowed(string origin);
}
