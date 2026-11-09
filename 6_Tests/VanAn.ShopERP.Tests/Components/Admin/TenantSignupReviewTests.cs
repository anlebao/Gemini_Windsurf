using System.Net;
using System.Text;
using Bunit;
using FluentAssertions;
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
/// Self-Serve Onboarding P2 (2026-11-09): bUnit tests cho tab Pending trên
/// /admin/tenants — badge nguồn self-serve, modal hồ sơ (signup-detail),
/// verify-signup submit + reject-pending. Gateway API stub qua IHttpClientFactory.
/// </summary>
public class TenantSignupReviewTests : ComponentTestBase
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ShopInstanceId = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private void RegisterServices(StubGatewayHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("GatewayClient")).Returns(httpClient);
        Services.AddSingleton(httpFactory.Object);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(Mock.Of<IJwtTokenService>());
        var authProvider = new TestAuthenticationStateProvider();
        Services.AddSingleton<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(authProvider);

        Services.AddSingleton(new TenantClaimApiClient(
            httpFactory.Object, new ConfigurationBuilder().Build(), Mock.Of<IJwtTokenService>(),
            authProvider, NullLogger<TenantClaimApiClient>.Instance));
        Services.AddSingleton(new TenantOnboardingApiClient(
            httpFactory.Object, new ConfigurationBuilder().Build(), Mock.Of<IJwtTokenService>(),
            authProvider, NullLogger<TenantOnboardingApiClient>.Instance));
        Services.AddSingleton(new ShopInstanceApiClient(
            httpFactory.Object, new ConfigurationBuilder().Build(), Mock.Of<IJwtTokenService>(),
            authProvider, NullLogger<ShopInstanceApiClient>.Instance));
        Services.AddSingleton(new TenantApiClient(
            httpFactory.Object, new ConfigurationBuilder().Build(), Mock.Of<IJwtTokenService>(),
            authProvider, NullLogger<TenantApiClient>.Instance));
        Services.AddSingleton(new MembershipAdminApiClient(
            httpFactory.Object, new ConfigurationBuilder().Build(), Mock.Of<IJwtTokenService>(),
            authProvider, NullLogger<MembershipAdminApiClient>.Instance));
        Services.AddSingleton(new OcrOnboardingApiClient(
            httpFactory.Object, new ConfigurationBuilder().Build(), Mock.Of<IJwtTokenService>(),
            authProvider, NullLogger<OcrOnboardingApiClient>.Instance));
    }

    private static string PendingJson(bool selfServe = true) => $$"""
        [{
            "id": "{{TenantId}}",
            "name": "Cà Phê Sáng",
            "taxCode": null,
            "address": "12 Lê Lợi, Q1",
            "potentialDuplicateOf": null,
            "createdAt": "2026-11-08T10:00:00Z",
            "sourceSite": "OCR-SELF-SIGN",
            "hasSignupPayload": {{(selfServe ? "true" : "false")}},
            "signupCase": "SmallShopNoLicense",
            "signupSrc": "qr",
            "productCount": 3,
            "unregistered": false,
            "otpVerified": false,
            "duplicateWarning": true
        }]
        """;

    private static string DetailJson() => $$"""
        {
            "tenantId": "{{TenantId}}",
            "name": "Cà Phê Sáng",
            "status": 5,
            "taxCode": null,
            "address": "12 Lê Lợi, Q1",
            "businessType": 2,
            "hkdGroup": 1,
            "createdAt": "2026-11-08T10:00:00Z",
            "hasSelfServePayload": true,
            "latitude": 10.77,
            "longitude": 106.7,
            "selfServe": {
                "signupCase": "SmallShopNoLicense",
                "src": "qr",
                "productLines": [
                    { "name": "Cà phê sữa đá", "price": 25000, "lineNo": 1 },
                    { "name": "Bạc xỉu", "price": 20000, "lineNo": 2 }
                ],
                "contactName": "Chị Lan",
                "contactPhone": "0901234567",
                "contactEmail": null,
                "ownerUsername": "caphesang",
                "otpVerified": false,
                "unregistered": false,
                "duplicateWarning": true,
                "signName": "Cà Phê Sáng",
                "gpdkInfo": null,
                "photoUrls": ["https://example.com/gpkd1.jpg"],
                "submittedAt": "2026-11-08T09:00:00Z"
            }
        }
        """;

    private static string ShopInstancesJson() => $$"""
        [{
            "id": "{{ShopInstanceId}}",
            "baseUrl": "https://shoperp-a.example.com",
            "label": "ShopERP-A",
            "maxTenants": 50,
            "isActive": true,
            "healthStatus": "Healthy",
            "tenantCount": 3,
            "createdAt": "2026-01-01T00:00:00Z"
        }]
        """;

    [Fact(DisplayName = "PendingTab_Renders_SelfServeBadge_And_HoSoButton")]
    public async Task PendingTab_Renders_SelfServeBadge_And_HoSoButton()
    {
        var handler = new StubGatewayHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/v1/tenants/pending")) return Json(HttpStatusCode.OK, PendingJson());
            if (path.EndsWith("/api/v1/tenants")) return Json(HttpStatusCode.OK, "[]");
            if (path.EndsWith("/api/v1/shop-instances")) return Json(HttpStatusCode.OK, ShopInstancesJson());
            return Json(HttpStatusCode.NotFound, "{}");
        });
        RegisterServices(handler);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<TenantManagement>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Quản lý Tenant"));

        // Click tab Pending
        cut.FindAll("button").First(b => b.TextContent.Contains("Pending")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Self-serve"));

        cut.Markup.Should().Contain("Hồ sơ");
        cut.Markup.Should().Contain("Cà Phê Sáng");
        cut.Markup.Should().Contain("qr"); // signupSrc
        cut.Markup.Should().Contain("3 SP menu");
    }

    [Fact(DisplayName = "SignupDetail_Modal_Renders_Info_Menu_Flags_NoPassword")]
    public async Task SignupDetail_Modal_Renders_Info_Menu_Flags_NoPassword()
    {
        var handler = new StubGatewayHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith($"/api/v1/tenants/{TenantId}/signup-detail")) return Json(HttpStatusCode.OK, DetailJson());
            if (path.EndsWith("/api/v1/tenants/pending")) return Json(HttpStatusCode.OK, PendingJson());
            if (path.EndsWith("/api/v1/tenants")) return Json(HttpStatusCode.OK, "[]");
            if (path.EndsWith("/api/v1/shop-instances")) return Json(HttpStatusCode.OK, ShopInstancesJson());
            return Json(HttpStatusCode.NotFound, "{}");
        });
        RegisterServices(handler);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<TenantManagement>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Quản lý Tenant"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Pending")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Hồ sơ"));

        // Mở modal hồ sơ
        cut.FindAll("button").First(b => b.TextContent.Contains("Hồ sơ")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Chị Lan"));

        cut.Markup.Should().Contain("caphesang");           // ownerUsername — KHÔNG password
        cut.Markup.Should().Contain("Cà phê sữa đá");        // menu OCR
        cut.Markup.Should().Contain("Bạc xỉu");
        cut.Markup.Should().Contain("Menu OCR (2 món)");
        cut.Markup.Should().Contain("Tên trùng tenant khác"); // duplicateWarning badge
        cut.Markup.Should().Contain("Duyệt &amp; Kích hoạt");
        cut.Markup.Should().NotContain("OwnerPasswordProtected");
        cut.Markup.Should().NotContain("passwordProtected");
    }

    [Fact(DisplayName = "VerifySignup_Submit_Calls_Api_And_Shows_Result")]
    public async Task VerifySignup_Submit_Calls_Api_And_Shows_Result()
    {
        var verifyCalled = false;
        string? verifyBody = null;
        var handler = new StubGatewayHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith($"/api/v1/tenants/{TenantId}/signup-detail")) return Json(HttpStatusCode.OK, DetailJson());
            if (path.EndsWith($"/api/v1/tenants/{TenantId}/verify-signup") && req.Method == HttpMethod.Post)
            {
                verifyCalled = true;
                verifyBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json(HttpStatusCode.OK, $$"""
                    { "tenantId": "{{TenantId}}", "slug": "ca-phe-sang", "ownerUsername": "caphesang", "usedMerchantPassword": true }
                    """);
            }
            if (path.EndsWith("/api/v1/tenants/pending")) return Json(HttpStatusCode.OK, PendingJson());
            if (path.EndsWith("/api/v1/tenants")) return Json(HttpStatusCode.OK, "[]");
            if (path.EndsWith("/api/v1/shop-instances")) return Json(HttpStatusCode.OK, ShopInstancesJson());
            return Json(HttpStatusCode.NotFound, "{}");
        });
        RegisterServices(handler);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<TenantManagement>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Quản lý Tenant"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Pending")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Hồ sơ"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Hồ sơ")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("caphesang"));

        // Vào form duyệt
        cut.FindAll("button").First(b => b.TextContent.Contains("Duyệt")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("ShopERP Instance"));

        // Chọn instance duy nhất + submit (mặc định dùng mật khẩu merchant)
        cut.Find("select#signup-shopinstance").Change(ShopInstanceId.ToString());
        await cut.InvokeAsync(() => cut.FindAll("button")
            .First(b => b.TextContent.Contains("Duyệt") && b.TextContent.Contains("Kích hoạt")).Click());

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("ca-phe-sang"));
        verifyCalled.Should().BeTrue();
        verifyBody.Should().Contain("shopInstanceId");
        verifyBody.Should().Contain("\"ownerPassword\":null"); // dùng mật khẩu merchant → null
    }

    [Fact(DisplayName = "VerifySignup_409_Shows_DuplicateGuidance")]
    public async Task VerifySignup_409_Shows_DuplicateGuidance()
    {
        var handler = new StubGatewayHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith($"/api/v1/tenants/{TenantId}/signup-detail")) return Json(HttpStatusCode.OK, DetailJson());
            if (path.EndsWith($"/api/v1/tenants/{TenantId}/verify-signup") && req.Method == HttpMethod.Post)
                return Json(HttpStatusCode.Conflict, """{"error":"Tenant bị chặn duplicate — resolve duplicate trước khi verify."}""");
            if (path.EndsWith("/api/v1/tenants/pending")) return Json(HttpStatusCode.OK, PendingJson());
            if (path.EndsWith("/api/v1/tenants")) return Json(HttpStatusCode.OK, "[]");
            if (path.EndsWith("/api/v1/shop-instances")) return Json(HttpStatusCode.OK, ShopInstancesJson());
            return Json(HttpStatusCode.NotFound, "{}");
        });
        RegisterServices(handler);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<TenantManagement>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Quản lý Tenant"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Pending")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Hồ sơ"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Hồ sơ")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("caphesang"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Duyệt")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("ShopERP Instance"));

        cut.Find("select#signup-shopinstance").Change(ShopInstanceId.ToString());
        await cut.InvokeAsync(() => cut.FindAll("button")
            .First(b => b.TextContent.Contains("Duyệt") && b.TextContent.Contains("Kích hoạt")).Click());

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("resolve duplicate"));
    }

    [Fact(DisplayName = "RejectPending_Calls_Api_With_Reason")]
    public async Task RejectPending_Calls_Api_With_Reason()
    {
        string? rejectBody = null;
        var handler = new StubGatewayHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith($"/api/v1/tenants/{TenantId}/reject-pending") && req.Method == HttpMethod.Post)
            {
                rejectBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (path.EndsWith("/api/v1/tenants/pending")) return Json(HttpStatusCode.OK, PendingJson());
            if (path.EndsWith("/api/v1/tenants")) return Json(HttpStatusCode.OK, "[]");
            if (path.EndsWith("/api/v1/shop-instances")) return Json(HttpStatusCode.OK, ShopInstancesJson());
            return Json(HttpStatusCode.NotFound, "{}");
        });
        RegisterServices(handler);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<TenantManagement>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Quản lý Tenant"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Pending")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Cà Phê Sáng"));

        // Nút từ chối (icon X) trên row
        var rejectButtons = cut.FindAll("button").Where(b => b.QuerySelector(".bi-x-circle") is not null).ToList();
        rejectButtons.Should().NotBeEmpty();
        rejectButtons.First().Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Từ chối tenant"));

        // Nhập lý do + confirm
        cut.Find("textarea").Change("Thông tin không hợp lệ");
        await cut.InvokeAsync(() => cut.FindAll("button")
            .First(b => b.TextContent.Contains("Xác nhận từ chối")).Click());

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("đã bị từ chối"));
        rejectBody.Should().Contain("\"reason\"");
    }

    // ── Stub Gateway HTTP ──────────────────────────────────────────────────

    private static HttpResponseMessage Json(HttpStatusCode code, string body)
        => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubGatewayHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
