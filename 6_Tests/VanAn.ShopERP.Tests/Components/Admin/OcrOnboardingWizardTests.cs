using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Components.Pages.Admin;
using VanAn.ShopERP.Services;
using VanAn.ShopERP.Tests.Components;
using Xunit;

namespace VanAn.ShopERP.Tests.Components.Admin;

/// <summary>
/// OCR Onboarding S4 (2026-10-25): smoke test wizard render (step 0 — chọn case).
/// Wizard JS-heavy (OCR/GPS) — logic runtime validate qua E2E + RV; ở đây chỉ
/// đảm bảo component render được + 2 case card hiển thị.
/// </summary>
public class OcrOnboardingWizardTests : ComponentTestBase
{
    private static void RegisterWizardServices(TestContext ctx)
    {
        // OcrOnboardingApiClient (concrete) — stub GatewayClient trả 500 (không gọi trong smoke test)
        var httpClient = new HttpClient(new Stub500Handler()) { BaseAddress = new Uri("http://localhost") };
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("GatewayClient")).Returns(httpClient);
        ctx.Services.AddSingleton(httpFactory.Object);
        ctx.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        ctx.Services.AddSingleton(Mock.Of<IJwtTokenService>());
        ctx.Services.AddSingleton(new TestAuthenticationStateProvider());
        ctx.Services.AddSingleton(new OcrOnboardingApiClient(
            httpFactory.Object,
            new ConfigurationBuilder().Build(),
            Mock.Of<IJwtTokenService>(),
            new TestAuthenticationStateProvider(),
            NullLogger<OcrOnboardingApiClient>.Instance));
    }

    [Fact(DisplayName = "Wizard_Renders_Step0_CaseCards")]
    public void Wizard_Renders_Step0_CaseCards()
    {
        RegisterWizardServices(this);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<OcrOnboardingWizard>();

        cut.Markup.Should().Contain("Công ty / Hộ kinh doanh");
        cut.Markup.Should().Contain("Shop nhỏ");
        cut.Markup.Should().Contain("1. Loại hình");
    }

    /// <summary>Trả 500 cho mọi request — wizard không gọi API khi mới render.</summary>
    private sealed class Stub500Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
    }
}