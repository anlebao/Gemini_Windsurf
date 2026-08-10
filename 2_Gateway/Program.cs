using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using VanAn.Shared.Services;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.Gateway.Middleware;
using VanAn.Gateway.Hubs;
using VanAn.Gateway.Services;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using Serilog;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VanAn.Core.Tests")]

namespace VanAn.Gateway
{
    public partial class Program
    {
        public static async Task Main(string[] args)
        {
            // Npgsql 7+: Enable legacy timestamp behavior so DateTime with Kind=Unspecified works
            // with PostgreSQL 'timestamp with time zone' columns (same switch as ShopERP).
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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

            _ = builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                    // Fix #87: Serialize enums as strings (CommerceMode, OrderStatus, etc.)
                    // ShopERP clients expect string values, not int.
                    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                });
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

            // Wave 1-3: Register IAccountingDbContext → VanAnDbContext (same instance, implements both interfaces).
            // Accounting repositories (AccountingEntryRepository, HKDBookRepository, AuditLogRepository) inject IAccountingDbContext.
            // Without this registration, Gateway crashes on startup with "Unable to resolve service for type IAccountingDbContext".
            _ = builder.Services.AddScoped<VanAn.CoreHub.Infrastructure.IAccountingDbContext>(provider =>
                provider.GetRequiredService<VanAn.CoreHub.Infrastructure.VanAnDbContext>());

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
                        // FIX: IdentityModel v7.1.2 doesn't auto-try IssuerSigningKey when JWT has no kid header.
                        // JwtTokenService issues HS256 tokens without kid (symmetric key) — resolver must
                        // explicitly return the configured key. Without this, all [Authorize] endpoints return 401
                        // with "The signature key was not found".
                        IssuerSigningKeyResolver = (_, _, _, validationParameters) => new[] { validationParameters.IssuerSigningKey },
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,
                        ValidateAudience = true,
                        ValidAudience = jwtAudience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,
                        // FIX: JwtTokenService emits role as ClaimTypes.Role (long-form URI
                        // http://schemas.microsoft.com/ws/2008/06/identity/claims/role).
                        // With MapInboundClaims=false above, the claim type stays as the long-form URI.
                        // RoleClaimType must match the actual claim type so RequireRole() finds it.
                        RoleClaimType = ClaimTypes.Role,
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

            // Phase 2 (Multi-VPS Checkout): Normalize JWT role claims — accept both short-form ("role")
            // and long-form (ClaimTypes.Role URI) in Bearer JWTs. See RoleClaimNormalizer for details.
            _ = builder.Services.AddTransient<IClaimsTransformation, VanAn.Gateway.Infrastructure.RoleClaimNormalizer>();

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

            // Community Commerce Sprint 0 v1.2/v1.4: RiskScoringService + WalletService base (PG-only)
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IRiskScoringService, VanAn.CoreHub.Services.RiskScoringService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IWalletService, VanAn.CoreHub.Services.WalletService>();
            // F3 fix 2026-07-26: DeviceRegistrationService — max 3 active devices per Customer enforcement
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IDeviceRegistrationService, VanAn.CoreHub.Services.DeviceRegistrationService>();
            // CC-S1-T1/T2 (Sprint 1): CommunityOrderService — nearby orders (Haversine) + accept (concurrency-safe)
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ICommunityOrderService, VanAn.CoreHub.Services.CommunityOrderService>();

            // CC-S2 (Sprint 2): DeliveryWorkflowService — delivery state machine + GPS location recording
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IDeliveryWorkflowService, VanAn.CoreHub.Services.DeliveryWorkflowService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IChatService, VanAn.CoreHub.Services.ChatService>();

            // CC-S4 (Sprint 4): Salesman + Composite QR Referral + App-Install Bonus + Risk Scoring + FraudFlag
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ISalesmanService, VanAn.CoreHub.Services.SalesmanService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IAppInstallAttributionService, VanAn.CoreHub.Services.AppInstallAttributionService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IProductReferralConfigService, VanAn.CoreHub.Services.ProductReferralConfigService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IFraudFlagService, VanAn.CoreHub.Services.FraudFlagService>();

