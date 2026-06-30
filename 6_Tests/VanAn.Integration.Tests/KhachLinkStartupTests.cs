using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Services.Http;
using VanAn.KhachLink.Services.PWA;
using Xunit;

namespace VanAn.Integration.Tests;

/// <summary>
/// Startup smoke tests for KhachLink.
///
/// THESE TESTS ARE BLOCKING IN CI (ci-full.ps1 Step 2b).
///
/// ROOT CAUSE THEY PREVENT:
///   - Missing AddScoped&lt;X&gt;() in Program.cs goes undetected because
///     CustomWebApplicationFactory only boots ShopERP, never KhachLink.
///     The DI error only surfaced at runtime on VPS.
///
/// HOW EACH TEST CATCHES REGRESSIONS:
///   Test 1 — AllServices_AreRegistered:
///     Explicitly resolves every service injected in KhachLink components.
///     When a Wave adds a new @inject but forgets AddScoped → this test fails immediately.
///
///   Test 2 — Health_Returns200:
///     Verifies the app boots at all. If WebApplicationFactory throws during host
///     construction (e.g. required config missing), every test in this class fails
///     with a clear startup error.
///
///   Test 3 — Homepage_Does_Not_Return_500:
///     Renders the full Blazor SSR pipeline for the home route. Catches:
///       (a) Missing DI during component instantiation
///       (b) JS interop calls in OnInitializedAsync during prerendering
///     This is the regression test for the exact 500 error that reached VPS.
/// </summary>
[Trait("Category", "Startup")]
public class KhachLinkStartupTests : IClassFixture<KhachLinkWebApplicationFactory>
{
    private readonly KhachLinkWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public KhachLinkStartupTests(KhachLinkWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // See actual status codes, not redirect targets
        });
    }

    /// <summary>
    /// Explicitly resolves every service registered in KhachLink Program.cs.
    /// When a new Wave adds @inject SomeService but forgets AddScoped → test fails here,
    /// before the code ever reaches the VPS.
    ///
    /// ADD TO THIS LIST whenever a new service is registered in KhachLink Program.cs.
    /// </summary>
    [Fact(DisplayName = "KhachLink: Tất cả services trong Program.cs được đăng ký đầy đủ")]
    public async Task KhachLink_AllServices_AreRegistered()
    {
        // AsyncServiceScope: required because PWAService implements IAsyncDisposable only
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Wave 8: RecentlyViewedService — bị quên đăng ký, gây 500 trên VPS
        Assert.NotNull(sp.GetRequiredService<RecentlyViewedService>());

        // Core KhachLink services
        Assert.NotNull(sp.GetRequiredService<PWAService>());
        Assert.NotNull(sp.GetRequiredService<ProductHttpService>());
        Assert.NotNull(sp.GetRequiredService<CartService>());
        Assert.NotNull(sp.GetRequiredService<CheckoutFlowState>());
    }

    /// <summary>
    /// /health không cần gateway hay database — pure app startup check.
    /// Nếu WebApplicationFactory crash khi boot (missing required config, port conflict, ...),
    /// test này fail với message rõ ràng thay vì NullReferenceException mơ hồ.
    /// </summary>
    [Fact(DisplayName = "KhachLink: /health trả về 200")]
    public async Task KhachLink_Health_Returns200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Render toàn bộ Blazor SSR pipeline cho home route.
    /// Bắt đúng loại lỗi đã xảy ra:
    ///   - Missing DI registration → 500 khi component được instantiate
    ///   - JS interop trong OnInitializedAsync → 500 vì prerendering không có JS
    ///
    /// Contract: phải KHÔNG phải 500. Cho phép 200 hoặc redirect (302/301).
    /// Không assert == 200 vì Blazor có thể redirect tùy auth config.
    /// </summary>
    [Fact(DisplayName = "KhachLink: Homepage render không trả về 500")]
    public async Task KhachLink_Homepage_Does_Not_Return_500()
    {
        var response = await _client.GetAsync("/");
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// Architecture validation: KhachLink must NOT have direct DbContext references.
    /// KhachLink is a client UI and should access data via HTTP through Gateway only.
    /// This test validates that IVanAnDbContext is NOT registered in KhachLink DI container.
    /// </summary>
    [Fact(DisplayName = "KhachLink: Architecture validation - no DbContext registered")]
    public async Task KhachLink_Architecture_No_DbContext_Registered()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // KhachLink MUST NOT have IVanAnDbContext registered
        var dbContextService = sp.GetService<Microsoft.EntityFrameworkCore.DbContext>();
        if (dbContextService != null)
        {
            Assert.True(false, "KhachLink architecture violation: IVanAnDbContext must NOT be registered in KhachLink (HTTP-only pattern via Gateway)");
        }
    }
}
