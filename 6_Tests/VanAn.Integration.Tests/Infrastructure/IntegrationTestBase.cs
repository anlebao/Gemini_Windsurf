using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
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

        // Add SQLite in-memory database for testing (real relational provider)
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite(_connection));

        // Register IVanAnDbContext and ITenantProvider (required by repositories)
        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        services.AddScoped<ITenantProvider, TestTenantProvider>();

        // Add repository registrations
        services.AddScoped<IAccountingEntryRepository, AccountingEntryRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

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

        _serviceProvider = services.BuildServiceProvider();
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
}
