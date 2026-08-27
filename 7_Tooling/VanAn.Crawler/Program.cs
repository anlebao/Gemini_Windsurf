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

// Register adapters
builder.Services.AddSingleton<IDataSourceAdapter>(sp =>
    new RestApiAdapter(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("doanhnghiep"),
        crawlerOptions,
        sp.GetRequiredService<ILogger<RestApiAdapter>>(),
        "doanhnghiep.vn",
        "https://doanhnghiep.vn"));

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
