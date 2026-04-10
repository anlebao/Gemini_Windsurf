using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.Shared.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using VanAn.CoreHub.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Serilog;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Tests")]

namespace VanAn.ShopERP;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Architect: Dynamic file logging configuration
        builder.Host.UseSerilog((context, config) => 
        {
            config.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);
            
            // Architect: Only enable Disk I/O logging if explicitly turned on in appsettings
            if (context.Configuration.GetValue<bool>("LoggingConfig:EnableFileLogging"))
            {
                var appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                config.WriteTo.File(
                    path: System.IO.Path.Combine(AppContext.BaseDirectory, "Logs", $"{appName}-.txt"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 2,
                    formatProvider: System.Globalization.CultureInfo.InvariantCulture
                );
            }
        });

        // Add services to the container.
        builder.Services.AddRazorPages();
        
        // 🛡️ PHASE 5: SQLite with WAL Mode for Edge Node
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
            ?? $"Data Source={System.IO.Path.Combine(AppContext.BaseDirectory, "vanan_shoperp.db")}";
        builder.Services.AddDbContext<VanAnDbContext>(options => 
            options.UseSqlite(connectionString));
        
        // PHÂN QUYỀN - Authentication & Authorization
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });
            
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("OwnerOnly", policy => policy.RequireRole(UserRole.Owner.ToString()));
            options.AddPolicy("StoreManagement", policy => policy.RequireRole(UserRole.Owner.ToString(), UserRole.StoreKeeper.ToString()));
            options.AddPolicy("GuardOnly", policy => policy.RequireRole(UserRole.Guard.ToString()));
            options.AddPolicy("StaffOrAbove", policy => policy.RequireRole(UserRole.Staff.ToString(), UserRole.StoreKeeper.ToString(), UserRole.Owner.ToString()));
        });
        
        // 🛡️ Antiforgery configuration for local HTTP development
        builder.Services.AddAntiforgery(options => 
        {
            // Allow cookies over plain HTTP for local development
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });
        
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

        // MIDDLEWARE ORDER COMPLIANCE - RULE #2: StaticFiles -> Routing -> Auth -> MapRazorPages
        app.UseStaticFiles(); // MUST be first to serve wwwroot files
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        // PROPER RAZOR PAGES ROUTING - ANTI-CHEATING RULE #2
        app.MapControllers(); // If you have API controllers in ShopERP
        app.MapRazorPages();
        app.MapFallbackToPage("/Index"); // Proper fallback to Razor Page, not static HTML

        var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5003";
        await app.RunAsync(urls);
    }
}
