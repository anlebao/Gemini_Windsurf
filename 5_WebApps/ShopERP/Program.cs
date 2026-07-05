using VanAn.Shared.Domain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using VanAn.UI.Platform.Services;
using Serilog;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
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
            // SaaS W1: Validate Production config — fail fast if __REPLACE_* sentinels remain
            if (builder.Environment.IsProduction())
            {
                ValidateProductionConfig(builder.Configuration);
            }

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
            // ADR-001 Edge: Allow SQLITE_DB_PATH env var override for Docker volume mounting
            string connectionString =
                Environment.GetEnvironmentVariable("SQLITE_DB_PATH")
                ?? builder.Configuration.GetConnectionString("DefaultConnection")
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

            // Wave 7: Health checks with database validation
            _ = builder.Services.AddHealthChecks()
                .AddDbContextCheck<ShopERPDbContext>("shoperp_database");

            // Register IVanAnDbContext with ShopERPDbContext for Offline-First architecture
            // This decouples services from VanAnDbContext (PostgreSQL) and allows SQLite usage
            _ = builder.Services.AddScoped<IVanAnDbContext>(provider => provider.GetRequiredService<ShopERPDbContext>());

            // Register NATS publisher as Singleton (holds NATS connection)
            // Required by OrderWorkflowService even without --sync-worker mode
            builder.Services.AddSingleton<INatsEventPublisher, NatsEventPublisher>();

            // ADR-001 Edge: Conditional NATS sync worker (activated via --sync-worker arg)
            if (args.Contains("--sync-worker"))
            {
                // Register Outbox for NATS sync (uses same SQLite ShopERPDbContext)
                builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();

                // Register NatsSyncWorker as BackgroundService
                builder.Services.AddHostedService<NatsSyncWorker>();

                Log.Information("NatsSyncWorker registered — running in edge sync mode");
            }

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

            // Add Memory Cache for ShopConfigService + W17-T1 OTP
            _ = builder.Services.AddMemoryCache();

            // Wave 8: HKD Book generation engine — required by /accounting/hkd-books UI page.
            // Dependency order: IFormulaEngine -> IPreAggregationService -> IDataProvider
            // -> IBookResultCache -> TemplateFactory (concrete) -> IHKDBookGenerationService
            // Wave 7: Lazy<IFormulaEngine> breaks circular dependency (FormulaEngine -> DataProvider
            // -> PreAggregation -> FormulaEngine). SmartPreAggregationService uses Lazy<IFormulaEngine>.
            _ = builder.Services.AddScoped<Lazy<CoreHub.Services.Formula.IFormulaEngine>>(sp => new Lazy<CoreHub.Services.Formula.IFormulaEngine>(() => sp.GetRequiredService<CoreHub.Services.Formula.IFormulaEngine>()));
            _ = builder.Services.AddScoped<CoreHub.Services.Formula.IFormulaEngine, CoreHub.Services.Formula.ProductionFormulaEngine>();
            _ = builder.Services.AddScoped<CoreHub.Services.PreAggregation.IPreAggregationService, CoreHub.Services.PreAggregation.SmartPreAggregationService>();
            _ = builder.Services.AddScoped<CoreHub.Services.Data.IDataProvider, CoreHub.Services.Data.ScopedDataProvider>();
            _ = builder.Services.AddScoped<CoreHub.Services.Cache.IBookResultCache, CoreHub.Services.Cache.BookResultCache>();
            _ = builder.Services.AddScoped<CoreHub.Services.Template.TemplateFactory>();
            _ = builder.Services.AddScoped<CoreHub.Services.Template.IHKDBookGenerationService, CoreHub.Services.Template.HKDBookGenerationService>();

            // Wave 8: HKD Book export service (DOCX via OpenXML + XLSX via EPPlus)
            _ = builder.Services.AddScoped<Services.IHKDBookExportService, Services.HKDBookExportService>();

            // W17-T1: Customer Identity services
            _ = builder.Services.AddScoped<VanAn.ShopERP.Services.IOtpService, VanAn.ShopERP.Services.OtpService>();
            _ = builder.Services.AddScoped<VanAn.ShopERP.Services.ICustomerTokenService, VanAn.ShopERP.Services.CustomerTokenService>();

            // Wave 7: Conditional distributed cache — Redis if configured, otherwise memory fallback
            string? redisConnection = builder.Configuration.GetConnectionString("Redis");
            if (!string.IsNullOrWhiteSpace(redisConnection))
            {
                _ = builder.Services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
            }
            else
            {
                _ = builder.Services.AddDistributedMemoryCache();
            }

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
            _ = builder.Services.AddHttpClient("GatewayClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            _ = builder.Services.AddScoped<CoreHub.Services.INotificationService, CoreHub.Services.CompositeNotificationService>();
            // Wave 5: Tenant management
            _ = builder.Services.AddScoped<CoreHub.Services.ITenantManagementService, CoreHub.Services.TenantManagementService>();
            // Wave 6: User & permission group management
            _ = builder.Services.AddScoped<CoreHub.Services.IUserManagementService, CoreHub.Services.UserManagementService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IRoleAssignmentService, CoreHub.Services.RoleAssignmentService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IPermissionGroupService, CoreHub.Services.PermissionGroupService>();
            // W3: VAS Account Chart service + HKD→DN account mapper (D9)
            _ = builder.Services.AddScoped<CoreHub.Services.IAccountChartService, CoreHub.Services.AccountChartService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IHkdToEnterpriseAccountMapper, CoreHub.Services.HkdToEnterpriseAccountMapper>();
            // W4: VAS Enterprise Financial Report services (BS + IS + CF + TB)
            _ = builder.Services.AddScoped<CoreHub.Services.IBalanceSheetService, CoreHub.Services.BalanceSheetService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IIncomeStatementService, CoreHub.Services.IncomeStatementService>();
            _ = builder.Services.AddScoped<CoreHub.Services.ICashFlowStatementService, CoreHub.Services.CashFlowStatementService>();
            _ = builder.Services.AddScoped<CoreHub.Services.ITrialBalanceService, CoreHub.Services.TrialBalanceService>();
            // W8: VAS feature flag + tenant conversion services
            _ = builder.Services.AddScoped<CoreHub.Services.IVasFeatureFlagService, CoreHub.Services.VasFeatureFlagService>();
            _ = builder.Services.AddScoped<CoreHub.Services.ITenantConversionService, CoreHub.Services.TenantConversionService>();
            // Wave 5: Gateway tenant onboarding API client (SystemAdmin JWT + HttpClient)
            _ = builder.Services.AddScoped<Services.TenantOnboardingApiClient>();
            // Wave 14: API Key management
            _ = builder.Services.AddScoped<VanAn.Shared.Repositories.IApiKeyRepository, CoreHub.Infrastructure.Repositories.ApiKeyRepository>();
            _ = builder.Services.AddScoped<CoreHub.Services.IApiKeyManagementService, CoreHub.Services.ApiKeyManagementService>();

            // ✅ FIXED: Enterprise authentication configuration
            // DefaultChallengeScheme = Cookie so [Authorize] redirects to LoginPath (/Login)
            // instead of triggering OIDC discovery (Gateway is not an identity server).
            _ = builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
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
                options.ClientSecret = builder.Configuration["Authentication:ClientSecret"]
                    ?? (builder.Environment.IsProduction()
                        ? throw new InvalidOperationException("Authentication:ClientSecret configuration is required in Production.")
                        : "your-secret-here");
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
                .AddPolicy("StaffOrAbove", policy => policy.RequireRole(UserRole.Staff.ToString(), UserRole.StoreKeeper.ToString(), UserRole.Owner.ToString()))
                // Wave 5: SystemAdmin — cross-tenant Tenant CRUD (platform-level admin)
                .AddPolicy("SystemAdmin", policy => policy.RequireRole("SystemAdmin"));

            // Wave 7: Rate limiting for login endpoint (5 requests per minute per IP)
            _ = builder.Services.AddRateLimiter(options =>
            {
                options.AddPolicy("LoginRateLimit", context =>
                {
                    string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
                });
            });

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
                // Always ensure owner user exists with Owner role (fix for config changes)
                string ownerPassword = builder.Configuration["Seed:OwnerPassword"]
                    ?? (builder.Environment.IsProduction()
                        ? throw new InvalidOperationException("Seed:OwnerPassword configuration is required in Production.")
                        : "VanAn@2026");
                string ownerUsername = builder.Configuration["Seed:OwnerUsername"] ?? "admin@vanan.vn";
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(ownerPassword, 12);

                // Production tenant: 00000000-0000-0000-0000-000000000001 (default)
                // Override via Seed:TenantId env var for multi-tenant setups
                string tenantIdStr = builder.Configuration["Seed:TenantId"] ?? "00000000-0000-0000-0000-000000000001";
                var seedTenantId = new TenantId(Guid.Parse(tenantIdStr));

                var existingOwner = await context.Users.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username == ownerUsername && u.TenantId == seedTenantId);

                if (existingOwner == null)
                {
                    // Seed all users if database is empty
                    if (!await context.Users.IgnoreQueryFilters().AnyAsync())
                    {
                        context.Users.AddRange(
                            new DemoUser(seedTenantId, ownerUsername, passwordHash, "Chủ Quán", UserRole.Owner),
                            new DemoUser(seedTenantId, "kho@vanan.vn", passwordHash, "Thủ Kho", UserRole.StoreKeeper),
                            new DemoUser(seedTenantId, "baove@vanan.vn", passwordHash, "Bảo Vệ", UserRole.Guard),
                            new DemoUser(seedTenantId, "staff@vanan.vn", passwordHash, "Phục Vụ", UserRole.Staff),
                            new DemoUser(seedTenantId, "bep@vanan.vn", passwordHash, "Bếp Trưởng", UserRole.Masterchef)
                        );
                        _ = await context.SaveChangesAsync();
                        Console.WriteLine($"Wave 0: DemoUsers seeded — owner={ownerUsername}, tenant={tenantIdStr}");
                    }
                    else
                    {
                        // Create only owner user if database has other users
                        context.Users.Add(
                            new DemoUser(seedTenantId, ownerUsername, passwordHash, "Chủ Quán", UserRole.Owner)
                        );
                        _ = await context.SaveChangesAsync();
                        Console.WriteLine($"Wave 0: Owner user created — owner={ownerUsername}, tenant={tenantIdStr}");
                    }
                }
                else if (existingOwner.Role != UserRole.Owner)
                {
                    // Fix owner role if incorrect - delete and recreate
                    context.Users.Remove(existingOwner);
                    context.Users.Add(
                        new DemoUser(seedTenantId, ownerUsername, passwordHash, "Chủ Quán", UserRole.Owner)
                    );
                    _ = await context.SaveChangesAsync();
                    Console.WriteLine($"Wave 0: Owner role fixed by recreating user — owner={ownerUsername}, oldRole={existingOwner.Role}");
                }

                // Wave 2: Encrypt any pre-existing plaintext PII in dev DB
                if (app.Environment.IsDevelopment())
                {
                    var migrationService = scope.ServiceProvider.GetRequiredService<CoreHub.Services.DataProtection.PiiDataMigrationService>();
                    await migrationService.MigrateAsync();
                }

                // W3: Seed AccountChart reference data (clear + reseed to ensure chart matches code).
                // Reference data is NOT user-editable — clear+reseed propagates label fixes + account additions/removals.
                // AccountCharts has no FK dependencies, safe to clear before HTTP requests start.
                CoreHub.Infrastructure.IVanAnDbContext vanAnContext = scope.ServiceProvider.GetRequiredService<CoreHub.Infrastructure.IVanAnDbContext>();
                await CoreHub.Infrastructure.Seed.AccountChartSeeder.CleanupAsync(vanAnContext);
                int accountChartCount = await CoreHub.Infrastructure.Seed.AccountChartSeeder.SeedAsync(vanAnContext);
                Console.WriteLine($"W3: AccountChart reference data seeded — {accountChartCount} accounts across 2 standards (TT 133 + TT 99)");
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                _ = app.UseExceptionHandler("/Error");
                _ = app.UseHsts();
            }

            // W7-T5 (2026-07-05): Security headers middleware — defense in depth
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
                await next();
            });

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

            // Wave 7: Enable HTTPS redirection only in Production
            if (!app.Environment.IsDevelopment())
            {
                _ = app.UseHttpsRedirection();
            }

            // MIDDLEWARE ORDER COMPLIANCE - RULE #2: StaticFiles -> Routing -> Auth -> Antiforgery -> MapRazorPages
            _ = app.UseStaticFiles(); // MUST be first to serve wwwroot files
            _ = app.UseRouting();
            _ = app.UseRateLimiter();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            _ = app.UseAntiforgery();

            // PROPER RAZOR PAGES ROUTING - ANTI-CHEATING RULE #2
            _ = app.MapControllers(); // If you have API controllers in ShopERP

            // T-20: Dev-only login endpoint for Playwright E2E tests.
            // W5 hardening: Wrapped in #if DEBUG so the route is compiled out of Release builds.
            // The DevLoginController class itself is also #if DEBUG-guarded (see Controllers/DevLoginController.cs).
            // VanAn.Architecture.Tests enforces this via DevLoginControllerReleaseBuildGuardTests.
