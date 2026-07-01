using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory for KhachLink — validates the real DI container and SSR rendering.
///
/// WHY THIS EXISTS:
///   CustomWebApplicationFactory only boots ShopERP. KhachLink's DI container was never
///   started in any test, so missing AddScoped&lt;X&gt;() registrations in Program.cs were
///   silently passing CI and only failing on VPS at runtime.
///
/// WHAT IT CATCHES:
///   1. Missing service registrations  → InvalidOperationException at component resolution
///   2. JS interop in OnInitializedAsync → InvalidOperationException during SSR prerendering
///
/// DESIGN DECISIONS:
///   - Environment = Development: loads appsettings.Development.json. Avoids UseHsts().
///   - Gateway:BaseUrl overridden to unreachable addr: HTTP calls fail gracefully (try-catch).
///   - No service mocking: we test REAL registrations from Program.cs. Mocking defeats purpose.
///     All Http services (ProductHttpService, etc.) already catch exceptions → return [].
///     The "Connection refused" log lines are expected and harmless during startup tests.
/// </summary>
public class KhachLinkWebApplicationFactory : WebApplicationFactory<VanAn.KhachLink.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Override Gateway URL so no real network call is attempted.
            // All Http services catch HttpRequestException → return empty data.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:BaseUrl"] = "http://localhost:19999"
            });
        });
    }
}
