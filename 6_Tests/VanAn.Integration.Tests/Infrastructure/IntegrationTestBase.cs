using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain.Common;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests
/// Uses SQLite in-memory with persistent connection for real relational behavior
/// (transactions, FK constraints, rollback support)
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    public readonly IServiceProvider _serviceProvider;
    public readonly VanAnDbContext _dbContext;
    public readonly ILogger<IntegrationTestBase> _logger;

    // Tests MUST use this TenantId — multi-tenancy query filter blocks data with different TenantId
    public static readonly TenantId TestTenantId = new TenantId(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    protected IntegrationTestBase()
    {
        // SQLite in-memory: connection stays open for test lifetime
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Wave 2: Data Protection for PII field-level encryption
        string keyDirectory = Path.Combine(Path.GetTempPath(), $"vanan-test-keys-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(keyDirectory);
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
            .SetApplicationName("VanAnTest");

        // Add SQLite in-memory database for testing (real relational provider)
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite(_connection));

        // Register IVanAnDbContext and ITenantProvider (required by repositories)
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        // WAVE 3: IAccountingDbContext → VanAnDbContext (implements both interfaces, has accounting DbSets)
        services.AddScoped<IAccountingDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        services.AddScoped<ITenantProvider, TestTenantProvider>();
        services.AddHttpContextAccessor();

        // Add repository registrations
        services.AddScoped<IAccountingEntryRepository, AccountingEntryRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISocialCampaignRepository, SocialCampaignRepository>();
        services.AddScoped<ILoyaltyRewardsRepository, LoyaltyRewardsRepository>();

        // Add core services (F4 — Real implementations, no stubs)
        services.AddScoped<IAccountingService, AccountingEntryService>();
        services.AddScoped<IAccountingEntryService, AccountingEntryServiceStub>(); // Keep for backward compat
        services.AddScoped<IAuditTrailService, AuditTrailService>();

        // Add E-Invoice orchestration services (F4 — Real implementations)
        services.AddScoped<IComplianceService, ComplianceService>();
        services.AddScoped<IInvoicePolicyService, InvoicePolicyService>();
        services.AddScoped<IHKDRevenueClassificationService, HKDRevenueClassificationService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        // Add lead management services for lead conversion tests
        services.AddScoped<VanAn.CoreHub.Services.ILeadManagementService, VanAn.CoreHub.Services.LeadManagementService>();
        services.AddScoped<VanAn.CoreHub.Services.ILeadConversionService, VanAn.CoreHub.Services.LeadConversionService>();
        services.AddScoped<VanAn.CoreHub.Services.IFacebookLeadService, VanAn.CoreHub.Services.FacebookLeadService>();
        services.AddScoped<VanAn.CoreHub.Services.ICustomerOnboardingService, VanAn.CoreHub.Services.CustomerOnboardingService>();
        services.AddScoped<VanAn.CoreHub.Services.ILoyaltyRewardsService, VanAn.CoreHub.Services.LoyaltyRewardsService>();

        // Notification services (required by CompositeNotificationService)
        services.AddHttpClient(); // Required by BrevoEmailService
        services.AddScoped<CoreHub.Services.IEmailService, CoreHub.Services.BrevoEmailService>();
        services.AddScoped<CoreHub.Services.ISmsService, CoreHub.Services.EsmsNotificationService>();
        services.AddScoped<CoreHub.Services.INotificationService, CoreHub.Services.CompositeNotificationService>();

        // FIX: Session 2 - Apply pattern from ShopERP/Program.cs for missing services
        services.AddScoped<Shared.Services.IOrderWorkflowService, CoreHub.Services.OrderWorkflowService>();
        services.AddScoped<CoreHub.Repositories.ISystemMetricsRepository, CoreHub.Infrastructure.Repositories.SystemMetricsRepository>();
        services.AddScoped<CoreHub.Services.IDashboardService, CoreHub.Services.DashboardService>();
        services.AddScoped<CoreHub.Services.IReversalService, CoreHub.Services.ReversalService>();
        services.AddScoped<CoreHub.Services.IPeriodClosingService, CoreHub.Services.PeriodClosingService>();

        // FIX: IConfiguration required by DashboardService
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Information"
            })
            .Build());

        _serviceProvider = services.BuildServiceProvider();

        // Wave 2: Initialize DataProtection provider for EF Core PII encryption
        DataProtectionProviderAccessor.Initialize(_serviceProvider.GetRequiredService<IDataProtectionProvider>());

        _dbContext = _serviceProvider.GetRequiredService<VanAnDbContext>();
        _logger = _serviceProvider.GetRequiredService<ILogger<IntegrationTestBase>>();

        // Ensure database schema is created
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _connection?.Dispose();
    }

    public T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Creates a new service provider scope with fresh DbContext instance
    /// Used for testing app restart scenarios (dual-DbContext pattern)
    /// </summary>
    protected IServiceScope CreateNewScope()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add SQLite in-memory database (same connection for test lifetime)
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite(_connection));

        // Register IVanAnDbContext and ITenantProvider
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        // WAVE 3: IAccountingDbContext → VanAnDbContext (implements both interfaces, has accounting DbSets)
        services.AddScoped<IAccountingDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        services.AddScoped<ITenantProvider, TestTenantProvider>();
        services.AddHttpContextAccessor();

        // Add repository registrations
        services.AddScoped<IAccountingEntryRepository, AccountingEntryRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISocialCampaignRepository, SocialCampaignRepository>();
        services.AddScoped<ILoyaltyRewardsRepository, LoyaltyRewardsRepository>();

        // Add core services
        services.AddScoped<IAccountingService, AccountingEntryService>();
        services.AddScoped<IAccountingEntryService, AccountingEntryServiceStub>();
        services.AddScoped<IAuditTrailService, AuditTrailService>();

        // Add E-Invoice orchestration services
        services.AddScoped<IComplianceService, ComplianceService>();
        services.AddScoped<IInvoicePolicyService, InvoicePolicyService>();
        services.AddScoped<IHKDRevenueClassificationService, HKDRevenueClassificationService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        // Add lead management services
        services.AddScoped<VanAn.CoreHub.Services.ILeadManagementService, VanAn.CoreHub.Services.LeadManagementService>();
        services.AddScoped<VanAn.CoreHub.Services.ILeadConversionService, VanAn.CoreHub.Services.LeadConversionService>();
        services.AddScoped<VanAn.CoreHub.Services.IFacebookLeadService, VanAn.CoreHub.Services.FacebookLeadService>();
        services.AddScoped<VanAn.CoreHub.Services.ICustomerOnboardingService, VanAn.CoreHub.Services.CustomerOnboardingService>();
        services.AddScoped<VanAn.CoreHub.Services.ILoyaltyRewardsService, VanAn.CoreHub.Services.LoyaltyRewardsService>();
        
        // Notification services (required by CompositeNotificationService)
        services.AddHttpClient(); // Required by BrevoEmailService
        services.AddScoped<CoreHub.Services.IEmailService, CoreHub.Services.BrevoEmailService>();
        services.AddScoped<CoreHub.Services.ISmsService, CoreHub.Services.EsmsNotificationService>();
        services.AddScoped<CoreHub.Services.INotificationService, CoreHub.Services.CompositeNotificationService>();

        // FIX: Session 2 - Apply pattern from ShopERP/Program.cs for missing services
        services.AddScoped<Shared.Services.IOrderWorkflowService, CoreHub.Services.OrderWorkflowService>();
        services.AddScoped<CoreHub.Repositories.ISystemMetricsRepository, CoreHub.Infrastructure.Repositories.SystemMetricsRepository>();
        services.AddScoped<CoreHub.Services.IDashboardService, CoreHub.Services.DashboardService>();
        services.AddScoped<CoreHub.Services.IReversalService, CoreHub.Services.ReversalService>();
        services.AddScoped<CoreHub.Services.IPeriodClosingService, CoreHub.Services.PeriodClosingService>();

        // FIX: IConfiguration required by DashboardService
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Information"
            })
            .Build());

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        return serviceProvider.CreateScope();
    }
}
