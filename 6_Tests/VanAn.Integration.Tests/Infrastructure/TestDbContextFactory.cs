using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Tests.TestInfrastructure;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Test DbContext Factory for Integration Tests
/// Provides database context for testing scenarios
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a new VanAnDbContext instance for testing
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <returns>VanAnDbContext instance</returns>
    public static VanAnDbContext Create(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddDbContext<VanAnDbContext>(options =>
            options.UseNpgsql(connectionString)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors());

        services.AddScoped<ITenantProvider, TestTenantProvider>();

        var provider = services.BuildServiceProvider();

        var scope = provider.CreateScope();

        return scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
    }

    /// <summary>
    /// Creates an in-memory VanAnDbContext for unit testing
    /// </summary>
    /// <param name="databaseName">Unique database name</param>
    /// <returns>VanAnDbContext instance</returns>
    public static VanAnDbContext CreateInMemory(string databaseName)
    {
        var services = new ServiceCollection();

        services.AddDbContext<VanAnDbContext>(options =>
            options.UseInMemoryDatabase(databaseName)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors());

        services.AddScoped<ITenantProvider, TestTenantProvider>();

        var provider = services.BuildServiceProvider();

        var scope = provider.CreateScope();

        return scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
    }
}
