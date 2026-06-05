using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests
/// Provides common setup and teardown for database operations
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    public readonly IServiceProvider _serviceProvider;
    public readonly VanAnDbContext _dbContext;
    public readonly ILogger<IntegrationTestBase> _logger;

    protected IntegrationTestBase()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add in-memory database for testing
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Add accounting services for Sprint 1 tests
        services.AddScoped<IAccountingEntryService, AccountingEntryServiceStub>();

        // Add lead management services for lead conversion tests
        services.AddScoped<VanAn.CoreHub.Services.ILeadManagementService, VanAn.CoreHub.Services.LeadManagementService>();
        services.AddScoped<VanAn.CoreHub.Services.ILeadConversionService, VanAn.CoreHub.Services.LeadConversionService>();
        services.AddScoped<VanAn.CoreHub.Services.IFacebookLeadService, VanAn.CoreHub.Services.FacebookLeadService>();
        services.AddScoped<VanAn.CoreHub.Services.ICustomerOnboardingService, VanAn.CoreHub.Services.CustomerOnboardingService>();
        services.AddScoped<VanAn.CoreHub.Services.ILoyaltyRewardsService, VanAn.CoreHub.Services.LoyaltyRewardsService>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<VanAnDbContext>();
        _logger = _serviceProvider.GetRequiredService<ILogger<IntegrationTestBase>>();

        // Ensure database is created
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        // IServiceProvider doesn't have Dispose method in .NET 8
        // Only dispose DbContext
        _dbContext?.Dispose();
    }

    public T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}
