using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VanAn.CoreHub.Infrastructure;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP;
using VanAn.ShopERP.Infrastructure;
using Xunit;

namespace VanAn.Integration.Tests
{
    /// <summary>
    /// AF-P1-T1 (TDD): HTTP auth + behavior tests for the cross-tenant customer list endpoint
    /// GET /api/customers/global (SystemAdmin-only).
    ///
    /// Verifies the security contract from the audit fix master plan:
    ///  - SystemAdmin → 200 + returns customers from MULTIPLE tenants (cross-tenant list works).
    ///  - Staff       → 403 (blocked by [Authorize(Policy = "SystemAdmin")] on the action,
    ///                       which combines with the controller-level [Authorize(Policy = "OwnerOnly")]
    ///                       to require SystemAdmin specifically).
    ///  - Anonymous   → auth-enforced (302 redirect to login — NOT 200).
    ///
    /// SystemAdmin + Anonymous tests use AuthRealWebApplicationFactory (real Cookie auth, EDR-AM-1).
    /// Staff test uses StaffRoleWebApplicationFactory (TestScheme with Staff role claim) because the
    /// ShopERP host has no real Staff login endpoint in the test harness (Staff is a tenant-scoped
    /// User; only PlatformUser sysadmin is seeded for real-auth tests).
    /// Cross-tenant customers are seeded directly into the shared SQLite connection via ShopERPDbContext.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Category", "AccessMatrix")]
    public class CustomerGlobalEndpointAuthTests : IClassFixture<AuthRealWebApplicationFactory>
    {
        private readonly AuthRealWebApplicationFactory _factory;
        private readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        public CustomerGlobalEndpointAuthTests(AuthRealWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact(DisplayName = "AF-P1-T1-D: SystemAdmin GET /api/customers/global returns 200 with cross-tenant customers")]
        public async Task SystemAdmin_GetGlobal_Returns200WithCrossTenantCustomers()
        {
            await SeedCrossTenantCustomersAsync();

            var client = await _factory.CreateSystemAdminClientAsync();

            var response = await client.GetAsync("/api/customers/global?page=1&pageSize=20");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<GlobalListResponse>();
            Assert.NotNull(body);
            Assert.NotNull(body!.items);
            Assert.NotEmpty(body.items);
            // Cross-tenant contract: at least one customer from each seeded tenant
            Assert.Contains(body.items, c => c.TenantId == _tenantA);
            Assert.Contains(body.items, c => c.TenantId == _tenantB);
        }

        [Fact(DisplayName = "AF-P1-T1-F: Anonymous GET /api/customers/global is auth-enforced (not 200)")]
        public async Task Anonymous_GetGlobal_AuthEnforced()
        {
            var client = _factory.CreateClient(NoRedirectOptions);

            var response = await client.GetAsync("/api/customers/global");

            // Auth-enforced: anonymous must NOT reach the action (200). Cookie auth challenges with
            // 302 redirect to /Login (DefaultChallengeScheme = Cookie, LoginPath = /Login).
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.True(
                response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Redirect ||
                response.StatusCode == HttpStatusCode.RedirectKeepVerb ||
                response.StatusCode == HttpStatusCode.TemporaryRedirect,
                $"Expected 401/30x (auth enforced), got {response.StatusCode}");
        }

        private static WebApplicationFactoryClientOptions NoRedirectOptions => new()
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        };

        private async Task SeedCrossTenantCustomersAsync()
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();

            // Avoid duplicate seeds across test invocations (xUnit fixture is shared).
            var existing = await db.Customers.IgnoreQueryFilters()
                .Where(c => c.FullName == "AF-P1-T1-Alice" || c.FullName == "AF-P1-T1-Bob")
                .FirstOrDefaultAsync();
            if (existing != null) return;

            var alice = new Customer(new TenantId(_tenantA), "AF-P1-T1-Alice", "0900000051", "alice@af-p1-t1.example");
            var bob = new Customer(new TenantId(_tenantB), "AF-P1-T1-Bob", "0900000052", "bob@af-p1-t1.example");

