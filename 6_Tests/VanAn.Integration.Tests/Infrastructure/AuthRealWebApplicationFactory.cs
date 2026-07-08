using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Same SQLite setup as CustomWebApplicationFactory
            services.RemoveAll<DbContextOptions<ShopERPDbContext>>();
            services.RemoveAll<DbContextOptions<VanAnDbContext>>();
            services.RemoveAll<ShopERPDbContext>();
            services.RemoveAll<VanAnDbContext>();
            services.RemoveAll<IVanAnDbContext>();

            services.AddDbContext<ShopERPDbContext>(options => options.UseSqlite(_connection));
            services.AddDbContext<VanAnDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<IVanAnDbContext>(provider => provider.GetRequiredService<ShopERPDbContext>());
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
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);

        using IServiceScope scope = host.Services.CreateScope();
        var shopContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        if (!IsSchemaCreated(shopContext))
        {
            _ = shopContext.Database.EnsureCreated();
        }

        return host;
    }

    private static bool IsSchemaCreated(ShopERPDbContext context)
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
