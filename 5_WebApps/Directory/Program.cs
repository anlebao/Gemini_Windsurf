using VanAn.Directory.Components;
using VanAn.Directory.Services;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Extensions;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Blazor SSR (.NET 8 interactive server render mode)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// IHttpContextAccessor — DirectoryLayout reads Request.Host.Host to resolve domain (server-side)
builder.Services.AddHttpContextAccessor();

// UI Platform — VanAnButton/VanAnCard inject ICssAdapter
builder.Services.AddScoped<ICssAdapter, BootstrapAdapter>();

// Realtime Platform P4 (F4): realtime chat/map clients + endpoint provider
// (host-agnostic RCL registration — the 3rd host referencing UI.Platform).
builder.Services.AddRealtimePlatform();

// JSON options — Gateway API returns enums as strings (e.g. "theme":"Classic")
// System.Text.Json default uses numbers for enums → JsonException
var gatewayJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
gatewayJsonOptions.Converters.Add(new JsonStringEnumConverter());
builder.Services.AddSingleton(gatewayJsonOptions);

// HttpClient cho Gateway API calls
builder.Services.AddHttpClient<InstanceConfigService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5001/");
});
builder.Services.AddHttpClient<ShopConfigService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5001/");
});
builder.Services.AddHttpClient<CatalogService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5001/");
});
// GTM Drill Machine W1 (2026-09-06): Merchant Audit — forwards end-user IP via X-Forwarded-For
// so Gateway growth-audit rate limiter partitions by the user's IP (see GrowthAuditService doc).
builder.Services.AddHttpClient<GrowthAuditService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5001/");
});

// Phí nền tảng P4: usage metering client — thin per-host wrapper (task_p4 D2).
// Missing Metering:* config → client self-disables (staging works without metering).
builder.Services.AddHttpClient<VanAn.Shared.Domain.Common.IMeteringClient, MeteringApiClient>(client =>
{
    var baseUrl = builder.Configuration["Metering:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl)) client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(3);
    var callerId = builder.Configuration["Metering:CallerId"];
    var callerKey = builder.Configuration["Metering:CallerKey"];
    if (!string.IsNullOrEmpty(callerId)) client.DefaultRequestHeaders.TryAddWithoutValidation("X-Caller-Id", callerId);
    if (!string.IsNullOrEmpty(callerKey)) client.DefaultRequestHeaders.TryAddWithoutValidation("X-Metering-Key", callerKey);
});

// IMemoryCache — cache instance config + store data 5 phút
builder.Services.AddMemoryCache();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Health check endpoint cho Docker + nginx
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "VanAn Directory SSR" }));

app.Run();
