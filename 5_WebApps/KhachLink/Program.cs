using VanAn.Shared.Services;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Infrastructure;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Services;
using VanAn.UI.Platform.Adapters;
using Microsoft.EntityFrameworkCore;
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

            // 🛡️ PHASE 5: SQLite with WAL Mode for Edge Node
            string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "vanan_khachlink.db")}";
            _ = builder.Services.AddDbContext<VanAnDbContext>(options =>
                options.UseSqlite(connectionString));
            // Register IVanAnDbContext interface for repositories that depend on the abstraction
            _ = builder.Services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());

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

            // Register PWA Services
            _ = builder.Services.AddScoped<Services.PWA.PWAService>();
            _ = builder.Services.AddHttpClient(); // For PWA API calls

            // Register Dashboard Services
            _ = builder.Services.AddScoped<Services.Dashboard.RealTimeDashboardService>();
            _ = builder.Services.AddSignalR(); // SignalR for real-time updates

            // Add Memory Cache for ShopConfigService
            _ = builder.Services.AddMemoryCache();

            WebApplication app = builder.Build();

            // Architect's Directive: Ensure SQLite schema exists for local testing
            using (IServiceScope scope = app.Services.CreateScope())
            {
                VanAnDbContext dbContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
                _ = await dbContext.Database.EnsureCreatedAsync();
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                _ = app.UseExceptionHandler("/Error");
                _ = app.UseHsts();
            }

            // Local-First: DISABLE HTTPS REDIRECTION for development
            // app.UseHttpsRedirection();

            // ALLOW IFRAME EMBEDDING for local development
            _ = app.Use(async (context, next) =>
            {
                context.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'self' http://localhost:5001 http://localhost:5003;");
                context.Response.Headers.Append("X-Frame-Options", "ALLOWALL");
                await next();
            });

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
