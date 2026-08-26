namespace VanAn.Crawler.Options;

/// <summary>
/// Configuration for the Crawler worker service.
/// Bound from "Crawler" section in appsettings.json.
/// </summary>
public sealed class CrawlerOptions
{
    /// <summary>Gateway base URL for posting crawled listings + auth login.</summary>
    public string GatewayBaseUrl { get; set; } = "http://localhost:5001";

    /// <summary>SysAdmin service account username for Gateway JWT login.</summary>
    public string GatewayUsername { get; set; } = "";

    /// <summary>SysAdmin service account password for Gateway JWT login.</summary>
    public string GatewayPassword { get; set; } = "";

    /// <summary>Default delay between API calls in milliseconds (rate limiting).</summary>
    public int DefaultRateLimitMs { get; set; } = 2000;

    /// <summary>Max listings per batch POST to Gateway (Gateway enforces 500 max).</summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>HTTP listen port for trigger endpoint (correction C3 — NOT 5003).</summary>
    public int ListenPort { get; set; } = 5010;

    /// <summary>User-Agent string for HTML scraping (ToS compliance — identifiable contact).</summary>
    public string UserAgent { get; set; } = "VanAnCrawler/1.0 (+contact@vanan.vn)";

    /// <summary>doanhnghiep.vn free tier limit — 100 requests per day.</summary>
    public int DoanhNghiepDailyLimit { get; set; } = 100;
}
