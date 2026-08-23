using VanAn.Directory.Components;
using VanAn.Directory.Services;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Adapters;
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