#if DEBUG
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
#endif
            _ = app.MapHealthChecks("/health");
            _ = app.MapHealthChecks("/health/detail", new HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";
                    var result = new
                    {
                        status = report.Status.ToString(),
                        totalDuration = report.TotalDuration.TotalMilliseconds,
                        entries = report.Entries.ToDictionary(e => e.Key, e => new
                        {
                            status = e.Value.Status.ToString(),
                            duration = e.Value.Duration.TotalMilliseconds,
                            exception = e.Value.Exception?.Message
                        })
                    };
                    await context.Response.WriteAsJsonAsync(result);
                }
            }).RequireAuthorization("OwnerOnly");

            _ = app.MapRazorPages();
            _ = app.MapRazorComponents<Components.App>()
                .AddInteractiveServerRenderMode();
            _ = app.MapFallbackToPage("/Index"); // Proper fallback to Razor Page, not static HTML

            string urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5003";
            await app.RunAsync(urls);
        }

        /// <summary>
        /// SaaS W1: Validate Production configuration — fail fast if env var references remain
        /// unresolved or required secrets are missing. Prevents accidental deployment with placeholder config.
        /// SaaS W3 fix: appsettings.Production.json now uses ${VAR} env var references (not __REPLACE_* sentinels).
        /// .NET ConfigurationBinder does NOT auto-resolve ${VAR} — env vars must be set with __-separated keys
        /// (e.g. Jwt__Secret, Brevo__ApiKey). This validator catches both unresolved ${VAR} placeholders
        /// and missing env vars.
        /// </summary>
        private static void ValidateProductionConfig(ConfigurationManager configuration)
        {
            var failures = new List<string>();

            // Check for unresolved ${VAR} references or __REPLACE_* sentinels — both signal
            // that the env var was not set. .NET config uses __-separated env var keys (e.g. Jwt__Secret).
            string[] sentinelKeys =
            [
                "Jwt:Secret",
                "DataProtection:KeyDirectory",
                "Brevo:ApiKey",
                "Brevo:SenderEmail",
                "Esms:ApiKey",
                "Esms:SecretKey",
                "Esms:BrandName",
                "ConnectionStrings:Redis"
            ];

            foreach (var key in sentinelKeys)
            {
                string? value = configuration[key];
                if (string.IsNullOrWhiteSpace(value)
                    || value.Contains("__REPLACE_", StringComparison.Ordinal)
                    || (value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal)))
                {
                    failures.Add($"Config '{key}' is missing or unresolved. Set via env var (e.g. {key.Replace(":", "__", StringComparison.Ordinal)}).");
                }
            }

            // JWT secret length check
            string? jwtSecret = configuration["Jwt:Secret"];
            if (!string.IsNullOrWhiteSpace(jwtSecret)
                && !jwtSecret.Contains("__REPLACE_", StringComparison.Ordinal)
                && !(jwtSecret.StartsWith("${", StringComparison.Ordinal) && jwtSecret.EndsWith("}", StringComparison.Ordinal))
                && jwtSecret.Length < 32)
            {
                failures.Add("Jwt:Secret must be at least 32 characters for HS256 security.");
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production configuration validation failed:\n" + string.Join("\n", failures.Select(f => $"  - {f}")));
            }
        }
    }
}
