using VanAn.Shared.Services;

using VanAn.CoreHub.Services;

using VanAn.UI.Platform.Core.Interfaces;

using VanAn.UI.Platform.Services;

using VanAn.UI.Platform.Adapters;

using VanAn.KhachLink.Components;

using Microsoft.AspNetCore.HttpOverrides;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Tests")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Integration.Tests")]



namespace VanAn.KhachLink

{

    public partial class Program

    {

        public static async Task Main(string[] args)

        {

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);



            // Add services to the container.

            _ = builder.Services.AddRazorPages();

            _ = builder.Services.AddRazorComponents()
                              .AddInteractiveServerComponents();

            _ = builder.Services.AddLogging();



            // UI Platform Services

            _ = builder.Services.AddScoped<ICssAdapter, BootstrapAdapter>();

            _ = builder.Services.AddScoped<IThemeProvider, ThemeProvider>();

            _ = builder.Services.AddScoped<ITenantService, TenantService>();



            // Register KhachLink HTTP-backed services (call Gateway, no direct DB access)
            // ARCHITECTURAL NOTE: KhachLink MUST use HTTP via Gateway only — no CoreHub DI.
            // IOnboardingService + IVoiceCommandService removed: not used in any component.
            _ = builder.Services.AddScoped<IOrderWorkflowService, Services.Http.OrderWorkflowHttpService>();
            _ = builder.Services.AddScoped<ISocialCampaignService, Services.Http.SocialCampaignHttpService>();
            _ = builder.Services.AddScoped<IDashboardService, Services.Http.DashboardHttpService>();

            // ShopConfigHttpService (Phase 2 — product-based, HTTP via Gateway only).
            // Loads real Shop data: products → TenantId → GET /api/shops/by-tenant/{tenantId}.
            // No CoreHub dependency. Wired into KhachLinkLayout + Home.razor in Phase 3.
            // Replaces the former IShopConfigService (CoreHub direct inject) — architectural violation fixed.
            _ = builder.Services.AddScoped<Services.Http.ShopConfigHttpService>();



            // Register Cart Services

            _ = builder.Services.AddScoped<Services.CartService>();

            _ = builder.Services.AddScoped<Services.CheckoutFlowState>();



            // Register PWA Services

            _ = builder.Services.AddScoped<Services.PWA.PWAService>();

            _ = builder.Services.AddHttpClient("gateway", client =>

            {

                client.BaseAddress = new Uri(

                    builder.Configuration["Gateway:BaseUrl"]

                        ?? throw new InvalidOperationException(

                            "Gateway:BaseUrl is required. Add it to appsettings.json."));

            }); // For Checkout flow API calls



            // Register Product Catalog Service (Wave 13: real API call replaces hardcoded data)

            _ = builder.Services.AddScoped<Services.Http.ProductHttpService>();

            // KhachLink Full Flow W0: Shop feature toggle settings (HTTP via Gateway)
            _ = builder.Services.AddScoped<Services.Http.ShopFeatureSettingsHttpService>();

            // Register Recently Viewed Service (Wave 8: Product Personalization)
            _ = builder.Services.AddScoped<Services.RecentlyViewedService>();

            // FIX-BATCH-4: SignalR + RealTimeDashboardService removed from KhachLink.
            // Staff dashboard now uses HTTP polling (RealTimeDashboard.razor → GET /api/dashboard/shop-metrics/{shopId}).
            // SignalR remains in ShopERP for Kitchen Display only (staff count << 10,000).


            // Add Memory Cache (used by HTTP services for optional caching)
            _ = builder.Services.AddMemoryCache();



            WebApplication app = builder.Build();



            // REMOVED: EnsureCreatedAsync - KhachLink uses Gateway API, not direct DB access



            // Configure the HTTP request pipeline.

            if (!app.Environment.IsDevelopment())

            {

                _ = app.UseExceptionHandler("/Error");

                _ = app.UseHsts();

            }



            // Local-First: DISABLE HTTPS REDIRECTION for development

            // app.UseHttpsRedirection();

            // Forwarded headers for nginx reverse proxy (Docker networking)
            _ = app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                   ForwardedHeaders.XForwardedProto |
                                   ForwardedHeaders.XForwardedHost,
                // Clear loopback restrictions for Docker networking
                KnownProxies = { },
                KnownNetworks = { }
            });


            _ = app.UseStaticFiles();

            _ = app.UseRouting();

            _ = app.UseAuthorization();

            _ = app.UseAntiforgery();



            _ = app.MapRazorPages();

            _ = app.MapRazorComponents<App>()
                   .AddInteractiveServerRenderMode();


            // FIX-BATCH-4: SignalR Hub mapping removed (no WebSocket connections from KhachLink).


            // Razor Pages fallback removed; Blazor Router handles unmatched routes.

            _ = app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "VanAn KhachLink", Timestamp = DateTime.UtcNow }));



            string urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5002";

            await app.RunAsync(urls);

        }

    }



    public partial class Program { }

}
