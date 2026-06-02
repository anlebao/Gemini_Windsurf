using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VanAn.Shared.Extensions;
using VanAn.CoreHub.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using VanAn.CoreHub.Infrastructure;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using VanAn.UI.Platform.Services;
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
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        
        // Add SignalR timeout configuration to prevent circuit disconnect
        builder.Services.AddServerSideBlazor()
            .AddHubOptions(options =>
            {
                options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            });
        
        // PHASE 5: SQLite with WAL Mode for Edge Node - Enhanced for concurrency
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
            ?? $"Data Source={System.IO.Path.Combine(AppContext.BaseDirectory, "vanan_shoperp.db")}";
        builder.Services.AddDbContext<ShopERPDbContext>(options => 
            options.UseSqlite(connectionString));
        
        // Register IVanAnDbContext with ShopERPDbContext for Offline-First architecture
        // This decouples services from VanAnDbContext (PostgreSQL) and allows SQLite usage
        builder.Services.AddScoped<IVanAnDbContext>(provider => provider.GetRequiredService<ShopERPDbContext>());

        // REMOVED: Queue and Outbox services for SQLite concurrency
        // builder.Services.AddSingleton<IOrderQueueService, OrderQueueService>();
        // builder.Services.AddHostedService<SimpleOutboxProcessor>();
        // builder.Services.AddHostedService(provider => (IHostedService)provider.GetRequiredService<IOrderQueueService>());

        // REMOVED: Enhanced OrderWorkflowService with queue integration
        // builder.Services.AddScoped<VanAn.ShopERP.Services.IOrderWorkflowService, VanAn.ShopERP.Services.OrderWorkflowService>();

        // Register CoreHub Services (FIX: Use CoreHub interfaces and implementations)
        builder.Services.AddScoped<VanAn.CoreHub.Services.IShopConfigService, VanAn.CoreHub.Services.ShopConfigService>();
        builder.Services.AddScoped<VanAn.CoreHub.Services.ISocialCampaignService, VanAn.CoreHub.Services.SocialCampaignService>();
        builder.Services.AddScoped<VanAn.CoreHub.Services.ILoyaltyRewardsService, VanAn.CoreHub.Services.LoyaltyRewardsService>();
        builder.Services.AddScoped<VanAn.CoreHub.Services.IOnboardingService, VanAn.CoreHub.Services.OnboardingService>();
        builder.Services.AddScoped<VanAn.CoreHub.Services.IVoiceCommandService, VanAn.CoreHub.Services.VoiceCommandService>();
        builder.Services.AddScoped<VanAn.Shared.Services.ICustomerService, VanAn.CoreHub.Services.CustomerService>();
        builder.Services.AddScoped<VanAn.CoreHub.Services.IOrderService, VanAn.CoreHub.Services.OrderService>();
        builder.Services.AddScoped<VanAn.CoreHub.Services.IOrderWorkflowService, VanAn.CoreHub.Services.OrderWorkflowService>();
        builder.Services.AddScoped<VanAn.CoreHub.Services.IAccountingService, VanAn.CoreHub.Services.AccountingEntryService>();
        builder.Services.AddScoped<VanAn.ShopERP.Services.Accounting.AccountingUIService>();
        builder.Services.AddHttpContextAccessor();

        // ADD these new services for Unified API Integration:
        builder.Services.AddScoped<IOrderManagementService, OrderManagementService>();
        builder.Services.AddScoped<OrderManagementService>();
        
        // Add UI Platform services
        builder.Services.AddScoped<ITenantService, TenantService>();
        builder.Services.AddScoped<IThemeProvider, ThemeProvider>();
        builder.Services.AddScoped<VanAn.UI.Platform.Core.Interfaces.ICssAdapter, VanAn.UI.Platform.Adapters.BootstrapAdapter>();

        // Add SignalR client
        builder.Services.AddSignalR();

        // ✅ FIXED: Error notification service
        builder.Services.AddScoped<IErrorNotificationService, ErrorNotificationService>();
        
        // Register Repositories (FIX: Missing repository registration)
        builder.Services.AddScoped<VanAn.CoreHub.Domain.Repositories.ICustomerRepository, VanAn.CoreHub.Infrastructure.Repositories.CustomerRepository>();
        
        // Register Repository implementations for refactored services (using IVanAnDbContext)
        builder.Services.AddScoped<VanAn.CoreHub.Repositories.IOrderRepository, VanAn.CoreHub.Repositories.OrderRepository>();
        builder.Services.AddScoped<VanAn.CoreHub.Repositories.IAccountingEntryRepository, VanAn.CoreHub.Repositories.AccountingEntryRepository>();
        builder.Services.AddScoped<VanAn.CoreHub.Repositories.IHKDBookRepository, VanAn.CoreHub.Repositories.HKDBookRepository>();
        builder.Services.AddScoped<VanAn.CoreHub.Repositories.ILoyaltyRewardsRepository, VanAn.CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();
        builder.Services.AddScoped<VanAn.CoreHub.Repositories.ISocialCampaignRepository, VanAn.CoreHub.Infrastructure.Repositories.SocialCampaignRepository>();
        builder.Services.AddScoped<VanAn.CoreHub.Repositories.ISystemMetricsRepository, VanAn.CoreHub.Infrastructure.Repositories.SystemMetricsRepository>();
        
        // Register Dashboard Service
        builder.Services.AddScoped<VanAn.CoreHub.Services.IDashboardService, VanAn.CoreHub.Services.DashboardService>();

        // Sprint 2: Period Closing (PR#1)
        builder.Services.AddScoped<VanAn.CoreHub.Services.IReversalService, VanAn.CoreHub.Services.ReversalService>();
        builder.Services.AddScoped<VanAn.CoreHub.Services.IPeriodClosingService, VanAn.CoreHub.Services.PeriodClosingService>();
        
        // Add Memory Cache for ShopConfigService
        builder.Services.AddMemoryCache();
        
        // ✅ FIXED: Enterprise authentication configuration
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.Name = ".VanAn.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.LoginPath = "/Login";
        })
        .AddOpenIdConnect("OpenIdConnect", options =>
        {
            options.Authority = builder.Configuration["Authentication:Authority"] ?? "https://localhost:5001";
            options.ClientId = builder.Configuration["Authentication:ClientId"] ?? "VanAn.ShopERP";
            options.ClientSecret = builder.Configuration["Authentication:ClientSecret"] ?? "your-secret-here";
            options.ResponseType = "code";
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.Scope.Add("roles");
            options.Scope.Add("tenant_id");
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                NameClaimType = "name",
                RoleClaimType = "role"
            };
        });
            
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAuthenticatedUser", policy => 
                policy.RequireAuthenticatedUser());
            
            options.AddPolicy("RequireTenantAccess", policy => 
                policy.RequireAuthenticatedUser()
                       .RequireClaim("TenantId"));
            
            options.AddPolicy("OwnerOnly", policy => policy.RequireRole(UserRole.Owner.ToString()));
            options.AddPolicy("StoreManagement", policy => policy.RequireRole(UserRole.Owner.ToString(), UserRole.StoreKeeper.ToString()));
            options.AddPolicy("GuardOnly", policy => policy.RequireRole(UserRole.Guard.ToString()));
            options.AddPolicy("StaffOrAbove", policy => policy.RequireRole(UserRole.Staff.ToString(), UserRole.StoreKeeper.ToString(), UserRole.Owner.ToString()));
        });

        // ✅ FIXED: Add cascading authentication state
        builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState>();
        
        // ✅ FIXED: Register AuthenticationStateProvider to bridge Razor Pages auth to Blazor
        builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, VanAn.ShopERP.Services.HttpContextAuthenticationStateProvider>();
        
        // 🛡️ Antiforgery configuration for local HTTP development
        builder.Services.AddAntiforgery(options => 
        {
            // Allow cookies over plain HTTP for local development
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });
        
        var app = builder.Build();

        // Architect's Directive: Ensure SQLite schema exists and optimized for concurrency
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            await context.Database.EnsureCreatedAsync();
            
            // Optimize SQLite for concurrency
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA cache_size=10000;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
            
            Console.WriteLine("SQLite database optimized for concurrency");
        }

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        // Local-First: DISABLE HTTPS REDIRECTION for development
        // app.UseHttpsRedirection();

        // MIDDLEWARE ORDER COMPLIANCE - RULE #2: StaticFiles -> Routing -> Auth -> Antiforgery -> MapRazorPages
        app.UseStaticFiles(); // MUST be first to serve wwwroot files
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        // PROPER RAZOR PAGES ROUTING - ANTI-CHEATING RULE #2
        app.MapControllers(); // If you have API controllers in ShopERP
        app.MapRazorPages();
        app.MapRazorComponents<VanAn.ShopERP.Components.App>()
            .AddInteractiveServerRenderMode();
        app.MapFallbackToPage("/Index"); // Proper fallback to Razor Page, not static HTML

        var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5003";
        await app.RunAsync(urls);
    }
}
