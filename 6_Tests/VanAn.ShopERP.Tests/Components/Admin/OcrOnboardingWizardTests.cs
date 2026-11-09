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
        // P2: wizard render OnboardingFlagsCard + QrSignupCard → cần flags client (GET fail → card ẩn, an toàn)
        ctx.Services.AddSingleton(new OnboardingFlagsApiClient(
            httpFactory.Object,
            new ConfigurationBuilder().Build(),
            Mock.Of<IJwtTokenService>(),
            new TestAuthenticationStateProvider(),
            NullLogger<OnboardingFlagsApiClient>.Instance));
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

    // ── P3 (2026-11-09): HKDGroup helper + unregistered + photo provenance ──

    [Fact(DisplayName = "Wizard_CaseB_Shows_HkdGroupHelper_And_UnregisteredCheckbox")]
    public void Wizard_CaseB_Shows_HkdGroupHelper_And_UnregisteredCheckbox()
    {
        RegisterWizardServices(this);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<OcrOnboardingWizard>();

        // Chọn Case B → Tiếp theo → step 1 (Giấy tờ)
        cut.FindAll("button.ocr-case-card").Skip(1).First().Click();
        cut.FindAll("button").First(b => b.TextContent.Contains("Tiếp theo")).Click();

        cut.Markup.Should().Contain("Doanh thu ước tính mỗi tháng");   // helper gợi ý
        cut.Markup.Should().Contain("CHƯA đăng ký");                  // checkbox unregistered
        cut.Markup.Should().Contain("không bắt buộc nếu kinh doanh lưu động"); // địa chỉ optional
    }

    [Fact(DisplayName = "Wizard_CaseB_Unregistered_HidesHelper_ShowsGroup1Hint")]
    public void Wizard_CaseB_Unregistered_HidesHelper_ShowsGroup1Hint()
    {
        RegisterWizardServices(this);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<OcrOnboardingWizard>();

        cut.FindAll("button.ocr-case-card").Skip(1).First().Click();
        cut.FindAll("button").First(b => b.TextContent.Contains("Tiếp theo")).Click();

        var checkbox = cut.Find("input[type=checkbox]");
        checkbox.Change(true);

        cut.Markup.Should().Contain("Nhóm 1");
        cut.Markup.Should().NotContain("Doanh thu ước tính mỗi tháng");
    }

    [Fact(DisplayName = "Wizard_OnOcrResult_Collects_PhotoUrls_Into_Strip")]
    public async Task Wizard_OnOcrResult_Collects_PhotoUrls_Into_Strip()
    {
        RegisterWizardServices(this);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<OcrOnboardingWizard>();

        await cut.InvokeAsync(() => cut.Instance.OnOcrResult(
            "extractGpdk", "{}", "[\"https://cdn.example.com/gpkd1.jpg\",\"https://cdn.example.com/gpkd2.jpg\"]"));

        cut.Markup.Should().Contain("ocr-photos");
        cut.Markup.Should().Contain("https://cdn.example.com/gpkd1.jpg");
        cut.Markup.Should().Contain("GPKD");

        // Xóa ảnh → strip biến mất
        cut.Find(".ocr-photo-remove").Click();
        cut.FindAll(".ocr-photo-remove").Count.Should().Be(1);
        cut.Find(".ocr-photo-remove").Click();
        cut.Markup.Should().NotContain("ocr-photos-strip");
    }

    [Fact(DisplayName = "Wizard_OnMapClick_UpdatesCoords_NoCrash")]
    public async Task Wizard_OnMapClick_UpdatesCoords_NoCrash()
    {
        RegisterWizardServices(this);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderComponent<OcrOnboardingWizard>();

        // JSInvokable từ map click — không throw, coords ngoài range bị bỏ qua
        await cut.InvokeAsync(() => cut.Instance.OnMapClick(10.7769, 106.7009));
        await cut.InvokeAsync(() => cut.Instance.OnMapClick(999, 0));   // invalid → ignored
    }

    /// <summary>Trả 500 cho mọi request — wizard không gọi API khi mới render.</summary>
    private sealed class Stub500Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
    }
}