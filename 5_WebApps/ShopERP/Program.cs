using VanAn.Shared.Domain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using VanAn.UI.Platform.Services;
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
            _ = builder.Host.UseSerilog((context, config) =>
            {
                _ = config.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);

                // Architect: Only enable Disk I/O logging if explicitly turned on in appsettings
                if (context.Configuration.GetValue<bool>("LoggingConfig:EnableFileLogging"))
                {
                    string? appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                    _ = config.WriteTo.File(
                        path: Path.Combine(AppContext.BaseDirectory, "Logs", $"{appName}-.txt"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 2,
                        formatProvider: System.Globalization.CultureInfo.InvariantCulture
                    );
                }
            });

            // Add services to the container.
            _ = builder.Services.AddRazorPages();
            _ = builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // Add SignalR timeout configuration to prevent circuit disconnect
            _ = builder.Services.AddServerSideBlazor()
                .AddHubOptions(options =>
                {
                    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
                });

            // PHASE 5: SQLite with WAL Mode for Edge Node - Enhanced for concurrency
            string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "vanan_shoperp.db")}";
            _ = builder.Services.AddDbContext<ShopERPDbContext>(options =>
                options.UseSqlite(connectionString));

            // Wave 2: Data Protection for PII field-level encryption
            string keyDirectory = builder.Configuration.GetSection("DataProtection")["KeyDirectory"]
                ?? Path.Combine(AppContext.BaseDirectory, "keys", "shoperp");
            _ = Directory.CreateDirectory(keyDirectory);
            _ = builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
                .SetApplicationName(builder.Configuration.GetSection("DataProtection")["ApplicationName"] ?? "VanAnShopERP");

            // Wave 2: PII data migration service
            _ = builder.Services.AddScoped<CoreHub.Services.DataProtection.PiiDataMigrationService>();

            // Register IVanAnDbContext with ShopERPDbContext for Offline-First architecture
            // This decouples services from VanAnDbContext (PostgreSQL) and allows SQLite usage
            _ = builder.Services.AddScoped<IVanAnDbContext>(provider => provider.GetRequiredService<ShopERPDbContext>());

            // REMOVED: Queue and Outbox services for SQLite concurrency
            // builder.Services.AddSingleton<IOrderQueueService, OrderQueueService>();
            // builder.Services.AddHostedService<SimpleOutboxProcessor>();
            // builder.Services.AddHostedService(provider => (IHostedService)provider.GetRequiredService<IOrderQueueService>());

            // REMOVED: Enhanced OrderWorkflowService with queue integration
            // builder.Services.AddScoped<VanAn.ShopERP.Services.IOrderWorkflowService, VanAn.ShopERP.Services.OrderWorkflowService>();

            // Register CoreHub Services (FIX: Use CoreHub interfaces and implementations)
            _ = builder.Services.AddScoped<CoreHub.Services.IShopConfigService, CoreHub.Services.ShopConfigService>();
            _ = builder.Services.AddScoped<CoreHub.Services.ISocialCampaignService, CoreHub.Services.SocialCampaignService>();
            _ = builder.Services.AddScoped<CoreHub.Services.ILoyaltyRewardsService, CoreHub.Services.LoyaltyRewardsService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IOnboardingService, CoreHub.Services.OnboardingService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IVoiceCommandService, CoreHub.Services.VoiceCommandService>();
            _ = builder.Services.AddScoped<Shared.Services.ICustomerService, CoreHub.Services.CustomerService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IOrderService, CoreHub.Services.OrderService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IOrderWorkflowService, CoreHub.Services.OrderWorkflowService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IAccountingService, CoreHub.Services.AccountingEntryService>();
            _ = builder.Services.AddScoped<Services.Accounting.AccountingUIService>();
            _ = builder.Services.AddHttpContextAccessor();

            // ADD these new services for Unified API Integration:
            _ = builder.Services.AddScoped<IOrderManagementService, OrderManagementService>();
            _ = builder.Services.AddScoped<OrderManagementService>();

            // Add UI Platform services
            _ = builder.Services.AddScoped<ITenantService, TenantService>();
            _ = builder.Services.AddScoped<IThemeProvider, ThemeProvider>();
            _ = builder.Services.AddScoped<UI.Platform.Core.Interfaces.ICssAdapter, UI.Platform.Adapters.BootstrapAdapter>();

            // Add SignalR client
            _ = builder.Services.AddSignalR();

            // ✅ FIXED: Error notification service
            _ = builder.Services.AddScoped<IErrorNotificationService, ErrorNotificationService>();

            // Register Repositories (FIX: Missing repository registration)
            _ = builder.Services.AddScoped<CoreHub.Domain.Repositories.ICustomerRepository, CoreHub.Infrastructure.Repositories.CustomerRepository>();

            // Register Repository implementations for refactored services (using IVanAnDbContext)
            _ = builder.Services.AddScoped<CoreHub.Repositories.IOrderRepository, CoreHub.Repositories.OrderRepository>();
            _ = builder.Services.AddScoped<CoreHub.Repositories.IAccountingEntryRepository, CoreHub.Repositories.AccountingEntryRepository>();
            _ = builder.Services.AddScoped<CoreHub.Repositories.IHKDBookRepository, CoreHub.Repositories.HKDBookRepository>();
            _ = builder.Services.AddScoped<CoreHub.Repositories.ILoyaltyRewardsRepository, CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();
            _ = builder.Services.AddScoped<CoreHub.Repositories.ISocialCampaignRepository, CoreHub.Infrastructure.Repositories.SocialCampaignRepository>();
            _ = builder.Services.AddScoped<CoreHub.Repositories.ISystemMetricsRepository, CoreHub.Infrastructure.Repositories.SystemMetricsRepository>();

            // Register Dashboard Service
            _ = builder.Services.AddScoped<CoreHub.Services.IDashboardService, CoreHub.Services.DashboardService>();

            // Audit Trail dependencies (required by AccountingEntryService)
            _ = builder.Services.AddScoped<VanAn.Shared.Domain.Common.ITenantProvider, Services.HttpContextTenantProvider>();
            _ = builder.Services.AddScoped<CoreHub.Domain.Repositories.IAuditLogRepository, CoreHub.Infrastructure.Repositories.AuditLogRepository>();
            _ = builder.Services.AddScoped<CoreHub.Services.IAuditTrailService, CoreHub.Services.AuditTrailService>();

            // Sprint 2: Period Closing (PR#1)
            _ = builder.Services.AddScoped<CoreHub.Services.IReversalService, CoreHub.Services.ReversalService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IPeriodClosingService, CoreHub.Services.PeriodClosingService>();

            // Add Memory Cache for ShopConfigService
            _ = builder.Services.AddMemoryCache();

            // Wave 0: JWT Authentication Foundation
            _ = builder.Services.AddScoped<CoreHub.Services.IJwtTokenService, CoreHub.Services.JwtTokenService>();

            // Wave 1: Notification Integration (Brevo Email + ESMS SMS)
            _ = builder.Services.AddHttpClient<CoreHub.Services.IEmailService, CoreHub.Services.BrevoEmailService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            _ = builder.Services.AddHttpClient<CoreHub.Services.ISmsService, CoreHub.Services.EsmsNotificationService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            _ = builder.Services.AddScoped<CoreHub.Services.INotificationService, CoreHub.Services.CompositeNotificationService>();

            // ✅ FIXED: Enterprise authentication configuration
            _ = builder.Services.AddAuthentication(options =>
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

            _ = builder.Services.AddAuthorizationBuilder()
                .AddPolicy("RequireAuthenticatedUser", policy =>
                    policy.RequireAuthenticatedUser())
                .AddPolicy("RequireTenantAccess", policy =>
                    policy.RequireAuthenticatedUser()
                           .RequireClaim("tenant_id"))
                .AddPolicy("OwnerOnly", policy => policy.RequireRole(UserRole.Owner.ToString()))
                .AddPolicy("StoreManagement", policy => policy.RequireRole(UserRole.Owner.ToString(), UserRole.StoreKeeper.ToString()))
                .AddPolicy("GuardOnly", policy => policy.RequireRole(UserRole.Guard.ToString()))
                .AddPolicy("StaffOrAbove", policy => policy.RequireRole(UserRole.Staff.ToString(), UserRole.StoreKeeper.ToString(), UserRole.Owner.ToString()));

            // ✅ FIXED: Add cascading authentication state
            _ = builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState>();

            // ✅ FIXED: Register AuthenticationStateProvider to bridge Razor Pages auth to Blazor
            _ = builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, HttpContextAuthenticationStateProvider>();

            // 🛡️ Antiforgery configuration for local HTTP development
            _ = builder.Services.AddAntiforgery(options =>
            {
                // Allow cookies over plain HTTP for local development
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            WebApplication app = builder.Build();

            // Wave 2: Initialize DataProtection provider for EF Core PII encryption
            DataProtectionProviderAccessor.Initialize(app.Services.GetRequiredService<IDataProtectionProvider>());

            // Architect's Directive: Ensure SQLite schema exists and optimized for concurrency
            using (IServiceScope scope = app.Services.CreateScope())
            {
                ShopERPDbContext context = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
                _ = await context.Database.EnsureCreatedAsync();

                // Optimize SQLite for concurrency
                _ = await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                _ = await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;");
                _ = await context.Database.ExecuteSqlRawAsync("PRAGMA cache_size=10000;");
                _ = await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");

                Console.WriteLine("SQLite database optimized for concurrency");

                // Wave 0 [W0-T5]: Seed DemoUsers with BCrypt hashed passwords (work factor 12)
                // Only seeds if no users exist to avoid duplicate key errors
                if (!await context.Users.AnyAsync())
                {
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword("VanAn@2026", 12);

                    static DemoUser MakeDemoUser(string username, string hash, string displayName, UserRole role)
                    {
                        // Activator bypasses protected constructor visibility — needed because DemoUser()
                        // is protected (EF Core materialization pattern). Seed-only usage.
                        var u = (DemoUser)Activator.CreateInstance(typeof(DemoUser), nonPublic: true)!;
                        u.Username = username;
                        u.PasswordHash = hash;
                        u.DisplayName = displayName;
                        u.Role = role;
                        return u;
                    }

                    context.Users.AddRange(
                        MakeDemoUser("admin@vanan.vn", passwordHash, "Chủ Quán", UserRole.Owner),
                        MakeDemoUser("kho@vanan.vn", passwordHash, "Thủ Kho", UserRole.StoreKeeper),
                        MakeDemoUser("baove@vanan.vn", passwordHash, "Bảo Vệ", UserRole.Guard),
                        MakeDemoUser("staff@vanan.vn", passwordHash, "Phục Vụ", UserRole.Staff),
                        MakeDemoUser("bep@vanan.vn", passwordHash, "Bếp Trưởng", UserRole.Masterchef)
                    );
                    _ = await context.SaveChangesAsync();
                    Console.WriteLine("Wave 0: DemoUsers seeded with BCrypt hashed passwords.");
                }

                // Wave 2: Encrypt any pre-existing plaintext PII in dev DB
                if (app.Environment.IsDevelopment())
                {
                    var migrationService = scope.ServiceProvider.GetRequiredService<CoreHub.Services.DataProtection.PiiDataMigrationService>();
                    await migrationService.MigrateAsync();
                }
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                _ = app.UseExceptionHandler("/Error");
                _ = app.UseHsts();
            }

            // Local-First: DISABLE HTTPS REDIRECTION for development
            // app.UseHttpsRedirection();

            // MIDDLEWARE ORDER COMPLIANCE - RULE #2: StaticFiles -> Routing -> Auth -> Antiforgery -> MapRazorPages
            _ = app.UseStaticFiles(); // MUST be first to serve wwwroot files
            _ = app.UseRouting();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            _ = app.UseAntiforgery();

            // PROPER RAZOR PAGES ROUTING - ANTI-CHEATING RULE #2
            _ = app.MapControllers(); // If you have API controllers in ShopERP

            // T-20: Dev-only login endpoint for Playwright E2E tests.
            // /dev/login is ONLY reachable in Development environment.
            // In Production/Staging this branch is never entered — the route is not registered.
            if (app.Environment.IsDevelopment())
            {
                _ = app.MapGet("/dev/login", () => Results.Ok(new
                {
                    available = true,
                    env       = "Development",
                    note      = "POST to /dev/login to create an auth session for E2E tests",
                }));
                app.Logger.LogInformation("DevLoginController registered at /dev/login (Development only)");
            }
            _ = app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "VanAn ShopERP", Timestamp = DateTime.UtcNow }));
            _ = app.MapRazorPages();
            _ = app.MapRazorComponents<Components.App>()
                .AddInteractiveServerRenderMode();
            _ = app.MapFallbackToPage("/Index"); // Proper fallback to Razor Page, not static HTML

            string urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5003";
            await app.RunAsync(urls);
        }
    }
}