            // CC-S6 (Sprint 6): Community Admin + Fraud Review services
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ICommunityAdminService, VanAn.CoreHub.Services.CommunityAdminService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IFraudReviewService, VanAn.CoreHub.Services.FraudReviewService>();

            // Sprint 7 — Commerce Mode Toggle: CommerceMode + CommunityFund services
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ICommerceModeService, VanAn.CoreHub.Services.CommerceModeService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ICommunityFundService, VanAn.CoreHub.Services.CommunityFundService>();

            // CC-S6-T5 — Collaborator SMS OTP + Deposit Wallet (toggle-gated)
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ICollaboratorVerificationService, VanAn.CoreHub.Services.CollaboratorVerificationService>();

            // Loyalty Alliance System — Phase 2A: mode resolver + cross-tenant wallet service (PG-only)
            _ = builder.Services.AddScoped<VanAn.Shared.Services.ILoyaltyModeResolver, VanAn.CoreHub.Services.LoyaltyModeResolver>();
            _ = builder.Services.AddScoped<VanAn.Shared.Services.IAllianceWalletService, VanAn.CoreHub.Services.AllianceWalletService>();

            // Loyalty Consistency Fix Phase 0 (Option B): Internal API key for service-to-service auth.
            // ShopERP HTTP proxies call /api/internal/loyalty/* with X-Internal-Api-Key header.
            // Config: InternalLoyalty:ApiKey (env var: InternalLoyalty__ApiKey). Validated by InternalApiKeyAttribute.
            // No explicit DI binding needed — InternalApiKeyAttribute reads via IConfiguration at request time.

            // Wave 4: Register Tenant Onboarding Service + industry seed strategies
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.ITenantOnboardingService, VanAn.CoreHub.Services.Onboarding.TenantOnboardingService>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.FnbSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.SpaSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.HotelSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.BarberSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.ClothesSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.HealthySeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.PetShopSeedStrategy>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.Onboarding.IIndustrySeedStrategy, VanAn.CoreHub.Services.Onboarding.Strategies.RetailSeedStrategy>();

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
            _ = builder.Services.AddScoped<VanAn.Shared.Services.ISocialCampaignService, VanAn.CoreHub.Services.SocialCampaignService>();
            // P3 FIX: Register missing repositories needed by services
            _ = builder.Services.AddScoped<VanAn.CoreHub.Repositories.IOrderRepository, VanAn.CoreHub.Repositories.OrderRepository>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Repositories.IProductRepository, VanAn.CoreHub.Repositories.ProductRepository>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Domain.Repositories.ICustomerRepository, VanAn.CoreHub.Infrastructure.Repositories.CustomerRepository>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Infrastructure.Repositories.ITenantProviderConfigurationService, VanAn.CoreHub.Infrastructure.Repositories.TenantProviderConfigurationService>();
            // Phase 5: Customer segmentation service for bulk push campaigns
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ICustomerSegmentationService, VanAn.CoreHub.Services.CustomerSegmentationService>();
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
            // W2-T6: Shop feature toggle settings — needed by OrderService.ConfirmPaymentAsync for accounting bypass
            _ = builder.Services.AddScoped<VanAn.Shared.Services.IShopFeatureSettingsService, VanAn.CoreHub.Services.ShopFeatureSettingsService>();
            _ = builder.Services.AddHttpClient<VanAn.CoreHub.Services.IShopInstanceService, VanAn.CoreHub.Services.ShopInstanceService>();
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

            // Phase 3.5: EInvoiceSyncSubscriber — subscribes vanan.shoperp.einvoice.synced.>
            // ShopERP publishes e-invoice result after submission → this subscriber updates PG ElectronicInvoice table.
            _ = builder.Services.AddHostedService<VanAn.Gateway.Services.EInvoiceSyncSubscriber>();

