using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VanAn.Shared.Services;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.Gateway.Middleware;
using VanAn.Gateway.Hubs;
using VanAn.Gateway.Services;
using VanAn.CoreHub.Infrastructure;
using Serilog;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Tests")]

namespace VanAn.Gateway
{
    public partial class Program
    {
        public static void Main(string[] args)
        {
            // Wave 4: Clear the default inbound claim type map so JWT short-form claims ("role", "sub")
            // are NOT silently remapped to long Microsoft schema URLs at runtime.
            // This ensures RoleClaimType = "role" matches exactly what arrives in the JWT payload.
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Architect: Dynamic file logging configuration
            _ = builder.Host.UseSerilog((context, config) =>
            {
                _ = config.WriteTo.Console();

                // Architect: Only enable Disk I/O logging if explicitly turned on in appsettings
                if (context.Configuration.GetValue<bool>("LoggingConfig:EnableFileLogging"))
                {
                    string? appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                    _ = config.WriteTo.File(
                        path: Path.Combine(AppContext.BaseDirectory, "Logs", $"{appName}-.txt"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 2
                    );
                }
            });

            // Add services to the container.
            // SaaS W1: Validate Production config — fail fast if __REPLACE_* sentinels remain
            if (builder.Environment.IsProduction())
            {
                ValidateProductionConfig(builder.Configuration);
            }

            _ = builder.Services.AddControllers();
            _ = builder.Services.AddSignalR();

            // Register CoreHub DbContext for monolithic architecture (in-process services)
            string connectionString = builder.Configuration.GetSection("ConnectionStrings")["DefaultConnection"]
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection configuration is required in Gateway.");
            _ = builder.Services.AddDbContext<VanAn.CoreHub.Infrastructure.IVanAnDbContext, VanAn.CoreHub.Infrastructure.VanAnDbContext>(options =>
            {
                // Auto-detect provider: SQLite ("Data Source=") for local dev, Npgsql ("Host=") for production
                if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                    options.UseSqlite(connectionString);
                else
                    options.UseNpgsql(connectionString);
            });

            // Wave 0: JWT + Cookie dual-scheme authentication
            // Cookie is default scheme (keeps Blazor UI working).
            // JwtBearer is secondary scheme for API endpoints — validate tokens issued by ShopERP.
            var jwtSecret = builder.Configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException("Jwt:Secret configuration is required in Gateway.");
            var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "VanAnShopERP";
            var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "VanAnApi";

            _ = builder.Services.AddAuthentication(options =>
            {
                // Cookie remains the default scheme — Blazor UI continues to work unchanged
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/login";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    // W4 Fix: Forward to JWT Bearer when Authorization header is present.
                    // This enables dual-scheme auth: Cookie for Blazor UI, JWT for API tests.
                    options.ForwardDefaultSelector = context =>
                    {
                        if (context.Request.Headers.TryGetValue("Authorization", out var auth)
                            && auth.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            return JwtBearerDefaults.AuthenticationScheme;
                        }
                        return null; // Use Cookie (default scheme)
                    };
                })
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    // Wave 4: DefaultInboundClaimTypeMap is cleared at startup so claims stay as short-form.
                    // RoleClaimType = "role" and NameClaimType = "sub" must match the JWT payload keys exactly.
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,
                        ValidateAudience = true,
                        ValidAudience = jwtAudience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,
                        RoleClaimType = "role",
                        NameClaimType = "sub"
                    };
                });

            _ = builder.Services.AddAuthorizationBuilder()
                .AddPolicy("RequireTenantAccess", policy =>
                    policy.RequireAuthenticatedUser()
                           .RequireClaim("tenant_id"))
                .AddPolicy("RequireOwnerRole", policy =>
                    policy.RequireAuthenticatedUser()
                           .RequireClaim("tenant_id")
                           .RequireRole("Owner"))
                .AddPolicy("RequireStoreKeeperRole", policy =>
                    policy.RequireAuthenticatedUser()
                           .RequireClaim("tenant_id")
                           .RequireRole("StoreKeeper"))
                // Wave 5: SystemAdmin — cross-tenant operations (Tenant CRUD) - platform-level admin
                .AddPolicy("SystemAdmin", policy =>
                    policy.RequireAuthenticatedUser()
                           .RequireRole("SystemAdmin"));

