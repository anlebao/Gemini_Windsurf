using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.Shared.Extensions;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Infrastructure;
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

namespace VanAn.KhachLink;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        
        // 🛡️ PHASE 5: SQLite with WAL Mode for Edge Node
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
            ?? $"Data Source={System.IO.Path.Combine(AppContext.BaseDirectory, "vanan_khachlink.db")}";
        builder.Services.AddDbContext<VanAnDbContext>(options => 
            options.UseSqlite(connectionString));
        
        // Register CoreHub Services
        builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
        builder.Services.AddScoped<IShopConfigService, ShopConfigService>();
        builder.Services.AddScoped<ISocialCampaignService, SocialCampaignService>();
        builder.Services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();
        builder.Services.AddScoped<IOnboardingService, OnboardingService>();
        builder.Services.AddScoped<IVoiceCommandService, VoiceCommandService>();
        builder.Services.AddScoped<ICustomerService, CustomerService>();
        
        // Register Dashboard Service
        builder.Services.AddScoped<VanAn.CoreHub.Services.IDashboardService, VanAn.CoreHub.Services.DashboardService>();
        
        // Register Cart Services
        builder.Services.AddScoped<VanAn.KhachLink.Services.CartService>();
        
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
        
        // PROPER RAZOR PAGES ROUTING - ANTI-CHEATING RULE #2
        app.UseDefaultFiles();
        app.MapRazorPages();
        app.MapFallbackToPage("/Index"); // Proper fallback to Razor Page, not static HTML

        var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5002";
        await app.RunAsync(urls);
    }
}

public partial class Program { }
