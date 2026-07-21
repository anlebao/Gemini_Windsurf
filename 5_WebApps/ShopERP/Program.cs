using VanAn.Shared.Domain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Providers.EInvoice;
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
            // Npgsql 7+: Enable legacy timestamp behavior so DateTime with Kind=Unspecified works
            // with PostgreSQL 'timestamp with time zone' columns. Domain layer uses new DateTime(year, month, 1)
            // which has Kind=Unspecified — Npgsql 7+ requires UTC by default. This switch restores Npgsql 6 behavior.
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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

            // FIX: JSON cycle detection — Order.Items[].Order navigation creates a cycle.
            // IgnoreCycles serializes back-references as null instead of throwing JsonException.
            builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });

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
            // CRITICAL: ShopERP ALWAYS uses SQLite for ShopERPDbContext (orders, products, users).
            // This ensures local dev matches VPS production — no PostgreSQL fallback for order data.
            // PostgreSQL (VanAnDbContext) is ONLY for accounting (IAccountingDbContext).
            string connectionString =
                Environment.GetEnvironmentVariable("SQLITE_DB_PATH")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? builder.Configuration.GetConnectionString("DefaultConnection")
                ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "vanan_shoperp.db")}";
            // Safety check: if connection string contains "Host=" or "Port=" it's PostgreSQL, not SQLite
            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"ShopERPDbContext MUST use SQLite, but connection string appears to be PostgreSQL: '{connectionString}'. " +
                    "Check ConnectionStrings:DefaultConnection in appsettings. SQLite format: 'Data Source=vanan_shoperp.db'");
            }
            Console.WriteLine($"[ShopERP] ShopERPDbContext (SQLite) connection: {connectionString}");
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

            // ADR-001: Accounting always online on PostgreSQL.
            // Register VanAnDbContext (PostgreSQL) + IAccountingDbContext for accounting services.
            string accountingConnectionString =
                Environment.GetEnvironmentVariable("ACCOUNTING_CONNECTION_STRING")
                ?? builder.Configuration.GetConnectionString("AccountingConnection")
                ?? "Host=localhost;Port=5432;Database=vanan_accounting;Username=vanan_admin;Password=VanAn@2024!";
            _ = builder.Services.AddDbContext<VanAn.CoreHub.Infrastructure.VanAnDbContext>(options =>
                options.UseNpgsql(accountingConnectionString));
            _ = builder.Services.AddScoped<IAccountingDbContext>(provider =>
                provider.GetRequiredService<VanAn.CoreHub.Infrastructure.VanAnDbContext>());

            // Register NATS publisher as Singleton (holds NATS connection)
            // Required by OrderWorkflowService even without --sync-worker mode
            builder.Services.AddSingleton<INatsEventPublisher, NatsEventPublisher>();

            // W-1-T3: Always register IOutboxRepository (uses IVanAnDbContext → ShopERPDbContext SQLite)
            // Previously gated behind --sync-worker flag, which meant OrderWorkflowService could not enqueue events
            builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();

            // W-1-T3: NatsSyncWorker runs by default (configurable via Sync:Enabled, default true)
            // Previously gated behind --sync-worker CLI arg, which meant sync never ran in production
            // Note: JSON config uses "Sync": { "Enabled": true } → key is "Sync:Enabled"
            //       Env var equivalent: Sync__Enabled=true
            bool syncEnabled = builder.Configuration.GetValue<bool>("Sync:Enabled", true);
            if (syncEnabled)
            {
                builder.Services.AddHostedService<NatsSyncWorker>();
                Log.Information("NatsSyncWorker registered (Sync__Enabled=true) — Outbox → NATS sync active");
            }
            else
            {
                Log.Information("NatsSyncWorker disabled (Sync__Enabled=false)");
            }

            // REMOVED: SimpleOutboxProcessor — NatsSyncWorker is the single Outbox processor (W-1-T3 / S2)
            // Duplicate processor would cause double-publish to NATS.
            // builder.Services.AddHostedService<SimpleOutboxProcessor>();

            // REMOVED: Enhanced OrderWorkflowService with queue integration
            // builder.Services.AddScoped<VanAn.ShopERP.Services.IOrderWorkflowService, VanAn.ShopERP.Services.OrderWorkflowService>();

            // Register CoreHub Services (FIX: Use CoreHub interfaces and implementations)
            _ = builder.Services.AddScoped<CoreHub.Services.IShopConfigService, CoreHub.Services.ShopConfigService>();
            _ = builder.Services.AddScoped<CoreHub.Services.ISocialCampaignService, CoreHub.Services.SocialCampaignService>();
            _ = builder.Services.AddScoped<CoreHub.Services.ILoyaltyRewardsService, CoreHub.Services.LoyaltyRewardsService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IOnboardingService, CoreHub.Services.OnboardingService>();
            // Industry seed strategies — registered for IOnboardingService.ApplyTemplateAsync to resolve by IndustryCode.
            // Must match Gateway Program.cs registrations (lines 230-236).
            _ = builder.Services.AddScoped<CoreHub.Services.Onboarding.IIndustrySeedStrategy, CoreHub.Services.Onboarding.Strategies.FnbSeedStrategy>();
            _ = builder.Services.AddScoped<CoreHub.Services.Onboarding.IIndustrySeedStrategy, CoreHub.Services.Onboarding.Strategies.SpaSeedStrategy>();
            _ = builder.Services.AddScoped<CoreHub.Services.Onboarding.IIndustrySeedStrategy, CoreHub.Services.Onboarding.Strategies.HotelSeedStrategy>();
            _ = builder.Services.AddScoped<CoreHub.Services.Onboarding.IIndustrySeedStrategy, CoreHub.Services.Onboarding.Strategies.BarberSeedStrategy>();
            _ = builder.Services.AddScoped<CoreHub.Services.Onboarding.IIndustrySeedStrategy, CoreHub.Services.Onboarding.Strategies.ClothesSeedStrategy>();
            _ = builder.Services.AddScoped<CoreHub.Services.Onboarding.IIndustrySeedStrategy, CoreHub.Services.Onboarding.Strategies.HealthySeedStrategy>();
            _ = builder.Services.AddScoped<CoreHub.Services.Onboarding.IIndustrySeedStrategy, CoreHub.Services.Onboarding.Strategies.PetShopSeedStrategy>();
            _ = builder.Services.AddScoped<CoreHub.Services.Onboarding.IIndustrySeedStrategy, CoreHub.Services.Onboarding.Strategies.RetailSeedStrategy>();
            _ = builder.Services.AddScoped<CoreHub.Services.IVoiceCommandService, CoreHub.Services.VoiceCommandService>();
            _ = builder.Services.AddScoped<Shared.Services.ICustomerService, CoreHub.Services.CustomerService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IOrderService, CoreHub.Services.OrderService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IOrderWorkflowService, CoreHub.Services.OrderWorkflowService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IAccountingService, CoreHub.Services.AccountingEntryService>();
            // KhachLink Full Flow W0: Shop feature toggle settings
            _ = builder.Services.AddScoped<CoreHub.Services.IShopFeatureSettingsService, CoreHub.Services.ShopFeatureSettingsService>();
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
            _ = builder.Services.AddScoped<CoreHub.Repositories.IProductRepository, CoreHub.Repositories.ProductRepository>();
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

            // E-Invoice Services (Sprint 3 — DI wiring for ShopERP host)
            // Mirrors 3_CoreHub/Program.cs registration. Required by InvoiceManagement.razor + HKDElectronicInvoiceController.
            _ = builder.Services.AddScoped<CoreHub.Services.Orchestration.IInvoicePolicyService, CoreHub.Services.Orchestration.InvoicePolicyService>();
            _ = builder.Services.Configure<ViettelConfig>(builder.Configuration.GetSection("ViettelConfig"));
            _ = builder.Services.AddHttpClient<ViettelEInvoiceProvider>("viettel", client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["ViettelConfig:BaseUrl"] ?? "https://sinvoice.viettel.vn/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            _ = builder.Services.Configure<MisaConfig>(builder.Configuration.GetSection("MisaConfig"));
            _ = builder.Services.AddHttpClient<MisaEInvoiceProvider>("misa", client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["MisaConfig:BaseUrl"] ?? "https://api.meinvoice.vn/");
                client.Timeout = TimeSpan.FromSeconds(45);
            });
            _ = builder.Services.AddSingleton<IEInvoiceProviderRegistry>(sp =>
            {
                var registry = new EInvoiceProviderRegistry();
                registry.RegisterProvider("viettel", typeof(ViettelEInvoiceProvider));
                registry.RegisterProvider("misa",    typeof(MisaEInvoiceProvider));
                return registry;
            });
            _ = builder.Services.AddScoped<IEInvoiceProviderFactory, EInvoiceProviderFactory>();
            _ = builder.Services.AddSingleton<CoreHub.Services.Resilience.ICircuitBreakerService, CoreHub.Services.Resilience.CircuitBreakerService>();
            _ = builder.Services.AddScoped<CoreHub.Services.Orchestration.IRetryPolicyService>(sp =>
            {
                var factory = sp.GetRequiredService<IEInvoiceProviderFactory>();
                var breaker = sp.GetRequiredService<CoreHub.Services.Resilience.ICircuitBreakerService>();
                var db      = sp.GetRequiredService<CoreHub.Infrastructure.VanAnDbContext>();
                var logger  = sp.GetRequiredService<ILogger<CoreHub.Services.Orchestration.RetryPolicyService>>();

                Func<VanAn.Shared.Domain.ElectronicInvoiceId, CancellationToken, Task> submitAction =
                    async (invoiceId, ct) =>
                    {
                        var invoice = await db.ElectronicInvoices
                            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct)
                            ?? throw new InvalidOperationException($"Invoice {invoiceId.Value} not found");

                        var providerId = invoice.CurrentProvider is not null
                            ? invoice.CurrentProvider.Value
                            : "viettel";

                        if (breaker.IsOpen(providerId))
                            throw new InvalidOperationException("Circuit breaker OPEN for provider: " + providerId);

                        var provider = factory.CreateProvider(providerId);

                        var supplierTaxCode = providerId == "viettel"
                            ? builder.Configuration["ViettelConfig:TaxCode"] ?? string.Empty
                            : builder.Configuration["MisaConfig:CompanyCode"] ?? string.Empty;

                        var request  = new EInvoiceRequest(
                            invoice.TenantId, invoice.InvoiceId, invoice.OrderId, invoice.InvoiceType,
                            invoice.Amount, invoice.VatAmount, invoice.TotalAmount,
                            invoice.CustomerName, invoice.CustomerTaxCode, invoice.CustomerAddress,
                            invoice.SubmittedAt ?? DateTime.UtcNow,
                            new Dictionary<string, string>(), supplierTaxCode,
                            invoice.Items.ToList() as IReadOnlyList<VanAn.Shared.Domain.InvoiceItem>,
                            "VND", "CASH");

                        var response = await provider.SubmitInvoiceAsync(request, ct);
                        if (response.Success)
                            breaker.RecordSuccess(providerId);
                        else
                        {
                            breaker.RecordFailure(providerId);
                            throw new InvalidOperationException(response.ErrorMessage);
                        }
                    };

                return new CoreHub.Services.Orchestration.RetryPolicyService(submitAction, logger);
            });
            _ = builder.Services.AddScoped<CoreHub.Services.Orchestration.IComplianceService, CoreHub.Services.Orchestration.ComplianceService>();
            _ = builder.Services.AddScoped<CoreHub.Services.Orchestration.IWebhookService, CoreHub.Services.Orchestration.WebhookService>();
            _ = builder.Services.AddScoped<CoreHub.Services.Orchestration.IHKDRevenueClassificationService, CoreHub.Services.Orchestration.HKDRevenueClassificationService>();
            _ = builder.Services.AddScoped<CoreHub.Infrastructure.Repositories.ITenantProviderConfigurationService, CoreHub.Infrastructure.Repositories.TenantProviderConfigurationService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IProviderManager, CoreHub.Services.ProviderManager>();
            _ = builder.Services.AddScoped<CoreHub.Services.Orchestration.IFallbackService, CoreHub.Services.Orchestration.FallbackService>();
            _ = builder.Services.AddScoped<CoreHub.Services.Orchestration.IEInvoiceOrchestrator, CoreHub.Services.Orchestration.EInvoiceOrchestrator>();

            // W17-T1: Customer Identity services
            _ = builder.Services.AddScoped<VanAn.ShopERP.Services.IOtpService, VanAn.ShopERP.Services.OtpService>();
            _ = builder.Services.AddScoped<VanAn.ShopERP.Services.ICustomerTokenService, VanAn.ShopERP.Services.CustomerTokenService>();

            // Tiered Auth Phase 1: Google OAuth
            _ = builder.Services.AddHttpClient<CoreHub.Services.IGoogleAuthService, CoreHub.Services.GoogleAuthService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            // FIX-BATCH-1: Missing DI registrations (verified unreachable via grep before this fix)
            // C1: QR code generation service (W2 — task card claimed COMPLETE but services never registered)
            // R2-0d: Consolidated — IShopQrCodeService merged into IQrCodeService (CoreHub). Single registration.
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IQrCodeService, VanAn.CoreHub.Services.QrCodeService>();
            // C2: CustomerRecommendationService (W3 — injected into ProductsController primary ctor, would throw at runtime)
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.CustomerRecommendationService>();
            // C3: PushNotificationService (W4 — depends on IPushSubscriptionRepository which was also unregistered here)
            _ = builder.Services.AddScoped<VanAn.CoreHub.Domain.Repositories.IPushSubscriptionRepository, VanAn.CoreHub.Infrastructure.Repositories.PushSubscriptionRepository>();
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.PushNotificationService>();
            // FIX-BATCH-3: IHostedService that subscribes to NATS "order.status.changed" and dispatches to PushNotificationService
            _ = builder.Services.AddHostedService<VanAn.CoreHub.Services.PushNotificationBackgroundService>();

            // Sync: Subscribe to NATS "order.created" events from Gateway → sync to SQLite
            // Without this, ShopERP Owner cannot see orders created via Gateway (KhachLink checkout).
            _ = builder.Services.AddHostedService<VanAn.ShopERP.Services.OrderSyncSubscriber>();

            // Phase 3.5: Subscribe to NATS "order.payment.confirmed" events from Gateway → create accounting entries in SQLite
            // Single source of truth for accounting entries: ShopERP SQLite (not Gateway PG).
            _ = builder.Services.AddHostedService<VanAn.ShopERP.Services.PaymentConfirmedSubscriber>();

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
            // FIX: Cookie-forwarding HttpClient for Blazor pages that call own API endpoints
            // (TenantManagement impersonation). Default HttpClient doesn't forward auth cookies → 302 redirect.
            _ = builder.Services.AddHttpClient("CookieForwarding", (sp, client) =>
            {
                var ctx = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
                if (ctx != null)
                {
                    var cookies = ctx.Request.Headers.Cookie.ToString();
                    if (!string.IsNullOrEmpty(cookies))
                    {
                        client.DefaultRequestHeaders.Add("Cookie", cookies);
                    }
                }
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            _ = builder.Services.AddScoped<CoreHub.Services.INotificationService, CoreHub.Services.CompositeNotificationService>();
            // Product Management (Phase 3): IProductService + IImageStorageService (Cloudinary)
            _ = builder.Services.AddScoped<CoreHub.Services.IProductService, CoreHub.Services.ProductService>();
            _ = builder.Services.AddScoped<CoreHub.Services.IImageStorageService, CoreHub.Services.CloudinaryImageStorageService>();
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
            // Phase 6: Gateway admin API clients for ShopInstances + FeaturedProducts management
            _ = builder.Services.AddScoped<Services.ShopInstanceApiClient>();
            _ = builder.Services.AddScoped<Services.FeaturedProductApiClient>();
            _ = builder.Services.AddScoped<Services.CampaignApiClient>();
            _ = builder.Services.AddScoped<Services.TenantApiClient>();
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
                // Lax (not Strict) — allows cookie to be sent on top-level navigation
                // and cross-subdomain requests (khachvip.online ↔ www.khachvip.online).
                // Strict blocks cookie when navigating from external sites.
                options.Cookie.SameSite = SameSiteMode.Lax;
                // Production: share cookie across khachvip.online and www.khachvip.online.
                // Without Cookie.Domain, the cookie is bound to the exact host that set it,
                // so login on khachvip.online is not recognized on www.khachvip.online (and vice versa).
                // Dev: no domain (localhost single host).
                var cookieDomain = builder.Configuration["Auth:CookieDomain"];
                if (!string.IsNullOrWhiteSpace(cookieDomain))
                {
                    options.Cookie.Domain = cookieDomain;
                }
                // Development: allow cookies over HTTP for local smoke test (no HTTPS cert needed).
                // Production: Always (HTTPS only) — defense in depth.
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
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
                .AddPolicy("OwnerOnly", policy => policy.RequireRole(UserRole.Owner.ToString(), "SystemAdmin"))
                .AddPolicy("StoreManagement", policy => policy.RequireRole(UserRole.Owner.ToString(), UserRole.StoreKeeper.ToString(), "SystemAdmin"))
                .AddPolicy("GuardOnly", policy => policy.RequireRole(UserRole.Guard.ToString()))
                .AddPolicy("StaffOrAbove", policy => policy.RequireRole(UserRole.Staff.ToString(), UserRole.StoreKeeper.ToString(), UserRole.Owner.ToString(), "SystemAdmin"))
                // KitchenAccess: StaffOrAbove + Masterchef (kitchen-only role)
                .AddPolicy("KitchenAccess", policy => policy.RequireRole(UserRole.Staff.ToString(), UserRole.StoreKeeper.ToString(), UserRole.Owner.ToString(), UserRole.Masterchef.ToString(), "SystemAdmin"))
                // Wave 5: SystemAdmin — cross-tenant Tenant CRUD (platform-level admin)
                .AddPolicy("SystemAdmin", policy => policy.RequireRole("SystemAdmin"));

            // Platform SystemAdmin: Register login service
            _ = builder.Services.AddScoped<VanAn.CoreHub.Services.IPlatformUserLoginService, VanAn.CoreHub.Services.PlatformUserLoginService>();

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

            // ✅ FIXED: Add cascading authentication state.
            // AddCascadingAuthenticationState() registers a ROOT cascading value via DI so
            // Task<AuthenticationState> is available to ALL components — including per-page
            // @rendermode InteractiveServer boundaries, which do NOT inherit <CascadingAuthenticationState>
            // wrappers from Routes.razor/MainLayout.razor (those only cover static SSR root).
            // The previous AddScoped<CascadingAuthenticationState>() was wrong: that type is a
            // ComponentBase, not a service, so DI registration did nothing and interactive pages
            // threw "Authorization requires a cascading parameter of type Task<AuthenticationState>".
            _ = builder.Services.AddCascadingAuthenticationState();

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
                // MIGRATE POSTGRESQL FIRST — Gateway (in-process CoreHub) uses PostgreSQL directly.
                // If SQLite migration crashes, PostgreSQL must already be migrated so Gateway works.
                // FAIL-FAST: migration errors are rethrown (don't swallow — data integrity first).
                CoreHub.Infrastructure.IAccountingDbContext accountingContext = scope.ServiceProvider.GetRequiredService<CoreHub.Infrastructure.IAccountingDbContext>();
                if (accountingContext is VanAn.CoreHub.Infrastructure.VanAnDbContext vanAnDb)
                {
                    await vanAnDb.Database.MigrateAsync();
                    Console.WriteLine("PostgreSQL accounting database migrated");
                }

                ShopERPDbContext context = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
                await context.Database.MigrateAsync();
                Console.WriteLine("SQLite database migrated");

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

                // Platform SystemAdmin: Seed PlatformUser (cross-tenant, idempotent)
                // F4: password from configuration with production guard — same pattern as DemoUser seed (L384-387).
                // Hardcoding "VanAn@2026" in production would leave the platform admin password at a known default.
                string sysadminPassword = builder.Configuration["Seed:SysAdminPassword"]
                    ?? (builder.Environment.IsProduction()
                        ? throw new InvalidOperationException("Seed:SysAdminPassword configuration is required in Production.")
                        : "VanAn@2026");
                var platformUserRepo = context.PlatformUsers;
                var existingPlatformAdmin = await platformUserRepo
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username == "sysadmin@vanan.vn");

                if (existingPlatformAdmin == null)
                {
                    var sysadminHash = BCrypt.Net.BCrypt.HashPassword(sysadminPassword, 12);
                    platformUserRepo.Add(new PlatformUser(
                        "sysadmin@vanan.vn",
                        sysadminHash,
                        "System Admin",
                        "sysadmin@vanan.vn"));
                    _ = await context.SaveChangesAsync();
                    Console.WriteLine("Platform SystemAdmin seeded — sysadmin@vanan.vn");
                }

                // Wave 2: Encrypt any pre-existing plaintext PII in dev DB
                if (app.Environment.IsDevelopment())
                {
                    var migrationService = scope.ServiceProvider.GetRequiredService<CoreHub.Services.DataProtection.PiiDataMigrationService>();
                    await migrationService.MigrateAsync();
                }

                // Phase 4: Seed sample Products for KhachLink home page catalog (idempotent — only when empty).
                // Customer-facing home page shows product showcase + recommendations; without seed data
                // the catalog is empty and the page looks broken in dev. Uses the seed tenant id above
                // so products belong to the same tenant as the Owner demo user.
                if (!await context.Products.IgnoreQueryFilters().AnyAsync())
                {
                    // Deterministic GUIDs (lowercase) — match PostgreSQL products exactly.
                    // This prevents GUID case mismatch between SQLite (uppercase) and PG (lowercase)
                    // which caused FK violations and duplicate products on every restart.
                    var seedProducts = new[]
                    {
                        (Id: Guid.Parse("4bda6dc0-a111-48ca-84d8-e8615477814c"), Name: "Cà phê sữa đá", Desc: "Cà phê phin truyền thống, sữa đặc, đá lạnh", Price: 25000m, Cat: "Đồ uống", Cost: 12000m),
                        (Id: Guid.Parse("e817ea26-93d5-42bc-9dc1-8902f02b6e53"), Name: "Cà phê đen đá", Desc: "Cà phê phin truyền thống, đen, đá lạnh", Price: 20000m, Cat: "Đồ uống", Cost: 10000m),
                        (Id: Guid.Parse("55ac278b-4226-49fa-b123-574198759c79"), Name: "Bánh mì thịt nướng", Desc: "Bánh mì nướng than hoa, pate, rau sống, nước sốt", Price: 35000m, Cat: "Đồ ăn", Cost: 18000m),
                        (Id: Guid.Parse("e63cf4c6-71d1-4a0c-9f3e-1e9ac4b31008"), Name: "Phở bò tái", Desc: "Phở bò truyền thống, tái nạc, nước dùng hầm xương", Price: 55000m, Cat: "Đồ ăn", Cost: 30000m),
                        (Id: Guid.Parse("f9ca4bf4-31a0-4631-80d7-86779261908f"), Name: "Trà đào cam sả", Desc: "Trà đen, đào miếng, cam tươi, sả", Price: 40000m, Cat: "Đồ uống", Cost: 18000m),
                        (Id: Guid.Parse("b89bcfc9-343e-4bed-b5b5-56f902f1cd27"), Name: "Cơm gà xối mỡ", Desc: "Cơm sườn, gà xối mỡ hành, đồ chua", Price: 65000m, Cat: "Đồ ăn", Cost: 35000m),
                        (Id: Guid.Parse("05341491-0b92-4ee1-82e8-d7714758bf86"), Name: "Sinh tố bơ", Desc: "Sinh tố bơ tươi, sữa đặc, đá xay", Price: 38000m, Cat: "Đồ uống", Cost: 20000m),
                        (Id: Guid.Parse("5fe7d1c6-1a96-4b33-92fb-8f4baabdfb80"), Name: "Gỏi cuốn tôm", Desc: "Gỏi cuốn tôm tươi, bún, rau sống, nước mắm", Price: 45000m, Cat: "Đồ ăn", Cost: 25000m),
                        (Id: Guid.Parse("2e6f1234-e70f-46b9-aad1-97ef8c854d1e"), Name: "Bánh flan caramel", Desc: "Bánh flan mềm, caramel đậm vị", Price: 28000m, Cat: "Tráng miệng", Cost: 12000m),
                    };
                    foreach (var sp in seedProducts)
                    {
                        var p = new Product(seedTenantId, sp.Name, sp.Desc, sp.Price, sp.Cat, true, null, 0.08m, sp.Cost);
                        typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(p, sp.Id);
                        typeof(Product).GetProperty("ProductId")!.SetValue(p, new ProductId(sp.Id));
                        context.Products.Add(p);
                    }
                    _ = await context.SaveChangesAsync();
                }

                // NOTE: SINGLE-IDENTITY data alignment is no longer needed here.
                // The migration drops BusinessKey columns. Data alignment for Product.Id
                // was already done by the previous DMD-FK1 fix (before this refactor).
                // New entities created after this refactor have Id = BusinessKey by constructor.

                // Seed default dev tenant into Tenants table (HKD Group 1 — quán cafe mẫu)
                // FIX: Tenant entity's TenantId (from BaseEntity) must equal its own Id for the
                // global multi-tenancy query filter to find it. Factory methods don't set TenantId,
                // so we set it via EF Core Entry API after tracking.
                if (!await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == seedTenantId))
                {
                    var devTenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateHouseholdBusiness(
                        seedTenantId, "Vạn An Cafe (HKD Group 1)", VanAn.Shared.Domain.HKDGroup.Group1);
                    context.Tenants.Add(devTenant);
                    // Set TenantId = own Id (multi-tenancy discriminator for self-reference)
                    context.Entry(devTenant).Property("TenantId").CurrentValue = seedTenantId;
                    await context.SaveChangesAsync();
                    Console.WriteLine($"Default tenant seeded into Tenants table — {tenantIdStr}");
                }

                // VAS Wave 1: Seed Enterprise tenant into SQLite (business DB) for feature flag routing.
                // VasFeatureFlagService queries IVanAnDbContext → ShopERPDbContext (SQLite), so the tenant
                // must exist here with Type=Enterprise_SME. Accounting data is seeded separately into PostgreSQL.
                var vasTenantId = new Guid("a5b6c7d8-1234-5678-9abc-def012345678");
                if (!await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == new TenantId(vasTenantId)))
                {
                    var vasSettings = new VanAn.Shared.Domain.Aggregates.TenantAggregate.TenantSettings("contact@vanan-enterprise.vn", "028-1234-5678", "123 Le Loi, Q.1, TP.HCM", taxCode: "0301234567");
                    var vasTenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(
                        new TenantId(vasTenantId), "Vạn An Trading Co. (DN vừa TT 133)", vasSettings);
                    vasTenant.SetTenantType(VanAn.Shared.Domain.TenantType.Enterprise_SME, VanAn.Shared.Domain.AccountingStandard.TT133_2016);
                    context.Tenants.Add(vasTenant);
                    // Set TenantId = own Id (multi-tenancy discriminator for self-reference)
                    context.Entry(vasTenant).Property("TenantId").CurrentValue = new TenantId(vasTenantId);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"VAS W1: Enterprise tenant seeded into SQLite — {vasTenantId}");
                }

                // KhachLink Full Flow W0: Seed default shop feature settings for default tenant.
                // Defaults: kitchen=ON, loyalty=ON, accounting=ON, QR_table=OFF, voice=OFF, einvoice=OFF.
                if (!await context.ShopFeatureSettings.IgnoreQueryFilters().AnyAsync(s => s.TenantId == seedTenantId))
                {
                    var featureSettings = new CoreHub.Infrastructure.Entities.ShopFeatureSettingsEntity(seedTenantId);
                    context.ShopFeatureSettings.Add(featureSettings);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"KL W0: Shop feature settings seeded for tenant {tenantIdStr}");
                }

                // W3: Seed AccountChart reference data (clear + reseed to ensure chart matches code).
                // Reference data is NOT user-editable — clear+reseed propagates label fixes + account additions/removals.
                // AccountCharts has no FK dependencies, safe to clear before HTTP requests start.
                // PostgreSQL migration already ran above (before SQLite migration).
                await CoreHub.Infrastructure.Seed.AccountChartSeeder.CleanupAsync(accountingContext);
                int accountChartCount = await CoreHub.Infrastructure.Seed.AccountChartSeeder.SeedAsync(accountingContext);
                Console.WriteLine($"W3: AccountChart reference data seeded — {accountChartCount} accounts across 2 standards (TT 133 + TT 99)");

                // Seed tenants into PostgreSQL (shared with Gateway via VanAnDbContext).
                // Gateway's HKDBookGenerationService queries Tenants table in PostgreSQL — without these rows,
                // GET /api/hkd-books returns 500 "Tenant not found" because tenants only exist in SQLite.
                if (accountingContext is VanAn.CoreHub.Infrastructure.VanAnDbContext vanAnDbForSeed)
                {
                    // Default HKD tenant — Factory method SetTenantId(id) makes TenantId = own Id
                    // (self-referential for multi-tenancy query filter).
                    bool hkdTenantExists = await vanAnDbForSeed.Tenants
                        .IgnoreQueryFilters()
                        .AnyAsync(t => t.Id == seedTenantId);
                    if (!hkdTenantExists)
                    {
                        var hkdTenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateHouseholdBusiness(
                            seedTenantId, "Vạn An Cafe (HKD Group 1)", VanAn.Shared.Domain.HKDGroup.Group1);
                        vanAnDbForSeed.Tenants.Add(hkdTenant);
                        await vanAnDbForSeed.SaveChangesAsync();
                        Console.WriteLine($"PostgreSQL: Default HKD tenant seeded — {seedTenantId}");
                    }

                    // Sync products from SQLite → PostgreSQL (shared with Gateway via VanAnDbContext).
                    // Gateway's PublicOrdersController.CreateCheckoutOrder creates orders in PostgreSQL,
                    // and OrderItems has FK constraint: OrderItems.ProductId → Products.Id (PK).
                    // Without matching products in PostgreSQL, checkout returns 500 (FK violation).
                    //
                    // IMPORTANT: FK maps OrderItems.ProductId → Products.Id (PK), NOT Products.ProductId.
                    // So we must set Products.Id = SQLite's ProductId value, so that when checkout sends
                    // ProductId (catalog ID), it matches Products.Id (PK) in PostgreSQL.
                    // Also override Products.ProductId to match SQLite for consistency.
                    // SINGLE-IDENTITY: ProductId column dropped by migration. No stale cleanup needed
                    // (Id is sole identity). Just seed products from SQLite if missing in PostgreSQL.
                    var sqliteProducts = await context.Products
                        .IgnoreQueryFilters()
                        .Where(p => p.TenantId == seedTenantId)
                        .ToListAsync();
                    int pgProductCount = 0;
                    foreach (var sqliteProd in sqliteProducts)
                    {
                        // SINGLE-IDENTITY: Check by Id (PK) first — most reliable.
                        // Also check by Name + TenantId as fallback (case-insensitive).
                        // Both checks needed: product may exist with same Id but different Name
                        // (data drift), or same Name but different Id (re-seed with new GUID).
                        bool pgProdExists = await vanAnDbForSeed.Products
                            .IgnoreQueryFilters()
                            .AnyAsync(p => p.Id == sqliteProd.Id
                                || (p.TenantId == sqliteProd.TenantId && p.Name == sqliteProd.Name));
                        if (!pgProdExists)
                        {
                            var pgProd = new Product(
                                sqliteProd.TenantId, sqliteProd.Name, sqliteProd.Description,
                                sqliteProd.Price, sqliteProd.Category, sqliteProd.IsActive,
                                sqliteProd.ImageUrl, sqliteProd.VatRate, sqliteProd.CostPrice);
                            // SINGLE-IDENTITY: Override Id (PK) = SQLite's Id, so FK_OrderItems_Products_ProductId
                            // (which references Products.Id) matches the ProductId sent by checkout.
                            // ProductId VO is synced to Id in constructor (Id = ProductId.Value).
                            typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty("Id")!
                                .SetValue(pgProd, sqliteProd.Id);
                            typeof(Product).GetProperty("ProductId")!.SetValue(pgProd, new ProductId(sqliteProd.Id));
                            vanAnDbForSeed.Products.Add(pgProd);
                            pgProductCount++;
                        }
                    }
                    if (pgProductCount > 0)
                    {
                        await vanAnDbForSeed.SaveChangesAsync();
                        Console.WriteLine($"PostgreSQL: Products synced from SQLite — {pgProductCount} items for tenant {seedTenantId}");
                    }

                    // VAS Wave 1: Seed Enterprise tenant + sample data for VAS report testing (idempotent)
                    var vasSeedResult = await CoreHub.Infrastructure.Seed.VasSampleDataSeeder.SeedAsync(vanAnDbForSeed);
                    if (vasSeedResult.Skipped)
                        Console.WriteLine("VAS W1: Enterprise tenant already seeded, skipping");
                    else
                        Console.WriteLine($"VAS W1: Seed complete — {vasSeedResult.JournalEntries} journals, {vasSeedResult.AccountingEntries} accounting entries");
                }
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
            // NOTE: GET /dev/login is handled by DevLoginController.LoginInfo() — do NOT register
            // a duplicate minimal API MapGet here (causes AmbiguousMatchException on GET).
#if DEBUG
            if (app.Environment.IsDevelopment())
            {
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

            _ = app.MapRazorPages(); // Login.cshtml, Logout.cshtml, Guard/Scan.cshtml
            _ = app.MapRazorComponents<Components.App>()
                .AddInteractiveServerRenderMode();
            // Blazor is the main app — unmatched routes fall through to Blazor App.razor
            // (which renders NotFound or redirects to /sitemap via Home.razor)

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