            // Sync: Register Outbox + NatsSyncWorker for Gateway→ShopERP sync (PostgreSQL → NATS → SQLite)
            // Gateway writes orders to PostgreSQL; Outbox event is enqueued by OrderService.CreateOrderFromCommandAsync.
            // NatsSyncWorker polls Outbox (PostgreSQL) and publishes to NATS → ShopERP subscriber syncs to SQLite.
            // RC-2 fix: Gateway publishes with prefix "cloud" (vanan.cloud.*) to distinguish from
            // ShopERP's SQLite→PG direction (vanan.shoperp.*). ShopERP OrderSyncSubscriber listens to vanan.cloud.*.
            _ = builder.Services.AddSingleton<INatsEventPublisher, NatsEventPublisher>();
            _ = builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
            _ = builder.Services.AddHostedService<NatsSyncWorker>();

            // CC-S4 (Sprint 4 v1.2): Background jobs for risk scoring cooling period + held timeout
            _ = builder.Services.AddHostedService<VanAn.CoreHub.Services.CoolingPeriodJob>();
            _ = builder.Services.AddHostedService<VanAn.CoreHub.Services.HeldTimeoutJob>();

            // VALCN v2.0 Phase 3: Loyalty budget reset jobs (Gateway — PG is source of truth for LoyaltyTenantConfigs)
            _ = builder.Services.AddHostedService<VanAn.CoreHub.Services.LoyaltyBudgetDailyResetJob>();
            _ = builder.Services.AddHostedService<VanAn.CoreHub.Services.LoyaltyBudgetMonthlyResetJob>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ILoyaltyBudgetService, VanAn.CoreHub.Services.LoyaltyBudgetService>();

            // Loyalty rewards repository + service — required by RefundOrchestrationService (Phase 4)
            // and by LoyaltyRewardsService itself for SubtractPointsAsync reversal path.
            _ = builder.Services.AddScoped<VanAn.CoreHub.Repositories.ILoyaltyRewardsRepository, VanAn.CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.ILoyaltyRewardsService, VanAn.CoreHub.Services.LoyaltyRewardsService>();

            // VALCN v2.0 Phase 4: Refund orchestration (4-step reversal on cancel — feature-flagged, default OFF)
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IRefundOrchestrationService, VanAn.CoreHub.Services.RefundOrchestrationService>();

            // VALCN v2.0 Phase 7: Network dashboard (cross-tenant aggregate metrics — read-only, 10-min cache)
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.INetworkDashboardService, VanAn.CoreHub.Services.NetworkDashboardService>();

            // REQ-1.2: Background service toggle — runtime on/off via SystemSetting (PG) + admin UI
            _ = builder.Services.AddSingleton<CoreHub.Services.IBackgroundServiceToggleService, CoreHub.Services.BackgroundServiceToggleService>();

            // VALCN v2.0 Phase 1: Feature flag toggle — default OFF (existing behavior preserved)
            _ = builder.Services.AddSingleton<CoreHub.Services.IFeatureFlagService, CoreHub.Services.FeatureFlagService>();

            _ = builder.Services.AddScoped<CoreHub.Services.IOrderService, CoreHub.Services.OrderService>();

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

