using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.ShopERP.Components.Pages.Admin;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.ShopERP.Tests.Components.Admin;

/// <summary>
/// Phí nền tảng P6 (task_p6_admin_ui §5 T01–T03): bUnit tests cho /admin/metering/rate-cards.
/// T01 render list · T02 non-admin guard · T03 client-side validation không gọi API.
/// </summary>
public class MeteringRateCardsTests : ComponentTestBase
{
    /// <summary>Stub handler — queue responses theo thứ tự + đếm request (T03 assert không gọi POST).</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public int RequestCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }

        public void Enqueue(HttpStatusCode code, string json)
            => _responses.Enqueue(new HttpResponseMessage(code)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestCount++;
            LastMethod = request.Method;
            return Task.FromResult(_responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                });
        }
    }

    private const string TwoCardsJson = """
        [
          {"id":"11111111-1111-1111-1111-111111111111","serviceCode":"store.visit","meterUnit":"visit","creditRate":1.0,"enforcement":"MeterOnly","effectiveFrom":"2026-10-23T00:00:00Z","effectiveTo":null,"isActive":true,"label":"Lượt xem cửa hàng","changedBy":"sysadmin","changedAtUtc":"2026-10-23T00:00:00Z","auditNote":null,"scope":"Global","scopeValue":null},
          {"id":"22222222-2222-2222-2222-222222222222","serviceCode":"chat.message","meterUnit":"message","creditRate":2.5,"enforcement":"Deny","effectiveFrom":"2026-10-23T00:00:00Z","effectiveTo":null,"isActive":true,"label":null,"changedBy":"sysadmin","changedAtUtc":"2026-10-23T00:00:00Z","auditNote":null,"scope":"TenantType","scopeValue":"HKD"}
        ]
        """;

    // Issue #197 — GET /v1/config/enforcement response (page tải switch khi mở).
    private const string EnforcementOnJson =
        """{"enabled":true,"updatedBy":"sysadmin","updatedAtUtc":"2026-10-23T00:00:00Z"}""";

    private StubHandler RegisterClient(params string[] roles)
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/api/") };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Metering:AdminCallerId"] = "sysadmin",
                ["Metering:AdminKey"] = "test-admin-key"
            })
            .Build();
        Services.AddSingleton(new MeteringRateCardApiClient(http, config, NullLogger<MeteringRateCardApiClient>.Instance));

        // Ghi đè auth provider của base bằng provider có roles (SetRoles).
        var auth = new TestAuthenticationStateProvider();
        auth.SetRoles(roles);
        Services.AddSingleton<AuthenticationStateProvider>(auth);
        return handler;
    }

    [Fact]
    public void T01_RendersRateCardTable_WithHeadVersions()
    {
        var handler = RegisterClient("SystemAdmin");
        handler.Enqueue(HttpStatusCode.OK, EnforcementOnJson); // switch (issue #197)
        handler.Enqueue(HttpStatusCode.OK, TwoCardsJson);

        var cut = RenderComponent<MeteringRateCards>();

        var rows = cut.FindAll("[data-testid='rc-row']");
        rows.Should().HaveCount(2);
        cut.Markup.Should().Contain("store.visit").And.Contain("chat.message");
        cut.Markup.Should().Contain("MeterOnly").And.Contain("Deny");
        // Issue #197 — scope badge hiển thị
        cut.Markup.Should().Contain("Global").And.Contain("Loại hình: HKD");
        cut.FindAll("[data-testid='btn-edit-rc']").Should().HaveCount(2);
        handler.RequestCount.Should().Be(2); // GET list + GET enforcement switch
    }

    [Fact]
    public void T02_NonSystemAdmin_SeesGuardMessage_NoApiCall()
    {
        var handler = RegisterClient("Owner");

        var cut = RenderComponent<MeteringRateCards>();

        cut.Markup.Should().Contain("chỉ dành cho SystemAdmin");
        cut.FindAll("[data-testid='rc-table']").Should().BeEmpty();
        handler.RequestCount.Should().Be(0); // guard chặn trước cả GET list
    }

    [Fact]
    public void T03_SaveWithEmptyServiceCode_ShowsValidation_NoPostCall()
    {
        var handler = RegisterClient("SystemAdmin");
        handler.Enqueue(HttpStatusCode.OK, EnforcementOnJson); // switch (issue #197)
        handler.Enqueue(HttpStatusCode.OK, "[]"); // GET list rỗng

        var cut = RenderComponent<MeteringRateCards>();
        cut.Markup.Should().Contain("Chưa có hạng mục"); // empty state

        cut.Find("[data-testid='btn-save-rc']").Click();

        cut.Markup.Should().Contain("ServiceCode không được để trống");
        handler.RequestCount.Should().Be(2); // GET list + GET enforcement — validation client-side chặn POST
        handler.LastMethod.Should().Be(HttpMethod.Get);
    }
}
