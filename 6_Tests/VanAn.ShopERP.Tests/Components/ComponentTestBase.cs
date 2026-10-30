using VanAn.UI.Platform.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.CongNo;
using Bunit;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Adapters;
using Microsoft.AspNetCore.Components;
using Bunit.JSInterop;
using VanAn.UI.Platform.Components;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Tests.Components;

/// <summary>
/// Base class for component tests with common service registrations
/// </summary>
public class ComponentTestBase : TestContext
{
    public ComponentTestBase()
    {
        // Register UI Platform services
        Services.AddSingleton<IThemeProvider, ThemeProvider>();
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
        
        // Register Authentication with tenant ID
        Services.AddSingleton<AuthenticationStateProvider, TestAuthenticationStateProvider>();
        
        // Register Logging
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        
        // Register Core Services (mock by default, tests can override)
        Services.AddSingleton<IAccountingService>(sp => new Mock<IAccountingService>().Object);
        
        // Register ITenantProvider mock — required by Blazor components with [Inject] ITenantProvider
        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(t => t.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        mockTenantProvider.Setup(t => t.CurrentUser).Returns("test-user");
        mockTenantProvider.Setup(t => t.HasTenant).Returns(true);
        Services.AddSingleton(mockTenantProvider.Object);

        // Register IVasFeatureFlagService mock — required by AccountingLayout
        // (Stream F W8 added [Inject] IVasFeatureFlagService to dynamically show/hide VAS menu).
        // Default: return false (HKD tenant — no VAS menu). Tests needing Enterprise can override.
        var mockFeatureFlag = new Mock<IVasFeatureFlagService>();
        mockFeatureFlag.Setup(f => f.CanAccessVasReportsAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(false);
        Services.AddSingleton(mockFeatureFlag.Object);

        // Register ITenantManagementService mock — required by UserHeader component
        // (AccountingLayout renders UserHeader which [Inject]s ITenantManagementService).
        Services.AddSingleton<ITenantManagementService>(sp => new Mock<ITenantManagementService>().Object);

        // Regime-lock (2026-10-30): IAccountingStandardSelectionService — real service backed by
        // DI-registered mocks (factory resolve at render time → test re-registered FeatureFlag
        // GetTenantTypeAsync vẫn được tôn trọng). Default: GetTenantTypeAsync unconfigured →
        // null → allowed đủ 4 options + effective TT133 (giữ hành vi cũ cho report pages).
        Services.AddSingleton<VanAn.ShopERP.Services.Accounting.IAccountingStandardSelectionService>(
            sp => new VanAn.ShopERP.Services.Accounting.AccountingStandardSelectionService(
                sp.GetRequiredService<IVasFeatureFlagService>(),
                sp.GetRequiredService<ITenantManagementService>()));

        // FinancialReports hub (W3): IDnsnBookService — mock by default.
        Services.AddSingleton<IDnsnBookService>(_ => new Mock<IDnsnBookService>().Object);

        // Register VanAnDbContext (PG) mock — required by UserHeader/NavMenu/Sitemap
        // (2026-10-09 fix: tenant name đọc TRỰC TIẾP từ PG — source of truth, không qua SQLite mirror).
        // Mock instance: constructor chỉ lưu options; query Tenants sẽ trả null → component catch (fallback giữ chuỗi gốc).
        var pgOptions = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<VanAn.CoreHub.Infrastructure.VanAnDbContext>().Options;
        Services.AddSingleton(new Mock<VanAn.CoreHub.Infrastructure.VanAnDbContext>(
            pgOptions, new Mock<ITenantProvider>().Object).Object);

        // Configure JSInterop for components with JavaScript interop
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Register UI Platform components
        Services.AddSingleton<VanALayout>();
        Services.AddSingleton<VanANavigation>();

        // Register nav menu SSOT service (deps: ITenantProvider + IVasFeatureFlagService mocks above)
        Services.AddSingleton<IShopErpMenuService, ShopErpMenuService>();

        // 2026-10-01: business-info lookup client (phiếu thu/chi MST) — mock by default
        Services.AddSingleton<IBusinessInfoApiClient>(_ => new Mock<IBusinessInfoApiClient>().Object);

        // Notification Hub (2026-10-15): NotificationApiClient mock — UserHeader bell badge.
        // Concrete sealed class — dùng instance THẬT với handler trả 500 → GetUnreadCountAsync
        // throw → catch fail-safe trong UserHeader (badge = 0, không làm hỏng render).
        var notifHttpClient = new HttpClient(new StubHttpHandler()) { BaseAddress = new Uri("http://localhost") };
        var notifFactory = new Mock<IHttpClientFactory>();
        notifFactory.Setup(f => f.CreateClient("GatewayClient")).Returns(notifHttpClient);
        Services.AddSingleton(new NotificationApiClient(
            notifFactory.Object,
            new ConfigurationBuilder().Build(),
            Mock.Of<IJwtTokenService>(),
            new TestAuthenticationStateProvider(),
            NullLogger<NotificationApiClient>.Instance));

        // THU CHI & CÔNG NỢ MVP (P2, 2026-10-07): ICongNoService + IReversalService — mock by default
        Services.AddSingleton<ICongNoService>(_ => new Mock<ICongNoService>().Object);
        Services.AddSingleton<IReversalService>(_ => new Mock<IReversalService>().Object);

        // NHẬP LIỆU & SỔ SÁCH P3 (2026-10-07): IOpeningBalanceService — mock by default
        Services.AddSingleton<IOpeningBalanceService>(_ => new Mock<IOpeningBalanceService>().Object);

        // W4 (P1.3, 2026-10-18): IBalanceAlertService — mock by default (Payment/ExpenseEntry inject).
        // Default returns null (no alert); tests needing alerts override before render.
        Services.AddSingleton<IBalanceAlertService>(_ => new Mock<IBalanceAlertService>().Object);

        // Issue #197 (2026-10-29): metering client + attribute resolver — mock by default.
        // MeteringDenyGate/FeatureUsageRecorder (AccountingLayout/Loyalty/Booking pages) inject
        // chúng; default CheckBalanceAsync trả null → fail-open (content render bình thường).
        Services.AddSingleton<IMeteringClient>(_ => new Mock<IMeteringClient>().Object);
        Services.AddSingleton<IMeteringTenantAttributeResolver>(_ => new Mock<IMeteringTenantAttributeResolver>().Object);
        Services.AddSingleton<FeatureUsageRecorder>();

        // Bunit automatically discovers components from referenced assemblies
        // UI.Platform is already referenced in the project, so components should be discoverable
    }

    /// <summary>Trả 500 cho mọi request — NotificationApiClient fail-safe (catch trong UserHeader).</summary>
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
    }
}
