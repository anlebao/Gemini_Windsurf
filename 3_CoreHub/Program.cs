// Build: 2026-06-17 (OutputType=Exe fix)
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services.Events;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Hubs;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.CoreHub.Services.Resilience;
using VanAn.CoreHub.Infrastructure.ProjectMemory;
using VanAn.CoreHub.Infrastructure.SemanticSearch;
using VanAn.CoreHub.Infrastructure.SemanticSearch.Services;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Agents;
using VanAn.CoreHub.Services.Providers.EInvoice;
using VanAn.CoreHub.Services.Formula;
using VanAn.CoreHub.Services.Data;
using VanAn.CoreHub.Services.PreAggregation;
using VanAn.CoreHub.Services.Cache;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace VanAn.CoreHub
{
    /// <summary>
    /// CoreHub Service Host for background processing
    /// Handles accounting events and HKD book generation
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Wave 3: EPPlus NonCommercial license context for Excel export
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            IHost host = CreateHostBuilder(args).Build();

            // Wave 2: Initialize DataProtection provider for EF Core PII encryption
            DataProtectionProviderAccessor.Initialize(host.Services.GetRequiredService<IDataProtectionProvider>());

            // Apply EF Core migrations (Stream E: replaced EnsureCreatedAsync with MigrateAsync for production-safe schema management)
            using (IServiceScope scope = host.Services.CreateScope())
            {
                VanAnDbContext context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
                await context.Database.MigrateAsync();

                // Phase 6: Project Memory migrations - Development only
                var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
                if (env.IsDevelopment())
                {
                    ProjectMemoryDbContext memoryContext = scope.ServiceProvider.GetRequiredService<ProjectMemoryDbContext>();
                    await memoryContext.Database.MigrateAsync();

                    // Wave 2: Encrypt any pre-existing plaintext PII in dev DB
                    var migrationService = scope.ServiceProvider.GetRequiredService<Services.DataProtection.PiiDataMigrationService>();
                    await migrationService.MigrateAsync();
                }
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Database configuration
                    string connectionString = context.Configuration.GetSection("ConnectionStrings")["DefaultConnection"]
                        ?? (context.HostingEnvironment.IsProduction()
                            ? throw new InvalidOperationException("ConnectionStrings:DefaultConnection configuration is required in Production.")
                            : "Host=localhost;Database=VanAnCoreHub;Username=vanan_admin;Password=VanAn@2024!");

                    _ = services.AddDbContext<VanAnDbContext>(options =>
                        options.UseNpgsql(connectionString));

                    // Wave 2: Data Protection for PII field-level encryption
                    string keyDirectory = context.Configuration.GetSection("DataProtection")["KeyDirectory"]
                        ?? Path.Combine(AppContext.BaseDirectory, "keys", "corehub");
                    _ = Directory.CreateDirectory(keyDirectory);
                    _ = services.AddDataProtection()
                        .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
                        .SetApplicationName(context.Configuration.GetSection("DataProtection")["ApplicationName"] ?? "VanAnCoreHub");

                    // Wave 2: PII data migration service
                    _ = services.AddScoped<Services.DataProtection.PiiDataMigrationService>();

                    // Repository layer
                    _ = services.AddScoped<IAccountingEntryRepository, AccountingEntryRepository>();
                    _ = services.AddScoped<IJournalTemplateRepository, JournalTemplateRepository>();
                    _ = services.AddScoped<IOrderRepository, OrderRepository>();
                    _ = services.AddScoped<IProductRepository, ProductRepository>();
                    _ = services.AddScoped<ICustomerRepository, CustomerRepository>();
                    _ = services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
                    _ = services.AddScoped<IHKDBookRepository, HKDBookRepository>();
                    _ = services.AddScoped<IAuditLogRepository, AuditLogRepository>();

                    // Core services
                    _ = services.AddScoped<IAccountingService, AccountingEntryService>();
                    _ = services.AddScoped<IHKDBookService, HKDBookService>();
                    _ = services.AddScoped<IOrderService, OrderService>();
                    _ = services.AddScoped<IAuditTrailService, AuditTrailService>();
                    // Sprint C: Period Closing guard (required by AccountingEntryService)
                    _ = services.AddScoped<IReversalService, ReversalService>();
                    _ = services.AddScoped<IPeriodClosingService, PeriodClosingService>();

                    // Background task queue
                    _ = services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
                    _ = services.AddHostedService<OrderQueueService>();

                    // Enhanced order services
                    _ = services.AddScoped<IOrderQueueService, OrderQueueService>();

                    // SignalR
                    _ = services.AddSignalR();

                    // Template factory (if not already registered)
                    _ = services.AddScoped<ITemplateFactory, TemplateFactory>();

                    // Wave 3: HKD Book calc engine DI wiring (unblocks Wave 4 routing)
                    // Dependency order: IFormulaEngine -> IPreAggregationService -> IDataProvider
                    // -> IBookResultCache -> TemplateFactory (concrete) -> IHKDBookGenerationService
                    // Note: New TemplateFactory (Services.Template namespace) registered as concrete,
                    // NOT as ITemplateFactory — preserves old TemplateFactory for OrderService (W0-T8 decision).
                    // Wave 7: Lazy<IFormulaEngine> breaks circular dependency (FormulaEngine -> DataProvider
                    // -> PreAggregation -> FormulaEngine). SmartPreAggregationService uses Lazy<IFormulaEngine>.
                    _ = services.AddScoped<Lazy<IFormulaEngine>>(sp => new Lazy<IFormulaEngine>(() => sp.GetRequiredService<IFormulaEngine>()));
                    _ = services.AddScoped<IFormulaEngine, ProductionFormulaEngine>();
                    _ = services.AddScoped<IPreAggregationService, SmartPreAggregationService>();
                    _ = services.AddScoped<IDataProvider, ScopedDataProvider>();
                    _ = services.AddScoped<IBookResultCache, BookResultCache>();
                    _ = services.AddScoped<VanAn.CoreHub.Services.Template.TemplateFactory>();
                    _ = services.AddScoped<VanAn.CoreHub.Services.Template.IHKDBookGenerationService, VanAn.CoreHub.Services.Template.HKDBookGenerationService>();

                    // Order hub
                    _ = services.AddScoped<OrderHub>();

                    // Event handling services
                    _ = services.AddHostedService<SimpleAccountingEventHandler>();

                    // E-Invoice Services (Sprint 3 â€” R4 DI wiring)
                    _ = services.AddMemoryCache();
                    _ = services.AddScoped<IOutboxRepository, OutboxRepository>();
                    _ = services.AddScoped<IInvoicePolicyService, InvoicePolicyService>();
                    // Viettel provider - named HttpClient + config
                    _ = services.Configure<ViettelConfig>(context.Configuration.GetSection("ViettelConfig"));
                    _ = services.AddHttpClient<ViettelEInvoiceProvider>("viettel", client =>
                    {
                        client.BaseAddress = new Uri(
                            context.Configuration["ViettelConfig:BaseUrl"] ?? "https://sinvoice.viettel.vn/");
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });

                    // MISA provider - named HttpClient + config
                    _ = services.Configure<MisaConfig>(context.Configuration.GetSection("MisaConfig"));
                    _ = services.AddHttpClient<MisaEInvoiceProvider>("misa", client =>
                    {
                        client.BaseAddress = new Uri(
                            context.Configuration["MisaConfig:BaseUrl"] ?? "https://api.meinvoice.vn/");
                        client.Timeout = TimeSpan.FromSeconds(45);
                    });

                    // Provider registry (Singleton)
                    _ = services.AddSingleton<IEInvoiceProviderRegistry>(sp =>
                    {
                        var registry = new EInvoiceProviderRegistry();
                        registry.RegisterProvider("viettel", typeof(ViettelEInvoiceProvider));
                        registry.RegisterProvider("misa",    typeof(MisaEInvoiceProvider));
                        return registry;
                    });
                    _ = services.AddScoped<IEInvoiceProviderFactory, EInvoiceProviderFactory>();

                    // RetryPolicyService - Fix TODO(F4): wire submitAction to real provider
                    // Safe: Scoped shares VanAnDbContext lifetime within same request scope
                    _ = services.AddScoped<IRetryPolicyService>(sp =>
                    {
                        var factory = sp.GetRequiredService<IEInvoiceProviderFactory>();
                        var breaker = sp.GetRequiredService<ICircuitBreakerService>();
                        var db      = sp.GetRequiredService<VanAnDbContext>();
                        var logger  = sp.GetRequiredService<ILogger<RetryPolicyService>>();

                        Func<VanAn.Shared.Domain.ElectronicInvoiceId, CancellationToken, Task> submitAction =
                            async (invoiceId, ct) =>
                            {
                                var invoice = await db.ElectronicInvoices
                                    .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct)
                                    ?? throw new InvalidOperationException(
                                        $"Invoice {invoiceId.Value} not found");

                                // Null-safe: CurrentProvider is ProviderId? (nullable)
                                var providerId = invoice.CurrentProvider is not null
                                    ? invoice.CurrentProvider.Value
                                    : "viettel";

                                if (breaker.IsOpen(providerId))
                                    throw new InvalidOperationException(
                                        "Circuit breaker OPEN for provider: " + providerId);

                                var provider = factory.CreateProvider(providerId);

                                // W6-T3: SupplierTaxCode from provider config, LineItems from invoice,
                                // CurrencyCode="VND", PaymentType default "CASH" (no PaymentType field on invoice entity).
                                var supplierTaxCode = providerId == "viettel"
                                    ? context.Configuration["ViettelConfig:TaxCode"] ?? string.Empty
                                    : context.Configuration["MisaConfig:CompanyCode"] ?? string.Empty;

                                var request  = new EInvoiceRequest(
                                    invoice.TenantId,
                                    invoice.InvoiceId,
                                    invoice.OrderId,
                                    invoice.InvoiceType,
                                    invoice.Amount,
                                    invoice.VatAmount,
                                    invoice.TotalAmount,
                                    invoice.CustomerName,
                                    invoice.CustomerTaxCode,
                                    invoice.CustomerAddress,
                                    invoice.SubmittedAt ?? DateTime.UtcNow,
                                    new Dictionary<string, string>(),
                                    supplierTaxCode,
                                    invoice.Items.ToList() as IReadOnlyList<VanAn.Shared.Domain.InvoiceItem>,
                                    "VND",
                                    "CASH");

                                var response = await provider.SubmitInvoiceAsync(request, ct);

                                if (response.Success)
                                    breaker.RecordSuccess(providerId);
                                else
                                {
                                    breaker.RecordFailure(providerId);
                                    throw new InvalidOperationException(response.ErrorMessage);
                                }
                            };

                        return new RetryPolicyService(submitAction, logger);
                    });
                    _ = services.AddScoped<IComplianceService, ComplianceService>();
                    _ = services.AddScoped<IWebhookService, WebhookService>();
                    _ = services.AddScoped<IHKDRevenueClassificationService, HKDRevenueClassificationService>();
                    _ = services.AddScoped<ITenantProviderConfigurationService, TenantProviderConfigurationService>();
                    _ = services.AddScoped<IProviderManager, ProviderManager>();
                    _ = services.AddScoped<IFallbackService, FallbackService>();
                    _ = services.AddScoped<IEInvoiceOrchestrator, EInvoiceOrchestrator>();
                    _ = services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
                    _ = services.AddHostedService<EInvoiceWorker>();

                    // UC1: QR Checkout Completion services (TODO: Sprint 3 incomplete - commented out for Phase 6 migration)
                    // _ = services.AddScoped<ICustomerRepository, CustomerRepository>();
                    // _ = services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();
                    // _ = services.AddScoped<IGuestMergeService, GuestMergeService>();
                    // _ = services.AddScoped<ICheckoutCompletionService, CheckoutCompletionService>();
                    _ = services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
                    // _ = services.AddHttpClient<IMstLookupService, MstLookupService>("VietQR", client =>
                    // {
                    //     client.BaseAddress = new Uri("https://api.vietqr.io/v2/");
                    //     client.Timeout = TimeSpan.FromSeconds(3);
                    // });
                    _ = services.AddHostedService<BatchInvoiceProcessor>();

                    // Phase 6: Project Memory (PostgreSQL with SQLite fallback)
                    var dbProvider = context.Configuration["ProjectMemory:DatabaseProvider"] ?? "PostgreSQL";
                    var projectMemoryConnectionString = context.Configuration["ProjectMemory:ConnectionString"]
                        ?? (context.HostingEnvironment.IsProduction()
                            ? throw new InvalidOperationException("ProjectMemory:ConnectionString configuration is required in Production.")
                            : "Host=localhost;Port=5432;Database=vanan_project_memory;Username=vanan;Password=VanAn@2024!");

                    if (dbProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
                    {
                        services.AddDbContext<ProjectMemoryDbContext>(options =>
                            options.UseSqlite(projectMemoryConnectionString));
                    }
                    else
                    {
                        // PostgreSQL registration with explicit CEI flag
                        services.AddDbContext<ProjectMemoryDbContext>(options =>
                            options.UseNpgsql(projectMemoryConnectionString));

                        // Override constructor to inject usePostgresFeatures: true (CEI Standard)
                        services.AddScoped<ProjectMemoryDbContext>(sp =>
                        {
                            var options = sp.GetRequiredService<DbContextOptions<ProjectMemoryDbContext>>();
                            return new ProjectMemoryDbContext(options, usePostgresFeatures: true);
                        });
                    }

                    services.AddScoped<IProjectMemoryService, ProjectMemoryService>();

                    // Phase 6: Project Memory Health Check
                    services.AddHealthChecks()
                        .AddCheck<ProjectMemoryHealthCheck>("project-memory");

                    // Phase 6: Agent Executors
                    services.AddScoped<FeatureDeveloperExecutor>();
                    services.AddScoped<BuildFixerExecutor>();

                    // Phase 6: Project Memory Cleanup Service
                    services.Configure<ProjectMemoryCleanupOptions>(
                        context.Configuration.GetSection("ProjectMemoryCleanup"));
                    services.AddHostedService<ProjectMemoryCleanupService>();

                    // Phase 7: Semantic Search
                    string semanticSearchConnection = context.Configuration.GetSection("ConnectionStrings")["SemanticSearch"]
                        ?? "Data Source=semantic_search.db";
                    _ = services.AddSingleton<IVectorStore>(sp =>
                        new SqliteVectorStore(semanticSearchConnection, sp.GetRequiredService<ILogger<SqliteVectorStore>>()));
                    _ = services.AddSingleton<IEmbeddingService>(sp =>
                        new LocalEmbeddingService(sp.GetRequiredService<ILogger<LocalEmbeddingService>>()));
                    _ = services.AddSingleton<ISemanticSearchService, SemanticSearchService>();
                    _ = services.AddSingleton<IndexingPipeline>();

                    // Wave 0: JWT Authentication Foundation
                    _ = services.AddScoped<IJwtTokenService, JwtTokenService>();

                    // Wave 3: Excel Export Service
                    _ = services.AddScoped<IExcelExportService, ExcelExportService>();

                    // Logging
                    _ = services.AddLogging(builder => builder.AddConsole());
                });
        }
    }
}