            // Wave 1 Phase 2: Register ITenantProvider for Gateway controllers
            _ = builder.Services.AddHttpContextAccessor();
            _ = builder.Services.AddScoped<ITenantProvider, HttpContextTenantProvider>();

            // Add YARP Reverse Proxy
            _ = builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

            // Register VietQR Service
            _ = builder.Services.AddHttpClient<IVietQrService, VietQrService>();
            _ = builder.Services.AddScoped<IVietQrService, VietQrService>();

            // W17: Named HttpClient to forward requests to ShopERP
            _ = builder.Services.AddHttpClient("shoperp", client =>
            {
                client.BaseAddress = new Uri(
                    builder.Configuration["ShopERP:BaseUrl"] ?? "http://shoperp:80/");
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            // Register MST Lookup Service (Business Lookup Proxy for KhachLink)
            _ = builder.Services.AddHttpClient("VietQR", client =>
            {
                client.BaseAddress = new Uri("https://api.vietqr.io/v2/");
                client.Timeout = TimeSpan.FromSeconds(3);
            });
            _ = builder.Services.AddScoped<IMstLookupService, MstLookupService>();

            // Register Swagger for API documentation
            _ = builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new()
                {
                    Title = "VanAn Gateway API",
                    Version = "v1",
                    Description = "VanAn Ecosystem Gateway Service API Documentation"
                });
            });

            // Register ShopConfig Service
            _ = builder.Services.AddScoped<IShopConfigService, ShopConfigService>();

            // Register Onboarding Service
            _ = builder.Services.AddHttpClient<IOnboardingService, OnboardingService>();
            _ = builder.Services.AddScoped<IOnboardingService, OnboardingService>();

            // Wave 4: Notification services (INotificationService required by TenantManagementService + UserManagementService)
            _ = builder.Services.AddHttpClient<VanAn.CoreHub.Services.IEmailService, VanAn.CoreHub.Services.BrevoEmailService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            _ = builder.Services.AddHttpClient<VanAn.CoreHub.Services.ISmsService, VanAn.CoreHub.Services.EsmsNotificationService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.INotificationService, VanAn.CoreHub.Services.CompositeNotificationService>();

            // Wave 4: Register Tenant Onboarding Service dependencies (used by TenantOnboardingService orchestrator)
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ITenantManagementService, VanAn.CoreHub.Services.TenantManagementService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IUserManagementService, VanAn.CoreHub.Services.UserManagementService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IRoleAssignmentService, VanAn.CoreHub.Services.RoleAssignmentService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IPermissionGroupService, VanAn.CoreHub.Services.PermissionGroupService>();

            // Wave 4: Register Tenant Onboarding Service + industry seed strategies
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.ITenantOnboardingService, VanAn.CoreHub.Services.Onboarding.TenantOnboardingService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.FnbSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.SpaSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.HotelSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.BarberSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.ClothesSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.HealthySeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.PetShopSeedStrategy>();

            // Register Voice Command Services
            _ = builder.Services.AddScoped<IVoiceCommandService, VoiceCommandService>();
            _ = builder.Services.AddScoped<IAudioStorageService, AudioStorageService>();
            _ = builder.Services.AddMemoryCache();
            _ = builder.Services.AddScoped<ILocalizationService, LocalizationService>();

            // Wave 14: HMAC Request Signing — register CoreHub repo + service + Gateway adapter
            _ = builder.Services.AddScoped<VanAn.Shared.Repositories.IApiKeyRepository, VanAn.CoreHub.Infrastructure.Repositories.ApiKeyRepository>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IApiKeyManagementService, VanAn.CoreHub.Services.ApiKeyManagementService>();
            _ = builder.Services.AddScoped<IHmacApiKeyLookup, HmacApiKeyLookupAdapter>();

            // Wave 7: HKD Book accounting services — register repositories + services + calc engine.
            // PRIOR BUG: AccountingEntriesController injected IHKDBookService/IAccountingService/IReversalService
            // but Gateway Program.cs never registered them → runtime 500 on any endpoint using them.
            // Hidden because GatewayStartupTests only hit /health + auth-challenge routes.
            // Repository layer
            _ = builder.Services.AddScoped<IAccountingEntryRepository, AccountingEntryRepository>();
            _ = builder.Services.AddScoped<IHKDBookRepository, HKDBookRepository>();
            _ = builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Repositories.ISocialCampaignRepository, VanAn.CoreHub.Infrastructure.Repositories.SocialCampaignRepository>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ISocialCampaignService, VanAn.CoreHub.Services.SocialCampaignService>();
            // P3 FIX: Register missing repositories needed by services
            _ = builder.Services.AddScoped<VanAn.CoreHub.Repositories.IOrderRepository, VanAn.CoreHub.Repositories.OrderRepository>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Domain.Repositories.ICustomerRepository, VanAn.CoreHub.Infrastructure.Repositories.CustomerRepository>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Infrastructure.Repositories.ITenantProviderConfigurationService, VanAn.CoreHub.Infrastructure.Repositories.TenantProviderConfigurationService>();
            // Core services
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IAccountingService, VanAn.CoreHub.Services.AccountingEntryService>();
            _ = builder.Services.AddScoped<IHKDBookService, HKDBookService>();
            _ = builder.Services.AddScoped<IReversalService, ReversalService>();
            _ = builder.Services.AddScoped<IPeriodClosingService, PeriodClosingService>();
            _ = builder.Services.AddScoped<IAuditTrailService, AuditTrailService>();
            // P3 FIX: Register missing services referenced by Gateway controllers
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IBuildService, VanAn.CoreHub.Services.BuildService>();
            _ = builder.Services.AddScoped<VanAn.Shared.Services.IKitchenService, VanAn.CoreHub.Services.KitchenService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IOrderService, VanAn.CoreHub.Services.OrderService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IProviderManager, VanAn.CoreHub.Services.ProviderManager>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IExcelExportService, VanAn.CoreHub.Services.ExcelExportService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Orchestration.IWebhookService, VanAn.CoreHub.Services.Orchestration.WebhookService>();
            _ = builder.Services.AddScoped<VanAn.Shared.Services.IInventoryService, VanAn.CoreHub.Services.InventoryService>();
            // Calc engine (Wave 3 wiring replicated for Gateway in-process host)
            // Dependency order: IFormulaEngine -> IPreAggregationService -> IDataProvider
            // -> IBookResultCache -> TemplateFactory (concrete) -> IHKDBookGenerationService
            // Lazy<IFormulaEngine> breaks circular dependency: FormulaEngine -> DataProvider
            // -> PreAggregation -> FormulaEngine (SmartPreAggregationService uses Lazy<IFormulaEngine>)
            _ = builder.Services.AddScoped<Lazy<VanAn.CoreHub.Services.Formula.IFormulaEngine>>(
                sp => new Lazy<VanAn.CoreHub.Services.Formula.IFormulaEngine>(() => sp.GetRequiredService<VanAn.CoreHub.Services.Formula.IFormulaEngine>()));
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Formula.IFormulaEngine, VanAn.CoreHub.Services.Formula.ProductionFormulaEngine>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.PreAggregation.IPreAggregationService, VanAn.CoreHub.Services.PreAggregation.SmartPreAggregationService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Data.IDataProvider, VanAn.CoreHub.Services.Data.ScopedDataProvider>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Cache.IBookResultCache, VanAn.CoreHub.Services.Cache.BookResultCache>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Template.TemplateFactory>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Template.IHKDBookGenerationService, VanAn.CoreHub.Services.Template.HKDBookGenerationService>();

            // W-1-T5 (S4, S5): Register NATS subscribers for SQLite→PostgreSQL sync flow
            // DataSyncSubscriber: subscribes vanan.shoperp.> → writes Order/Customer status to PostgreSQL
            // SimpleAccountingEventHandler: subscribes vanan.shoperp.ordercompleted → creates accounting entries + HKD books
            // Both run in Gateway scope (has VanAnDbContext = PostgreSQL).
            // Degraded mode: if NATS unavailable, services log warning and skip events.
            _ = builder.Services.AddHostedService<VanAn.Gateway.Services.DataSyncSubscriber>();
            _ = builder.Services.AddHostedService<VanAn.CoreHub.Services.Events.SimpleAccountingEventHandler>();

            // W0-T3: Register IOrderNotificationService (SignalR broadcast abstraction)
            // Implemented in Gateway using IHubContext<OrderHub> — CoreHub stays pure class library.
            _ = builder.Services.AddScoped<VanAn.CoreHub.Interfaces.IOrderNotificationService, VanAn.Gateway.Services.OrderNotificationService>();

            // Wave 14: Build HmacSigningOptions from configuration
            var hmacOptions = new VanAn.Gateway.Middleware.HmacSigningOptions();
            var protectedPaths = builder.Configuration
                .GetSection("HmacSigning:ProtectedPaths")
                .Get<string[]>() ?? [];
            hmacOptions.ProtectedPaths = protectedPaths.Select(p => new PathString(p)).ToList();
            _ = builder.Services.AddSingleton(hmacOptions);

            // Wave 7: CORS hardening — whitelist from configuration
            string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["*"];
            _ = builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    if (allowedOrigins.Contains("*"))
                    {
                        _ = policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    }
                    else
                    {
                        _ = policy.WithOrigins(allowedOrigins)
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    }
                });
            });

            WebApplication app = builder.Build();

            try
            {
                Log.Information("🚀 Starting Vạn An Gateway Service...");

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    _ = app.UseSwagger();
                    _ = app.UseSwaggerUI();
                }

                // Add unified error handling middleware
                _ = app.UseMiddleware<UnifiedErrorHandler>();

                // Wave 7: Enable HTTPS redirection only in Production
                if (!app.Environment.IsDevelopment())
                {
                    _ = app.UseHttpsRedirection();
                }

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

                _ = app.UseCors("AllowAll");

                // Wave 1 Phase 2: Authentication & Authorization middleware
                _ = app.UseAuthentication();
                _ = app.UseAuthorization();

                // Wave 14: HMAC Request Signing — validate signatures on protected paths
                _ = app.UseMiddleware<VanAn.Gateway.Middleware.HmacSigningMiddleware>();

                // Add Localization Middleware
                _ = app.UseMiddleware<LocalizationMiddleware>();

                // W4 Fix: Map controllers BEFORE YARP so Gateway's own API endpoints
                // (OrdersController, VietQrController, etc.) take priority over the
                // YARP fallback catch-all route ({**catch-all} → khachlink-cluster).
                // Without this, /api/* requests get forwarded to KhachLink (HTML) instead
                // of being handled by Gateway controllers.
                _ = app.MapControllers();
                _ = app.MapHub<OrderHub>("/orderHub");
                _ = app.MapHub<KitchenHub>("/kitchenhub");

                // Add YARP Reverse Proxy (after controllers so it only catches non-API routes)
                _ = app.MapReverseProxy();

                // Health check endpoint
                _ = app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "VanAn Gateway", Timestamp = DateTime.UtcNow }));

                // ÉP CỨNG BINDING - Fix 404
                // Respect ASPNETCORE_URLS env (Docker: http://+:80). Fallback to 5001 for local dev.
                var aspUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
                if (!string.IsNullOrEmpty(aspUrls))
                {
                    app.Run();
                }
                else
                {
                    app.Urls.Add("http://0.0.0.0:5001");
                    app.Run("http://0.0.0.0:5001");
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "❌ Gateway Service terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// SaaS W1: Validate Production configuration — fail fast if __REPLACE_* sentinels remain.
        /// </summary>
        private static void ValidateProductionConfig(ConfigurationManager configuration)
        {
            string? jwtSecret = configuration["Jwt:Secret"];
            if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Contains("__REPLACE_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Jwt:Secret is missing or still has __REPLACE_* sentinel. Set via Jwt__Secret env var.");
            }
            if (jwtSecret.Length < 32)
            {
                throw new InvalidOperationException("Jwt:Secret must be at least 32 characters for HS256 security.");
            }
        }
    }

    public partial class Program { }
}
