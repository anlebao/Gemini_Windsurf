using VanAn.Crawler.Adapters;
using VanAn.Crawler.Auth;
using VanAn.Crawler.Dtos;
using VanAn.Crawler.Options;
using VanAn.Crawler.Workers;

var builder = WebApplication.CreateBuilder(args);

// Bind CrawlerOptions from configuration
builder.Services.Configure<CrawlerOptions>(builder.Configuration.GetSection("Crawler"));
var crawlerOptions = builder.Configuration.GetSection("Crawler").Get<CrawlerOptions>() ?? new CrawlerOptions();
// Register CrawlerOptions as singleton so CrawlerCoordinator + adapters can inject it directly
// (not just IOptions<CrawlerOptions>)
builder.Services.AddSingleton(crawlerOptions);

// Register GatewayAuthHandler as a DelegatingHandler
builder.Services.AddTransient<GatewayAuthHandler>();

// Named HttpClient "gateway" — with auth handler for Gateway API calls
builder.Services.AddHttpClient("gateway", client =>
{
    client.BaseAddress = new Uri(crawlerOptions.GatewayBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<GatewayAuthHandler>();

// Named HttpClient "doanhnghiep" — for doanhnghiep.vn API (no auth needed, free tier 100 req/day)
builder.Services.AddHttpClient("doanhnghiep", client =>
{
    client.BaseAddress = new Uri("https://doanhnghiep.vn");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Named HttpClient "trangvang" — for trangvangvietnam.com HTML scraping
builder.Services.AddHttpClient("trangvang", client =>
{
    client.BaseAddress = new Uri("https://trangvangvietnam.com");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(crawlerOptions.UserAgent);
});

// 2026-09-22 fallback MST source: tracuunnt.gdt.gov.vn (Tổng cục Thuế).
// Browser-like headers are REQUIRED — the GDT WAF rejects bare requests
// ("Request Rejected"). A dedicated CookieContainer keeps the session across
// page → captcha → POST (the captcha is bound to the session cookie).
var gdtCookieContainer = new System.Net.CookieContainer();
builder.Services.AddHttpClient("gdt", client =>
{
    client.BaseAddress = new Uri("https://tracuunnt.gdt.gov.vn");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("vi-VN,vi;q=0.9,en;q=0.8");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    CookieContainer = gdtCookieContainer,
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
});

// Register adapters
builder.Services.AddSingleton<IDataSourceAdapter>(sp =>
    new RestApiAdapter(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("doanhnghiep"),
        crawlerOptions,
        sp.GetRequiredService<ILogger<RestApiAdapter>>(),
        "doanhnghiep.vn",
        "https://doanhnghiep.vn"));

// 2026-09-22: GDT MST lookup fallback (captcha OCR — opt-in, rate-limited)
builder.Services.AddSingleton(sp =>
    new GdtMstLookupSource(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("gdt"),
        crawlerOptions,
        sp.GetRequiredService<ILogger<GdtMstLookupSource>>()));

builder.Services.AddSingleton<IDataSourceAdapter>(sp =>
    new TrangVangHtmlAdapter(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("trangvang"),
        crawlerOptions,
        sp.GetRequiredService<ILogger<TrangVangHtmlAdapter>>()));

// Register CrawlerCoordinator — both as hosted service (background) and singleton (for trigger endpoint)
// Register as singleton first, then AddHostedService<>() wrapper resolves the same singleton instance.
builder.Services.AddSingleton<CrawlerCoordinator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CrawlerCoordinator>());

var app = builder.Build();

// ── HTTP endpoints (port 5010 — correction C3, NOT 5003) ──────────────────

app.MapPost("/trigger", async (CrawlTriggerRequest request, CrawlerCoordinator coordinator, CancellationToken ct) =>
{
    var result = await coordinator.RunCrawlAsync(request, ct);
    return Results.Ok(result);
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "VanAn Crawler",
    timestamp = DateTime.UtcNow
}));

// GET /status — polled by ShopERP UI for crawl progress (running phase + last result)
app.MapGet("/status", () => Results.Ok(CrawlerCoordinator.GetStatus()));

app.Run($"http://0.0.0.0:{crawlerOptions.ListenPort}");
