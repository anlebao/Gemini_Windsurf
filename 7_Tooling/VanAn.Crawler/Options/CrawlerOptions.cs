namespace VanAn.Crawler.Options;

/// <summary>
/// Configuration for the Crawler worker service.
/// Bound from "Crawler" section in appsettings.json.
/// </summary>
public sealed class CrawlerOptions
{
    /// <summary>Gateway base URL for posting crawled listings (POST /api/v1/crawl/batch).</summary>
    public string GatewayBaseUrl { get; set; } = "http://localhost:5001";

    /// <summary>ShopERP base URL for JWT login (POST /api/platform/login — lives on ShopERP, not Gateway).</summary>
    public string AuthBaseUrl { get; set; } = "";

    /// <summary>SysAdmin service account username for JWT login.</summary>
    public string GatewayUsername { get; set; } = "";

    /// <summary>SysAdmin service account password for JWT login.</summary>
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

    /// <summary>
    /// doanhnghiep.vn API key (2026-09-14: public no-key tier DISABLED — 401 key_required).
    /// Sent as `x-api-key: <key>` header (or `authorization: Bearer <key>`). Register FREE:
    /// https://doanhnghiep.vn/api/docs — contact lienhe@doanhnghiep.vn.
    /// Empty = requests will 401 (crawl returns 0 listings with a clear error).
    /// </summary>
    public string DoanhNghiepApiKey { get; set; } = "";

    // ── 2026-09-22: Fallback MST lookup — Tổng cục Thuế tracuunnt.gdt.gov.vn ──
    // Official tax registry (authoritative for MST validity + status). Captcha-gated:
    // GET page (cookies) → GET captcha.png → OCR (tesseract) → POST form.
    // Usage policy: SINGLE-MST lookups ONLY (feature "đăng ký tenant bằng MST"),
    // strict rate limit + small daily cap — the captcha is the site's own anti-spam gate.

    /// <summary>Master switch — default OFF (opt-in). When ON, missing MSTs after the
    /// doanhnghiep.vn lookup fall back to tracuunnt.gdt.gov.vn (one request per MST).</summary>
    public bool GdtLookupEnabled { get; set; } = false;

    /// <summary>Daily cap for GDT lookups (polite — captcha-gated site). Default 30.</summary>
    public int GdtMaxPerDay { get; set; } = 30;

    /// <summary>Captcha retries per MST (OCR is not 100%). Default 3.</summary>
    public int GdtCaptchaRetries { get; set; } = 3;

    /// <summary>Delay between GDT requests (ms). Default 5000 — keep it polite.</summary>
    public int GdtRateLimitMs { get; set; } = 5000;

    /// <summary>tesseract binary path (Dockerfile installs tesseract-ocr). Default "tesseract".</summary>
    public string GdtOcrPath { get; set; } = "tesseract";
}
