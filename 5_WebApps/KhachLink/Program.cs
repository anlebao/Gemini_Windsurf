using VanAn.Shared.Services;

using VanAn.CoreHub.Services;

using VanAn.UI.Platform.Core.Interfaces;

using VanAn.UI.Platform.Services;

using VanAn.UI.Platform.Adapters;

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

            _ = builder.Services.AddServerSideBlazor();

            _ = builder.Services.AddLogging();



            // UI Platform Services

            _ = builder.Services.AddScoped<ICssAdapter, BootstrapAdapter>();

            _ = builder.Services.AddScoped<IThemeProvider, ThemeProvider>();

            _ = builder.Services.AddScoped<ITenantService, TenantService>();



            // Register CoreHub Services

            _ = builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();

            _ = builder.Services.AddScoped<IShopConfigService, ShopConfigService>();

            _ = builder.Services.AddScoped<ISocialCampaignService, SocialCampaignService>();

            _ = builder.Services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();

            _ = builder.Services.AddScoped<IOnboardingService, OnboardingService>();

            _ = builder.Services.AddScoped<IVoiceCommandService, VoiceCommandService>();

            _ = builder.Services.AddScoped<ICustomerService, CustomerService>();



            // Register Repositories

            _ = builder.Services.AddScoped<CoreHub.Domain.Repositories.ICustomerRepository, CoreHub.Infrastructure.Repositories.CustomerRepository>();

            _ = builder.Services.AddScoped<CoreHub.Repositories.IOrderRepository, CoreHub.Repositories.OrderRepository>();

            _ = builder.Services.AddScoped<CoreHub.Repositories.ISocialCampaignRepository, CoreHub.Infrastructure.Repositories.SocialCampaignRepository>();

            _ = builder.Services.AddScoped<CoreHub.Repositories.ILoyaltyRewardsRepository, CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();

            _ = builder.Services.AddScoped<CoreHub.Repositories.ISystemMetricsRepository, CoreHub.Infrastructure.Repositories.SystemMetricsRepository>();



            // Register Dashboard Service

            _ = builder.Services.AddScoped<IDashboardService, DashboardService>();



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



            // Register Dashboard Services

            _ = builder.Services.AddScoped<Services.Dashboard.RealTimeDashboardService>();

            _ = builder.Services.AddSignalR(); // SignalR for real-time updates



            // Add Memory Cache for ShopConfigService

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



            _ = app.UseStaticFiles();

            _ = app.UseRouting();

            _ = app.UseAuthorization();

            _ = app.MapRazorPages();

            _ = app.MapBlazorHub();



            // Map SignalR Hub

            _ = app.MapHub<Hubs.DashboardHub>("/dashboardHub");



            // PROPER RAZOR PAGES ROUTING - ANTI-CHEATING RULE #2

            _ = app.UseDefaultFiles();

            _ = app.MapRazorPages();

            _ = app.MapFallbackToPage("/Index"); // Proper fallback to Razor Page, not static HTML



            string urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5002";

            await app.RunAsync(urls);

        }

    }



    public partial class Program { }

}

