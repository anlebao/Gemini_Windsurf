namespace VanAn.Gateway.Services;

/// <summary>
/// Configuration for the doanhnghiep.vn business-info lookup proxy (2026-10-01).
/// Bound from the "BusinessLookup" config section (appsettings.json + docker-compose env).
/// </summary>
public sealed class BusinessLookupOptions
{
    /// <summary>doanhnghiep.vn API key — sent via `x-api-key` header. Empty = lookup returns 502 with clear message.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Max doanhnghiep.vn calls per day from this proxy (free tier = 100 req/day; crawler has its own counter). Default 50.</summary>
    public int DailyLimit { get; set; } = 50;

    /// <summary>Cache TTL for successful lookups (company data changes rarely — saves quota). Default 1440 min = 24h.</summary>
    public int CacheMinutes { get; set; } = 1440;

    /// <summary>Base URL override (tests). Default https://doanhnghiep.vn.</summary>
    public string BaseUrl { get; set; } = "https://doanhnghiep.vn";
}
