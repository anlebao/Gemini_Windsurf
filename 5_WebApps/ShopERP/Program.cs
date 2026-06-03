using VanAn.Shared.Domain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using VanAn.CoreHub.Infrastructure;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using VanAn.UI.Platform.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Tests")]

namespace VanAn.ShopERP
{
    public partial class Program
    {
        public static async Task Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Architect: Dynamic file logging configuration
            builder.Host.UseSerilog((context, config) =>
            {
                config.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);

                // Architect: Only enable Disk I/O logging if explicitly turned on in appsettings
                if (context.Configuration.GetValue<bool>("LoggingConfig:EnableFileLogging"))
                {
                    string? appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                    config.WriteTo.File(
                        path: Path.Combine(AppContext.BaseDirectory, "Logs", $"{appName}-.txt"),
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
            string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "vanan_shoperp.db")}";
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
            builder.Services.AddScoped<CoreHub.Services.IShopConfigService, CoreHub.Services.ShopConfigService>();
            builder.Services.AddScoped<CoreHub.Services.ISocialCampaignService, CoreHub.Services.SocialCampaignService>();
            builder.Services.AddScoped<CoreHub.Services.ILoyaltyRewardsService, CoreHub.Services.LoyaltyRewardsService>();
            builder.Services.AddScoped<CoreHub.Services.IOnboardingService, CoreHub.Services.OnboardingService>();
            builder.Services.AddScoped<CoreHub.Services.IVoiceCommandService, CoreHub.Services.VoiceCommandService>();
            builder.Services.AddScoped<Shared.Services.ICustomerService, CoreHub.Services.CustomerService>();
            builder.Services.AddScoped<CoreHub.Services.IOrderService, CoreHub.Services.OrderService>();
            builder.Services.AddScoped<CoreHub.Services.IOrderWorkflowService, CoreHub.Services.OrderWorkflowService>();
            builder.Services.AddScoped<CoreHub.Services.IAccountingService, CoreHub.Services.AccountingEntryService>();
            builder.Services.AddScoped<Services.Accounting.AccountingUIService>();
            builder.Services.AddHttpContextAccessor();

            // ADD these new services for Unified API Integration:
            builder.Services.AddScoped<IOrderManagementService, OrderManagementService>();
            builder.Services.AddScoped<OrderManagementService>();

            // Add UI Platform services
            builder.Services.AddScoped<ITenantService, TenantService>();
            builder.Services.AddScoped<IThemeProvider, ThemeProvider>();
            builder.Services.AddScoped<UI.Platform.Core.Interfaces.ICssAdapter, UI.Platform.Adapters.BootstrapAdapter>();

            // Add SignalR client
            builder.Services.AddSignalR();

            // ✅ FIXED: Error notification service
            builder.Services.AddScoped<IErrorNotificationService, ErrorNotificationService>();

            // Register Repositories (FIX: Missing repository registration)
            builder.Services.AddScoped<CoreHub.Domain.Repositories.ICustomerRepository, CoreHub.Infrastructure.Repositories.CustomerRepository>();

            // Register Repository implementations for refactored services (using IVanAnDbContext)
            builder.Services.AddScoped<CoreHub.Repositories.IOrderRepository, CoreHub.Repositories.OrderRepository>();
            builder.Services.AddScoped<CoreHub.Repositories.IAccountingEntryRepository, CoreHub.Repositories.AccountingEntryRepository>();
            builder.Services.AddScoped<CoreHub.Repositories.IHKDBookRepository, CoreHub.Repositories.HKDBookRepository>();
            builder.Services.AddScoped<CoreHub.Repositories.ILoyaltyRewardsRepository, CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();
            builder.Services.AddScoped<CoreHub.Repositories.ISocialCampaignRepository, CoreHub.Infrastructure.Repositories.SocialCampaignRepository>();
            builder.Services.AddScoped<CoreHub.Repositories.ISystemMetricsRepository, CoreHub.Infrastructure.Repositories.SystemMetricsRepository>();

            // Register Dashboard Service
            builder.Services.AddScoped<CoreHub.Services.IDashboardService, CoreHub.Services.DashboardService>();

            // Sprint 2: Period Closing (PR#1)
            builder.Services.AddScoped<CoreHub.Services.IReversalService, CoreHub.Services.ReversalService>();
            builder.Services.AddScoped<CoreHub.Services.IPeriodClosingService, CoreHub.Services.PeriodClosingService>();

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

            builder.Services.AddAuthorizationBuilder()
                .AddPolicy("RequireAuthenticatedUser", policy =>
                    policy.RequireAuthenticatedUser())
                .AddPolicy("RequireTenantAccess", policy =>
                    policy.RequireAuthenticatedUser()
                           .RequireClaim("TenantId"))
                .AddPolicy("OwnerOnly", policy => policy.RequireRole(UserRole.Owner.ToString()))
                .AddPolicy("StoreManagement", policy => policy.RequireRole(UserRole.Owner.ToString(), UserRole.StoreKeeper.ToString()))
                .AddPolicy("GuardOnly", policy => policy.RequireRole(UserRole.Guard.ToString()))
                .AddPolicy("StaffOrAbove", policy => policy.RequireRole(UserRole.Staff.ToString(), UserRole.StoreKeeper.ToString(), UserRole.Owner.ToString()));

            // ✅ FIXED: Add cascading authentication state
            builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState>();

            // ✅ FIXED: Register AuthenticationStateProvider to bridge Razor Pages auth to Blazor
            builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, HttpContextAuthenticationStateProvider>();

            // 🛡️ Antiforgery configuration for local HTTP development
            builder.Services.AddAntiforgery(options =>
            {
                // Allow cookies over plain HTTP for local development
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            WebApplication app = builder.Build();

            // Architect's Directive: Ensure SQLite schema exists and optimized for concurrency
            using (IServiceScope scope = app.Services.CreateScope())
            {
                ShopERPDbContext context = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
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
            app.MapRazorComponents<Components.App>()
                .AddInteractiveServerRenderMode();
            app.MapFallbackToPage("/Index"); // Proper fallback to Razor Page, not static HTML

            string urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5003";
            await app.RunAsync(urls);
        }
    }
}
