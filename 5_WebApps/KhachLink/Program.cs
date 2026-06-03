using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.Shared.Extensions;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Infrastructure;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Services;
using VanAn.UI.Platform.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using System;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Integration.Tests")]

namespace VanAn.KhachLink;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddLogging();
        
        // UI Platform Services
        builder.Services.AddScoped<ICssAdapter, BootstrapAdapter>();
        builder.Services.AddScoped<IThemeProvider, ThemeProvider>();
        builder.Services.AddScoped<ITenantService, TenantService>();
        
        // 🛡️ PHASE 5: SQLite with WAL Mode for Edge Node
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
            ?? $"Data Source={System.IO.Path.Combine(AppContext.BaseDirectory, "vanan_khachlink.db")}";
        builder.Services.AddDbContext<VanAnDbContext>(options => 
            options.UseSqlite(connectionString));
        // Register IVanAnDbContext interface for repositories that depend on the abstraction
        builder.Services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        
        // Register CoreHub Services
        builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
        builder.Services.AddScoped<IShopConfigService, ShopConfigService>();
        builder.Services.AddScoped<ISocialCampaignService, SocialCampaignService>();
        builder.Services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();
        builder.Services.AddScoped<IOnboardingService, OnboardingService>();
        builder.Services.AddScoped<IVoiceCommandService, VoiceCommandService>();
        builder.Services.AddScoped<ICustomerService, CustomerService>();
        
        // Register Repositories
        builder.Services.AddScoped<VanAn.CoreHub.Domain.Repositories.ICustomerRepository, VanAn.CoreHub.Infrastructure.Repositories.CustomerRepository>();
        builder.Services.AddScoped<VanAn.CoreHub.Repositories.IOrderRepository, VanAn.CoreHub.Repositories.OrderRepository>();
        builder.Services.AddScoped<VanAn.CoreHub.Repositories.ISocialCampaignRepository, VanAn.CoreHub.Infrastructure.Repositories.SocialCampaignRepository>();
        builder.Services.AddScoped<VanAn.CoreHub.Repositories.ILoyaltyRewardsRepository, VanAn.CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();
        builder.Services.AddScoped<VanAn.CoreHub.Repositories.ISystemMetricsRepository, VanAn.CoreHub.Infrastructure.Repositories.SystemMetricsRepository>();
        
        // Register Dashboard Service
        builder.Services.AddScoped<VanAn.CoreHub.Services.IDashboardService, VanAn.CoreHub.Services.DashboardService>();
        
        // Register Cart Services
        builder.Services.AddScoped<VanAn.KhachLink.Services.CartService>();
        
        // Register PWA Services
        builder.Services.AddScoped<VanAn.KhachLink.Services.PWA.PWAService>();
        builder.Services.AddHttpClient(); // For PWA API calls
        
        // Register Dashboard Services
        builder.Services.AddScoped<VanAn.KhachLink.Services.Dashboard.RealTimeDashboardService>();
        builder.Services.AddSignalR(); // SignalR for real-time updates
        
        // Add Memory Cache for ShopConfigService
        builder.Services.AddMemoryCache();
        
        var app = builder.Build();

        // Architect's Directive: Ensure SQLite schema exists for local testing
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VanAn.CoreHub.Infrastructure.VanAnDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        // Local-First: DISABLE HTTPS REDIRECTION for development
        // app.UseHttpsRedirection();
        
        // ALLOW IFRAME EMBEDDING for local development
        app.Use(async (context, next) => {
            context.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'self' http://localhost:5001 http://localhost:5003;");
            context.Response.Headers.Append("X-Frame-Options", "ALLOWALL");
            await next();
        });
        
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();
        app.MapBlazorHub();
        
        // Map SignalR Hub
        app.MapHub<VanAn.KhachLink.Hubs.DashboardHub>("/dashboardHub");
        
        // PROPER RAZOR PAGES ROUTING - ANTI-CHEATING RULE #2
        app.UseDefaultFiles();
        app.MapRazorPages();
        app.MapFallbackToPage("/Index"); // Proper fallback to Razor Page, not static HTML

        var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5002";
        await app.RunAsync(urls);
    }
}

public partial class Program { }
