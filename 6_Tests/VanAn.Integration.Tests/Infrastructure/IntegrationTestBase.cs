using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests
/// Provides common setup and teardown for database operations
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly IServiceProvider _serviceProvider;
    protected readonly VanAnDbContext _dbContext;
    protected readonly ILogger<IntegrationTestBase> _logger;

    protected IntegrationTestBase()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add in-memory database for testing
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        
        // Add services
        // Note: Service registrations will be added as needed
        
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<VanAnDbContext>();
        _logger = _serviceProvider.GetRequiredService<ILogger<IntegrationTestBase>>();
        
        // Ensure database is created
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext?.Database?.EnsureDeleted();
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}
