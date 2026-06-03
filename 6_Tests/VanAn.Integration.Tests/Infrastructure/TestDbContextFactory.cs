using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Common;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Wrapper to properly dispose ServiceProvider and scope
/// </summary>
public sealed class TestDbContextWrapper : IDisposable
{
    private readonly IServiceProvider _provider;
    private readonly IServiceScope _scope;

    public VanAnDbContext Context { get; }

    public TestDbContextWrapper(IServiceProvider provider, IServiceScope scope, VanAnDbContext context)
    {
        _provider = provider;
        _scope = scope;
        Context = context;
    }

    public void Dispose()
    {
        Context?.Dispose();
        _scope?.Dispose();
        if (_provider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }
    }
}

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
    /// <returns>TestDbContextWrapper that properly disposes resources</returns>
    public static TestDbContextWrapper Create(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddDbContext<VanAnDbContext>(options =>
            options.UseNpgsql(connectionString)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors());

        services.AddScoped<ITenantProvider, TestTenantProvider>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();

        return new TestDbContextWrapper(provider, scope, context);
    }

    /// <summary>
    /// Creates an in-memory VanAnDbContext for unit testing
    /// </summary>
    /// <param name="databaseName">Unique database name</param>
    /// <returns>TestDbContextWrapper that properly disposes resources</returns>
    public static TestDbContextWrapper CreateInMemory(string databaseName)
    {
        var services = new ServiceCollection();

        services.AddDbContext<VanAnDbContext>(options =>
            options.UseInMemoryDatabase(databaseName)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors());

        services.AddScoped<ITenantProvider, TestTenantProvider>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();

        return new TestDbContextWrapper(provider, scope, context);
    }
}
