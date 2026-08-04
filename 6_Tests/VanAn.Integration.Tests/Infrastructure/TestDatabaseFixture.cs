using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Common;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Test database fixture using SQLite in-memory database
/// Provides a real SQLite database for integration tests with proper lifecycle management
/// Implements xUnit IAsyncLifetime for proper resource cleanup
/// Connection string loaded from appsettings.test.json (no hardcoding)
/// </summary>
public class TestDatabaseFixture : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private VanAnDbContext? _dbContext;
    private TestTenantProvider? _tenantProvider;
    private readonly IConfiguration _configuration;

    public VanAnDbContext DbContext => _dbContext ?? throw new InvalidOperationException("DbContext not initialized");
    public string ConnectionString => _configuration.GetConnectionString("TestcontainersSqlite") ?? "DataSource=:memory:";

    public TestDatabaseFixture()
    {
        // Load test configuration from appsettings.test.json
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: false)
            .Build();

        // SQLite in-memory: connection stays open for test lifetime
        // Connection string loaded from configuration (no hardcoding)
        _connection = new SqliteConnection(ConnectionString);
        _connection.Open();

        var services = new ServiceCollection();

        // Add configuration
        services.AddSingleton(_configuration);

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add DbContext with SQLite connection from configuration
        // Connection pooling configured via connection string
        var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseInternalServiceProvider(efServiceProvider)
                   .UseSqlite(_connection)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors());

        services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        // WAVE 3: IAccountingDbContext → VanAnDbContext (implements both interfaces, has accounting DbSets)
        services.AddScoped<IAccountingDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
        // W5: Singleton so SetCurrentTenant affects all scopes (CreateFreshDbContext creates new scopes
        // that would otherwise get a default-tenant TestTenantProvider instance).
        services.AddSingleton<ITenantProvider, TestTenantProvider>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        _dbContext = _serviceProvider.GetRequiredService<VanAnDbContext>();
        _tenantProvider = _serviceProvider.GetRequiredService<ITenantProvider>() as TestTenantProvider;

        // W4a fix: EnsureDeletedAsync before EnsureCreatedAsync to prevent
        // "table AccountCharts already exists" error when fixture is reused
        // or SQLite in-memory state persists from prior test class.
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }

        await _connection.DisposeAsync();
    }
    
    /// <summary>
    /// Creates a fresh DbContext instance for each test to avoid tracking conflicts
    /// </summary>
    public VanAnDbContext CreateFreshDbContext()
    {
        var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        return context;
    }
    
    /// <summary>
    /// Sets the current tenant ID for the test
    /// </summary>
    public void SetCurrentTenant(TenantId tenantId)
    {
        if (_tenantProvider != null)
        {
            _tenantProvider.SetTenant(tenantId.Value);
        }
    }
}
