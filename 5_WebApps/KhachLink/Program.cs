using VanAn.Shared.Services;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Services;
using VanAn.UI.Platform.Adapters;
using VanAn.KhachLink.Components;
using VanAn.KhachLink.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Integration.Tests")]

namespace VanAn.KhachLink
{
    public partial class Program
    {
        public static async Task Main(string[] args)
        {
            WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            // UI Platform Services
            _ = builder.Services.AddScoped<ICssAdapter, BootstrapAdapter>();
            _ = builder.Services.AddScoped<IThemeProvider, ThemeProvider>();
            _ = builder.Services.AddScoped<ITenantService, TenantService>();

            // AuthenticationStateProvider: KhachLink WASM is customer-facing (no server auth).
            // Anonymous stub satisfies TenantService's dependency; tenant context comes from
            // LastInteractionService (localStorage), not auth claims.
            _ = builder.Services.AddScoped<AuthenticationStateProvider, AnonymousAuthenticationStateProvider>();

            // Register KhachLink HTTP-backed services (call Gateway, no direct DB access)
            // ARCHITECTURAL NOTE: KhachLink MUST use HTTP via Gateway only â€” no CoreHub DI.
            _ = builder.Services.AddScoped<IOrderWorkflowService, Services.Http.OrderWorkflowHttpService>();
            _ = builder.Services.AddScoped<ISocialCampaignService, Services.Http.SocialCampaignHttpService>();

            // ShopConfigHttpService (Phase 2 â€” product-based, HTTP via Gateway only).
            _ = builder.Services.AddScoped<Services.Http.ShopConfigHttpService>();

            // TenantProfileHttpService (2026-07-21): /store/{slug} page data loader.
            _ = builder.Services.AddScoped<Services.Http.TenantProfileHttpService>();

            // Register Cart Services
            _ = builder.Services.AddScoped<Services.CartService>();
            _ = builder.Services.AddScoped<Services.CheckoutFlowState>();

            // Register PWA Services
            _ = builder.Services.AddScoped<Services.PWA.PWAService>();

            // Register HttpClient for Gateway communication
            // WASM: use AddHttpClient (supports IHttpClientFactory pattern matching Server code)
            var gatewayBaseUrl = builder.Configuration["Gateway:BaseUrl"]
                ?? builder.HostEnvironment.BaseAddress;
            _ = builder.Services.AddHttpClient("gateway", client =>
            {
                client.BaseAddress = new Uri(gatewayBaseUrl);
            });

            // Register Product Catalog Service
            _ = builder.Services.AddScoped<Services.Http.ProductHttpService>();

            // Phase 6: Catalog recommended API client
            _ = builder.Services.AddScoped<Services.Http.CatalogHttpService>();

            // KhachLink Full Flow W0: Shop feature toggle settings
            _ = builder.Services.AddScoped<Services.Http.ShopFeatureSettingsHttpService>();

            // #100: KhachLink home page section toggles — GLOBAL (not tenant-scoped)
            _ = builder.Services.AddScoped<Services.Http.KhachLinkHomeSettingsHttpService>();

            // Tiered Auth Phase 3: Social auth + identity upgrade HTTP service
            _ = builder.Services.AddScoped<Services.Http.SocialAuthHttpService>();

            // CC-S1-T1/T2 (Sprint 1): Community Commerce HTTP service (nearby orders + accept)
            _ = builder.Services.AddScoped<Services.Http.CommunityHttpService>();
            _ = builder.Services.AddScoped<Services.Http.ChatHttpService>();
            _ = builder.Services.AddScoped<Services.Http.WalletHttpService>();
            // Loyalty Alliance Phase 5B: cross-tenant alliance wallet (points) HTTP client
            _ = builder.Services.AddScoped<Services.Http.AllianceWalletHttpService>();
            // Loyalty mode resolver — KhachLink hides "Ví liên minh" UI when mode=Silo
            _ = builder.Services.AddScoped<Services.Http.LoyaltyModeHttpService>();
            // CC-S6-T5 — Collaborator SMS OTP verification
            _ = builder.Services.AddScoped<Services.Http.CollaboratorVerificationHttpService>();
            // #126 R2 Sprint 4: Guard QR Claim + Wallet API client
            _ = builder.Services.AddScoped<Services.Http.GuardQrApiClient>();
            _ = builder.Services.AddScoped<Services.LocationTrackingService>();

            // Register Recently Viewed Service + LastInteractionService
            _ = builder.Services.AddScoped<Services.RecentlyViewedService>();
            _ = builder.Services.AddScoped<Services.LastInteractionService>();

            // Add Logging
            _ = builder.Services.AddLogging();

            await builder.Build().RunAsync();
        }
    }
}
