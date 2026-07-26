using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Common;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory for Gateway — validates the real DI container and critical service resolution.
///
/// WHY THIS EXISTS:
///   Gateway had no WebApplicationFactory in CI. Missing AddScoped&lt;X&gt;() registrations
///   pass all checks silently and only crash on VPS at runtime. Gateway is the single
///   point of entry — its failure brings down KhachLink, ShopERP, and Accounting entirely.
///
/// DI CHALLENGE: ApiKeyRepository(IVanAnDbContext db) but Gateway does not register any
/// DbContext. In Development mode, .NET validates service descriptors on container build
/// and throws InvalidOperationException. Fix: factory adds SQLite in-memory VanAnDbContext
/// (same pattern as CustomWebApplicationFactory for ShopERP).
/// Production code is NOT touched — this addition is test-only.
///
/// YARP ROUTES: Cluster destinations (shoperp:80, khachlink:80, corehub:80) do not exist
/// in the test environment. Safe because tests only hit /health (a direct Gateway endpoint,
/// not forwarded through YARP) and one protected controller route.
///
/// DESIGN DECISIONS:
///   - Environment = Development: Jwt:Secret is present in appsettings.Development.json.
///   - ShopERP:BaseUrl overridden to unreachable address: HttpClient is lazy — no connection
///     is attempted at startup.
///   - SQLite VanAnDbContext added via TryAddScoped so a future Gateway DbContext registration
///     does not cause a duplicate-registration error.
///   - ITenantProvider replaced with TestTenantProvider to satisfy VanAnDbContext multi-tenancy.
/// </summary>
public class GatewayWebApplicationFactory : WebApplicationFactory<VanAn.Gateway.Program>
{
    private readonly SqliteConnection _connection;

    public GatewayWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Dummy URL — HttpClient is lazy, no connection attempted at startup.
                // YARP clusters also point to non-existent addresses; tests avoid forwarded routes.
                ["ShopERP:BaseUrl"] = "http://localhost:19999"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL existing DbContext registrations so PostgreSQL is fully replaced by SQLite.
            // SingleOrDefault is not sufficient — EF Core registers multiple descriptors
            // (DbContextOptions<T>, the concrete type, and the interface). Remove all of them.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<VanAnDbContext>)
                         || d.ServiceType == typeof(VanAnDbContext)
                         || d.ServiceType == typeof(IVanAnDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            // Add SQLite in-memory VanAnDbContext on the shared open connection.
            // The connection is kept open for the lifetime of the factory so EnsureCreated
            // schema and all subsequent queries share the same in-memory database.
            services.AddDbContext<IVanAnDbContext, VanAnDbContext>(options =>
                options.UseSqlite(_connection));

            // WAVE 3: IAccountingDbContext → VanAnDbContext (implements both interfaces, has accounting DbSets)
            services.AddScoped<IAccountingDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());

            // ITenantProvider needed by VanAnDbContext multi-tenancy filters.
            // Replace with test provider that returns a deterministic test tenant ID.
            services.RemoveAll<ITenantProvider>();
            services.AddScoped<ITenantProvider, TestTenantProvider>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Ensure schema exists BEFORE the host starts (race condition fix — same as
        // CustomWebApplicationFactory). Gateway hosted services (DataSyncSubscriber,
        // EInvoiceSyncSubscriber) may query the database on startup.
        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var tempContext = new VanAnDbContext(options);
        _ = tempContext.Database.EnsureCreated();

        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _connection.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>
    /// Test-only tenant provider: returns a fixed test tenant ID.
    /// Required by VanAnDbContext multi-tenancy filters.
    /// </summary>
    private sealed class TestTenantProvider : ITenantProvider
    {
        private static readonly Guid TestTenantId = new Guid("12345678-1234-1234-1234-123456789abc");

        public Guid TenantId => TestTenantId;
        public string? CurrentUser => "test-user";
        public bool HasTenant => true;

        public void SetTenant(Guid tenantId)
        {
            // No-op for tests — always return fixed tenant ID
        }
    }
}
