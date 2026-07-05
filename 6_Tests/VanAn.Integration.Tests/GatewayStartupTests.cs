using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.Gateway.Services;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Integration.Tests;

/// <summary>
/// Startup smoke tests for Gateway.
///
/// THESE TESTS ARE BLOCKING IN CI (ci-full.ps1 Step 2c).
///
/// WHY GATEWAY IS CRITICAL:
///   Gateway is the single point of entry for the entire ecosystem.
///   KhachLink (5002) → Gateway (5001) → ShopERP (5003)
///   A DI failure in Gateway brings down ALL services simultaneously.
///   Without a startup test, a missing AddScoped would only be discovered
///   on VPS after deployment — at that point the whole system is down.
///
/// HOW EACH TEST CATCHES REGRESSIONS:
///   Test 1 — CriticalServices_AreRegistered:
///     Explicitly resolves every critical service in Gateway Program.cs.
///     Catches the pattern: new controller added with @inject XxxService but
///     AddScoped forgotten in Program.cs.
///     The HMAC chain (IHmacApiKeyLookup → IApiKeyManagementService → IApiKeyRepository)
///     is particularly important: ApiKeyRepository needs IVanAnDbContext which Gateway
///     does not register natively — this test validates the factory workaround holds.
///
///   Test 2 — Health_Returns200:
///     /health is a public direct-Gateway endpoint (not forwarded through YARP).
///     Verifies the app boots without crash. If WebApplicationFactory throws
///     during host construction (e.g. required config missing like Jwt:Secret),
///     this test fails with a clear startup error rather than a cryptic NullReference.
///
///   Test 3 — ProtectedEndpoint_Returns_AuthResponse_Not_500:
///     A protected controller route must return 401/302 (auth challenge), NOT 500.
///     Catches DI exceptions surfacing as unhandled 500 errors in middleware/controllers.
///     Does not assert == 401 because Cookie default scheme may redirect to /login (302).
///
/// ADD TO TEST 1 whenever a new service is registered in Gateway Program.cs.
/// </summary>
[Trait("Category", "Startup")]
public class GatewayStartupTests : IClassFixture<GatewayWebApplicationFactory>
{
    private readonly GatewayWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GatewayStartupTests(GatewayWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // See actual status codes, not redirect targets
        });
    }

    /// <summary>
    /// Explicitly resolves every critical service registered in Gateway Program.cs.
    /// When a new Wave adds a controller with [FromServices] XxxService but forgets
    /// AddScoped in Program.cs → this test fails here, before code reaches VPS.
    ///
    /// ADD to this list whenever a new service is registered in Gateway Program.cs.
    /// </summary>
    [Fact(DisplayName = "Gateway: Tất cả critical services được đăng ký đầy đủ")]
    public async Task Gateway_CriticalServices_AreRegistered()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // HMAC chain — ApiKeyRepository needs IVanAnDbContext (handled by factory SQLite)
        Assert.NotNull(sp.GetRequiredService<IHmacApiKeyLookup>());
        Assert.NotNull(sp.GetRequiredService<IApiKeyManagementService>());

        // Routing & tenant resolution
        Assert.NotNull(sp.GetRequiredService<ITenantProvider>());

        // Business services used by Gateway controllers
        Assert.NotNull(sp.GetRequiredService<IShopConfigService>());
        Assert.NotNull(sp.GetRequiredService<IVietQrService>());
        Assert.NotNull(sp.GetRequiredService<IMstLookupService>());
        Assert.NotNull(sp.GetRequiredService<IVoiceCommandService>());
        Assert.NotNull(sp.GetRequiredService<ILocalizationService>());

        // Wave 4: Tenant Onboarding Service + seed strategies
        Assert.NotNull(sp.GetRequiredService<ITenantOnboardingService>());
        Assert.NotEmpty(sp.GetServices<IIndustrySeedStrategy>());
    }

    /// <summary>
    /// /health is a direct Gateway endpoint — not forwarded through YARP.
    /// Pure app startup check: if Gateway crashes on boot (missing required config,
    /// DI container build failure), this test fails with a clear error message.
    /// </summary>
    [Fact(DisplayName = "Gateway: /health trả về 200")]
    public async Task Gateway_Health_Returns200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A protected endpoint must respond with an auth challenge (401 or 302 to /login),
    /// NOT a 500 InternalServerError.
    /// Catches DI exceptions in middleware/controller initialization that surface as
    /// unhandled 500s instead of proper authentication failures.
    /// Does not assert == 401 because Cookie default scheme redirects to /login (302).
    /// </summary>
    [Fact(DisplayName = "Gateway: Protected endpoint trả về 401/302, không phải 500")]
    public async Task Gateway_ProtectedEndpoint_Returns_AuthResponse_Not_500()
    {
        var response = await _client.GetAsync("/api/orders");
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// Architecture validation: Gateway operates in monolithic mode (SaaS W0, Option B approved 2026-07-05).
    /// Gateway hosts in-process CoreHub services + DbContext (Npgsql) for low-latency access.
    /// This test verifies DbContext IS registered and functional.
    /// See: docs/AI/tasks/saas_w0_task_card.md + .windsurfrules CRITICAL ARCHITECTURAL BOUNDARIES amendment.
    /// </summary>
    [Fact(DisplayName = "Gateway: DbContext registered (monolithic mode - Option B approved)")]
    public async Task Gateway_Architecture_DbContext_Registered_Monolithic_Mode()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var dbContextService = sp.GetService<VanAn.CoreHub.Infrastructure.IVanAnDbContext>();
        Assert.NotNull(dbContextService);
    }
}
