using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP;
using VanAn.ShopERP.Infrastructure;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// AM-T10: WebApplicationFactory for SystemAdmin access matrix tests.
/// Uses REAL authentication (Cookie + JWT Bearer) — NO TestAuthenticationHandler.
/// This is required for EDR-AM-1: verify access with real auth, not auto-authenticate.
/// </summary>
public class AuthRealWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public AuthRealWebApplicationFactory()
    {
        _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Mark as "Testing" to skip dual-database migration in Program.Main (same as CustomWebApplicationFactory).
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // OrderSyncSubscriber requires ShopInstance:Id (same as CustomWebApplicationFactory).
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ShopInstance:Id"] = "00000000-0000-0000-0000-000000000001"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Same SQLite setup as CustomWebApplicationFactory
            services.RemoveAll<DbContextOptions<ShopERPDbContext>>();
            services.RemoveAll<DbContextOptions<VanAnDbContext>>();
            services.RemoveAll<ShopERPDbContext>();
            services.RemoveAll<VanAnDbContext>();
            services.RemoveAll<IVanAnDbContext>();

            var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

            services.AddDbContext<ShopERPDbContext>(options => options.UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection));
            services.AddDbContext<VanAnDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<IVanAnDbContext>(provider => provider.GetRequiredService<ShopERPDbContext>());
            // WAVE 3: IAccountingDbContext → VanAnDbContext (implements both interfaces, has accounting DbSets)
            services.AddScoped<IAccountingDbContext>(provider => provider.GetRequiredService<VanAnDbContext>());
            services.AddScoped<ITenantProvider, TestTenantProvider>();

            // Configure OpenIdConnect to skip metadata fetch (same as CustomWebApplicationFactory).
            // Cookie + JWT Bearer auth from Program.cs is kept intact — this factory does NOT
            // replace them with TestAuthenticationHandler.
            services.PostConfigure<OpenIdConnectOptions>("OpenIdConnect", options =>
            {
                options.Authority = "http://localhost:5001";
                options.MetadataAddress = "http://localhost:5001/.well-known/openid-configuration";
                options.RequireHttpsMetadata = false;
                options.RefreshInterval = TimeSpan.FromDays(365);
                options.AutomaticRefreshInterval = TimeSpan.FromDays(365);
            });

            // Override Cookie SecurePolicy for testing.
            // Program.cs sets CookieSecurePolicy.Always in non-Development mode (Testing is not Development).
            // Always = cookies only sent over HTTPS. Test server uses HTTP → cookie dropped → auth fails.
            // Fix: set SameAsRequest so cookies are sent over HTTP in tests.
            services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            // Mock the GatewayClient HttpClient for impersonation tests.
            // AdminController.Impersonate validates tenants via Gateway HTTP (Option C — PG source of truth).
            // In tests, Gateway isn't running. This mock intercepts GET api/v1/tenants/{id} and returns
            // a fake tenant DTO for the test tenant ID, 404 for unknown IDs.
            services.AddHttpClient("GatewayClient")
                .ConfigurePrimaryHttpMessageHandler(() => new MockGatewayHandler());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);

        // Use VanAnDbContext (superset model) for EnsureCreated — includes accounting tables
        // (JournalEntries, AccountCharts) that ShopERPDbContext excludes.
        using IServiceScope scope = host.Services.CreateScope();
        var vanAnContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        if (!IsSchemaCreated(vanAnContext))
        {
            _ = vanAnContext.Database.EnsureCreated();
        }

        // Seed test data needed by AM-S* tests.
        // Program.Main seeding is skipped in Testing environment (Testing env guard),
        // so the factory must seed PlatformUser + test tenant itself.
        var shopContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        SeedTestData(shopContext);

        return host;
    }

    private static void SeedTestData(ShopERPDbContext context)
    {
        // Seed PlatformUser (sysadmin@vanan.vn / VanAn@2026) — needed by CreateSystemAdminClientAsync
        var existingAdmin = context.PlatformUsers
            .FirstOrDefault(u => u.Username == "sysadmin@vanan.vn");
        if (existingAdmin == null)
        {
            var sysadminHash = BCrypt.Net.BCrypt.HashPassword("VanAn@2026", 12);
            context.PlatformUsers.Add(new VanAn.CoreHub.Infrastructure.Entities.PlatformUser(
                "sysadmin@vanan.vn",
                sysadminHash,
                "System Admin",
                "sysadmin@vanan.vn"));
            _ = context.SaveChanges();
        }

        // Seed test tenant (00000000-0000-0000-0000-000000000001) — needed by impersonation tests
        var testTenantId = new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var existingTenant = context.Tenants.IgnoreQueryFilters()
            .FirstOrDefault(t => t.Id == testTenantId);
        if (existingTenant == null)
        {
            var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(
                testTenantId,
                "Test Tenant",
                VanAn.Shared.Domain.Aggregates.TenantAggregate.TenantSettings.Empty());
            context.Tenants.Add(tenant);
            context.Entry(tenant).Property("TenantId").CurrentValue = testTenantId;
            _ = context.SaveChanges();
        }
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

    /// <summary>
    /// Creates an HttpClient authenticated as SystemAdmin via Cookie auth.
    /// Calls POST /api/platform/login to get a real auth cookie, then returns the client.
    /// </summary>
    public async Task<HttpClient> CreateSystemAdminClientAsync()
    {
        var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/platform/login", new
        {
            Username = "sysadmin@vanan.vn",
            Password = "VanAn@2026"
        });
        loginResponse.EnsureSuccessStatusCode();
        return client; // HttpClient now has the auth cookie
    }

    /// <summary>
    /// Impersonates a tenant on behalf of an already-authenticated SystemAdmin client.
    /// </summary>
    public async Task ImpersonateTenantAsync(HttpClient client, Guid tenantId)
    {
        var response = await client.PostAsync($"/api/admin/impersonate/{tenantId}", null);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Exits impersonation for the given client.
    /// </summary>
    public async Task ExitImpersonationAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/admin/exit-impersonation", null);
        response.EnsureSuccessStatusCode();
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

/// <summary>
/// Mock HttpMessageHandler for GatewayClient — intercepts tenant validation calls
/// from AdminController.Impersonate. Returns a fake active tenant for the test tenant ID,
/// 404 for unknown IDs. This avoids needing a real Gateway server in integration tests.
/// </summary>
file sealed class MockGatewayHandler : HttpMessageHandler
{
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        // Match: api/v1/tenants/{guid}
        if (path.StartsWith("/api/v1/tenants/", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Get)
        {
            var idPart = path["/api/v1/tenants/".Length..];
            if (Guid.TryParse(idPart, out Guid tenantId))
            {
                if (tenantId == TestTenantId)
                {
                    // TenantStatus.Active = 1 (int enum — System.Text.Json expects numeric value, not string)
                    var json = """{"id":"00000000-0000-0000-0000-000000000001","name":"Test Tenant","status":1}""";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    });
                }
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
            }
        }
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}
