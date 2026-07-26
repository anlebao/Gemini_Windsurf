using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Encodings.Web;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP;
using VanAn.ShopERP.Infrastructure;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Fake authentication handler for integration tests.
/// Bypasses OpenIdConnect network calls while preserving authorization flow.
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim("TenantId", "12345678-1234-1234-1234-123456789abc"),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Points to ShopERP (the Web API host) and replaces the production SQLite database
/// with an isolated in-memory SQLite instance so each test run is deterministic.
///
/// Root-cause fix for SQLite in-memory schema errors:
/// - The factory owns a single opened SqliteConnection for the whole test lifetime.
/// - All DbContext registrations (ShopERPDbContext and VanAnDbContext) are forced to use this
///   physical connection instead of creating new per-context connections from the
///   "DataSource=:memory:" string.
/// - Schema creation is performed exactly once per factory instance on the owned connection.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public CustomWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Mark environment as "Testing" so Program.Main can skip dual-database migration
        // (VanAnDbContext + ShopERPDbContext share the same SQLite connection in tests;
        // migrating both causes "table already exists" since both contexts map AccountCharts).
        // The factory's EnsureCreated() handles schema creation for the test environment.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // OrderSyncSubscriber (IHostedService) requires ShopInstance:Id to route NATS
            // subscriptions. Without it, the subscriber throws on startup and crashes the host.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ShopInstance:Id"] = "00000000-0000-0000-0000-000000000001"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the original DbContextOptions registrations so we can replace them
            // with a single, shared, opened SQLite in-memory connection.
            services.RemoveAll<DbContextOptions<ShopERPDbContext>>();
            services.RemoveAll<DbContextOptions<VanAnDbContext>>();

            // Remove any direct DbContext / IVanAnDbContext registrations as well.
            services.RemoveAll<ShopERPDbContext>();
            services.RemoveAll<VanAnDbContext>();
            services.RemoveAll<IVanAnDbContext>();

            // Use the factory-owned, already opened connection. EF Core will not close it,
            // so the in-memory database survives for the whole factory lifetime.
            services.AddDbContext<ShopERPDbContext>(options => options.UseSqlite(_connection));
            services.AddDbContext<VanAnDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<IVanAnDbContext>(provider => provider.GetRequiredService<ShopERPDbContext>());
            // WAVE 3: IAccountingDbContext → VanAnDbContext (implements both interfaces, has accounting DbSets)
            services.AddScoped<IAccountingDbContext>(provider => provider.GetRequiredService<VanAnDbContext>());

            // Deterministic tenant provider for multi-tenancy tests.
            services.AddScoped<ITenantProvider, TestTenantProvider>();

            // Configure OpenIdConnect to skip metadata fetch for integration tests
            services.PostConfigure<OpenIdConnectOptions>("OpenIdConnect", options =>
            {
                options.Authority = "http://localhost:5001";
                options.MetadataAddress = "http://localhost:5001/.well-known/openid-configuration";
                options.RequireHttpsMetadata = false;
                // Disable automatic metadata refresh
                options.RefreshInterval = TimeSpan.FromDays(365);
                options.AutomaticRefreshInterval = TimeSpan.FromDays(365);
            });

            // Add test authentication scheme that bypasses network calls
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("TestScheme", options => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);

        // Ensure schema is created on the owned connection.
        // VanAnDbContext is the SUPERSET model (includes accounting entities like JournalEntries,
        // AccountCharts that ShopERPDbContext excludes). EnsureCreated on VanAnDbContext creates
        // ALL tables. ShopERPDbContext shares the same connection and its tables are a subset,
        // so no separate EnsureCreated is needed for it.
        using IServiceScope scope = host.Services.CreateScope();
        var vanAnContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        if (!IsSchemaCreated(vanAnContext))
        {
            _ = vanAnContext.Database.EnsureCreated();
        }

        return host;
    }

    private static bool IsSchemaCreated(DbContext context)
    {
        try
        {
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Users' LIMIT 1;";
            var result = command.ExecuteScalar();
            return result != null && result != DBNull.Value;
        }
        catch
        {
            return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }

        base.Dispose(disposing);
    }
}