                // Apply PostgreSQL migrations on Gateway startup (production).
                // The Gateway uses VanAnDbContext (PG) for Tenants, Orders, Accounting, etc.
                // Previously relied on ShopERP to apply PG migrations — but if the Gateway starts
                // before ShopERP (or ShopERP is on an older version), PG is missing new columns
                // (e.g., TenantSettings_LegalForm/NavColor) and all Tenant queries fail with 500.
                // Fix #101: Gateway applies its own PG migrations on startup.
                if (!connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var migrateScope = app.Services.CreateScope();
                        var vanAnDb = migrateScope.ServiceProvider.GetRequiredService<VanAn.CoreHub.Infrastructure.VanAnDbContext>();
                        await vanAnDb.Database.MigrateAsync();
                        Log.Information("PostgreSQL database migrated (Gateway)");

                        // Order sync seed: ensure ShopInstance exists + tenants assigned.
                        // Without this, NATS routing key mismatch → orders never sync to ShopERP.
                        await SeedShopInstanceAndAssignTenantsAsync(migrateScope.ServiceProvider);
                    }
                    catch (Exception migrateEx)
                    {
                        Log.Warning(migrateEx, "PostgreSQL migration skipped (may already be applied by ShopERP)");
                    }
                }

                // Local dev SQLite schema sync: ShopERP's migration creates AccountingEntries with
                // audit columns only (AccountingEntry DbSet removed from ShopERPDbContext per ADR-001).
                // Gateway uses VanAnDbContext which expects full business columns (AccountCode, Amount,
                // EntryType, etc.). On SQLite local dev, patch the missing columns via ALTER TABLE.
                // Production uses PostgreSQL where VanAnDbContext migrations create the full schema.
                if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await EnsureSqliteAccountingSchemaAsync(app.Services);
                    }
                    catch (Exception schemaEx)
                    {
                        Log.Warning(schemaEx, "SQLite schema patch skipped (table may not exist yet in test/dev)");
                    }
                }

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
                _ = app.MapHub<LocationHub>("/hubs/location");
                _ = app.MapHub<ChatHub>("/hubs/chat");

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

        /// <summary>
        /// Local dev SQLite schema patch: add missing business columns to AccountingEntries table.
        /// ShopERP's ShopERPDbContext migration creates AccountingEntries with audit columns only
        /// (AccountingEntry DbSet removed per ADR-001). Gateway's VanAnDbContext expects full
        /// business columns. This method patches the gap via ALTER TABLE ADD COLUMN (SQLite-safe,
        /// idempotent — checks PRAGMA table_info before adding).
        /// </summary>
        private static async Task EnsureSqliteAccountingSchemaAsync(IServiceProvider services)
        {
            using IServiceScope scope = services.CreateScope();
            VanAnDbContext context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();

            // Columns that VanAnDbContext expects but ShopERP's migration doesn't create.
            // SQLite ALTER TABLE ADD COLUMN is null-tolerant for existing rows.
            (string Name, string Type)[] requiredColumns =
            [
                ("Amount", "REAL NOT NULL DEFAULT 0"),
                ("EntryType", "INTEGER NOT NULL DEFAULT 0"),
                ("VatRate", "INTEGER NOT NULL DEFAULT 0"),
                ("AccountingBookType", "INTEGER NOT NULL DEFAULT 0"),
                ("PeriodYear", "INTEGER NOT NULL DEFAULT 2000"),
                ("PeriodMonth", "INTEGER NOT NULL DEFAULT 1"),
                ("ReversalEntryId", "TEXT"),
                ("Description", "TEXT NOT NULL DEFAULT ''"),
                ("AccountCode", "TEXT"),
                ("Vendor", "TEXT"),
                ("Category", "TEXT"),
                ("Reference", "TEXT"),
                ("IndustrySector", "INTEGER"),
            ];

            // Query existing columns
            var existingColumns = await context.Database.SqlQueryRaw<ColumnInfo>(
                "PRAGMA table_info(AccountingEntries)").ToListAsync();
            var existingNames = existingColumns.Select(c => c.Name).ToHashSet();

            int added = 0;
            foreach ((string name, string type) in requiredColumns)
            {
                if (!existingNames.Contains(name))
                {
                    await context.Database.ExecuteSqlRawAsync(
                        $"ALTER TABLE AccountingEntries ADD COLUMN {name} {type}");
                    added++;
                }
            }

            if (added > 0)
            {
                Log.Information("SQLite schema patch: added {Count} missing columns to AccountingEntries", added);
            }
        }

        private class ColumnInfo
        {
            public int Cid { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public int NotNull { get; set; }
            [Column("dflt_value")]
            public string? DfltValue { get; set; }
            public int Pk { get; set; }
        }

        /// <summary>
        /// Order sync seed: ensures all tenants are assigned to an active ShopInstance.
        ///
        /// Without this, NATS routing key is null → orders published to unrouted subject
        /// → ShopERP subscriber never receives them → orders don't appear in ShopERP UI.
        ///
        /// Logic:
        /// 1. If SEED_SHOP_INSTANCE_ID is set and no matching ShopInstance exists → auto-create it
        /// 2. Find target ShopInstance — prefer SEED_SHOP_INSTANCE_ID match, else first active
        /// 3. Assign all unassigned tenants to it
        /// 4. If SEED_SHOP_INSTANCE_ID is set, reassign tenants currently on a DIFFERENT ShopInstance
        ///    (handles config drift: tenants assigned to old ID after secret change)
        ///
        /// SEED_SHOP_INSTANCE_ID env var (optional): if set, auto-creates the ShopInstance if
        /// missing and reassigns all tenants to it. Otherwise, the first active ShopInstance is used.
        /// </summary>
        private static async Task SeedShopInstanceAndAssignTenantsAsync(IServiceProvider serviceProvider)
        {
            try
            {
                var db = serviceProvider.GetRequiredService<VanAnDbContext>();

                // 1. Parse SEED_SHOP_INSTANCE_ID (optional)
                Guid? preferredShopInstanceId = null;
                string? seedIdStr = Environment.GetEnvironmentVariable("SEED_SHOP_INSTANCE_ID");
                if (Guid.TryParse(seedIdStr, out Guid seedId) && seedId != Guid.Empty)
                {
                    preferredShopInstanceId = seedId;
                }

                // 2. If SEED_SHOP_INSTANCE_ID is set but ShopInstance doesn't exist → auto-create
                if (preferredShopInstanceId.HasValue)
                {
                    bool exists = await db.ShopInstances
                        .IgnoreQueryFilters()
                        .AnyAsync(s => s.Id == preferredShopInstanceId.Value);
                    if (!exists)
                    {
                        var seedInstance = new ShopInstance(
                            baseUrl: "http://localhost",
                            label: $"Seeded {preferredShopInstanceId.Value.ToString()[..8]}",
                            maxTenants: 100);
                        // BaseEntity.Id has protected setter — use reflection to set the seed ID
                        // (seed operation, not domain business logic)
                        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!
                            .SetValue(seedInstance, preferredShopInstanceId.Value);
                        db.ShopInstances.Add(seedInstance);
                        await db.SaveChangesAsync();
                        Log.Information("SeedShopInstance: auto-created ShopInstance {Id} (label={Label})",
                            seedInstance.Id, seedInstance.Label);
                    }
                }

                // 3. Find target ShopInstance — prefer SEED_SHOP_INSTANCE_ID match, else first active
                var shopInstances = await db.ShopInstances
                    .IgnoreQueryFilters()
                    .Where(s => s.IsActive)
                    .ToListAsync();

                ShopInstance? targetInstance = preferredShopInstanceId.HasValue
                    ? shopInstances.FirstOrDefault(s => s.Id == preferredShopInstanceId.Value)
                    : null;
                targetInstance ??= shopInstances.FirstOrDefault();

                if (targetInstance == null)
                {
                    Log.Warning("SeedShopInstance: no active ShopInstance found — tenants cannot be assigned. " +
                                "Create a ShopInstance via POST /api/v1/shop-instances");
                    return;
                }

                // 4. Assign unassigned tenants + reassign tenants on a DIFFERENT ShopInstance (config drift fix)
                var tenantsToAssign = await db.Tenants
                    .IgnoreQueryFilters()
                    .Where(t => t.ShopInstanceId == null || t.ShopInstanceId != targetInstance.Id)
                    .ToListAsync();

                if (tenantsToAssign.Count > 0)
                {
                    int unassignedCount = tenantsToAssign.Count(t => t.ShopInstanceId == null);
                    int reassignedCount = tenantsToAssign.Count - unassignedCount;
                    foreach (var tenant in tenantsToAssign)
                    {
                        tenant.AssignToShopInstance(targetInstance.Id);
                    }
                    await db.SaveChangesAsync();
                    Log.Information("SeedShopInstance: assigned {Unassigned} new + reassigned {Reassigned} drifted tenant(s) to ShopInstance {Id} ({Label})",
                        unassignedCount, reassignedCount, targetInstance.Id, targetInstance.Label);
                }
                else
                {
                    Log.Debug("SeedShopInstance: all tenants already assigned to ShopInstance {Id}", targetInstance.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SeedShopInstance: failed — order sync may not work until tenants are assigned manually");
            }
        }
    }

    public partial class Program { }
}