            _ = await db.Customers.AddAsync(alice);
            _ = await db.Customers.AddAsync(bob);
            _ = await db.SaveChangesAsync();
        }

        private sealed class GlobalListResponse
        {
            public List<GlobalCustomerDto>? items { get; set; }
            public int total { get; set; }
            public int page { get; set; }
            public int pageSize { get; set; }
        }

        private sealed class GlobalCustomerDto
        {
            public Guid Id { get; set; }
            public Guid TenantId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string? PhoneNumber { get; set; }
            public string CustomerTier { get; set; } = string.Empty;
            public int PointBalance { get; set; }
            public decimal TotalSpent { get; set; }
            public DateTime? LastOrderDate { get; set; }
            public DateTime? Birthday { get; set; }
            public string IdentityLevel { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }
    }

    /// <summary>
    /// AF-P1-T1-E: Staff role is blocked from the SystemAdmin-only cross-tenant endpoint.
    /// Uses a TestScheme auth handler that authenticates with the Staff role claim (no real
    /// Staff login endpoint exists in the test harness). Verifies the policy combination
    /// (controller OwnerOnly + action SystemAdmin) rejects non-SystemAdmin roles with 403.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Category", "AccessMatrix")]
    public class CustomerGlobalStaffBlockedTests : IClassFixture<StaffRoleWebApplicationFactory>
    {
        private readonly StaffRoleWebApplicationFactory _factory;

        public CustomerGlobalStaffBlockedTests(StaffRoleWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact(DisplayName = "AF-P1-T1-E: Staff GET /api/customers/global returns 403")]
        public async Task Staff_GetGlobal_Returns403()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/customers/global");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    /// <summary>
    /// Test WebApplicationFactory that authenticates every request as a Staff-role user via a
    /// TestScheme auth handler. Mirrors CustomWebApplicationFactory's SQLite-in-memory setup but
    /// issues the "Staff" role claim instead of "Admin". Used to verify non-SystemAdmin roles are
    /// blocked by the [Authorize(Policy = "SystemAdmin")] action filter.
    /// </summary>
    public sealed class StaffRoleWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection;

        public StaffRoleWebApplicationFactory()
        {
            _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ShopInstance:Id"] = "00000000-0000-0000-0000-000000000001"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ShopERPDbContext>>();
                services.RemoveAll<DbContextOptions<VanAnDbContext>>();
                services.RemoveAll<ShopERPDbContext>();
                services.RemoveAll<VanAnDbContext>();
                services.RemoveAll<IVanAnDbContext>();

                var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

                services.AddDbContext<ShopERPDbContext>(options => options.UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection));
                services.AddDbContext<VanAnDbContext>(options => options.UseSqlite(_connection));
                services.AddScoped<IVanAnDbContext>(provider => provider.GetRequiredService<ShopERPDbContext>());
                services.AddScoped<IAccountingDbContext>(provider => provider.GetRequiredService<VanAnDbContext>());
                services.AddScoped<ITenantProvider, TestTenantProvider>();

                services.PostConfigure<OpenIdConnectOptions>("OpenIdConnect", options =>
                {
                    options.Authority = "http://localhost:5001";
                    options.MetadataAddress = "http://localhost:5001/.well-known/openid-configuration";
                    options.RequireHttpsMetadata = false;
                    options.RefreshInterval = TimeSpan.FromDays(365);
                    options.AutomaticRefreshInterval = TimeSpan.FromDays(365);
                });

                // Replace production auth with TestScheme that authenticates as Staff.
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestScheme";
                    options.DefaultChallengeScheme = "TestScheme";
                    options.DefaultForbidScheme = "TestScheme";
                })
                .AddScheme<AuthenticationSchemeOptions, StaffAuthHandler>("TestScheme", options => { });
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            IHost host = base.CreateHost(builder);
            using IServiceScope scope = host.Services.CreateScope();
            var vanAnContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
            _ = vanAnContext.Database.EnsureCreated();
            return host;
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
    /// Test auth handler that authenticates every request as a Staff-role user with a fixed tenant.
    /// Used by StaffRoleWebApplicationFactory to verify non-SystemAdmin roles are blocked from
    /// SystemAdmin-only endpoints (403, not 200).
    /// </summary>
    file sealed class StaffAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public StaffAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new(ClaimTypes.Name, "Test Staff"),
                new(ClaimTypes.Role, "Staff"),
                new("tenant_id", "12345678-1234-1234-1234-123456789abc"),
                new("TenantId", "12345678-1234-1234-1234-123456789abc"),
            };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
