using System.Net;
using System.Text;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Services;
using VanAn.ShopERP.Components.Pages.Admin;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.ShopERP.Tests.Components.Admin;

/// <summary>
/// VOICE SERVICE C3 (task_p7 §C3): bUnit tests cho /admin/voice.
/// T01 render list tenant + toggle + link wallet · T02 non-admin guard (không gọi API) ·
/// T03 Lưu → PUT + alert thành công.
/// </summary>
public class VoiceAdminTests : ComponentTestBase
{
    /// <summary>Stub handler — queue responses + đếm request.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public int RequestCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastUri { get; private set; }

        public void Enqueue(HttpStatusCode code, string json)
            => _responses.Enqueue(new HttpResponseMessage(code)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestCount++;
            LastMethod = request.Method;
            LastUri = request.RequestUri?.ToString();
            return Task.FromResult(_responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                });
        }
    }

    private const string TwoConfigsJson = """
        [
          {"tenantId":"11111111-1111-1111-1111-111111111111","tenantName":"Vạn An Trading","voiceFeatureEnabled":true,"voiceOpenCalling":false,"isDefault":true,"lastChangedAt":null,"lastChangedBy":null},
          {"tenantId":"22222222-2222-2222-2222-222222222222","tenantName":"Cafe Tân Quy","voiceFeatureEnabled":false,"voiceOpenCalling":true,"isDefault":false,"lastChangedAt":"2026-11-11T00:00:00Z","lastChangedBy":"sysadmin"}
        ]
        """;

    private StubHandler RegisterClient(params string[] roles)
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("GatewayClient")).Returns(http);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:BaseUrl"] = "http://localhost" })
            .Build();

        Services.AddSingleton(new VoiceAdminApiClient(
            factory.Object,
            config,
            Mock.Of<IJwtTokenService>(),
            new TestAuthenticationStateProvider(),
            NullLogger<VoiceAdminApiClient>.Instance));

        var auth = new TestAuthenticationStateProvider();
        auth.SetRoles(roles);
        Services.AddSingleton<AuthenticationStateProvider>(auth);
        return handler;
    }

    [Fact]
    public void T01_RendersTenantList_WithTogglesAndWalletLink()
    {
        var handler = RegisterClient("SystemAdmin");
        handler.Enqueue(HttpStatusCode.OK, TwoConfigsJson);

        var cut = RenderComponent<VoiceAdmin>();

        var rows = cut.FindAll("[data-testid='voice-config-row']");
        rows.Should().HaveCount(2);
        cut.Markup.Should().Contain("Vạn An Trading").And.Contain("Cafe Tân Quy");
        // default badge cho tenant chưa cấu hình + badge đã cấu hình
        cut.Markup.Should().Contain("Mặc định").And.Contain("Đã cấu hình");
        // link wallet
        cut.Markup.Should().Contain("/wallet/phi-nen-tang");
        handler.RequestCount.Should().Be(1);
        handler.LastMethod.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public void T02_NonSystemAdmin_SeesGuardMessage_NoApiCall()
    {
        var handler = RegisterClient("Owner");

        var cut = RenderComponent<VoiceAdmin>();

        cut.Markup.Should().Contain("chỉ dành cho SystemAdmin");
        cut.FindAll("[data-testid='voice-config-row']").Should().BeEmpty();
        handler.RequestCount.Should().Be(0); // guard chặn trước cả GET
    }

    [Fact]
    public void T03_Save_SendsPut_AndShowsSuccessAlert()
    {
        var handler = RegisterClient("SystemAdmin");
        handler.Enqueue(HttpStatusCode.OK, TwoConfigsJson); // GET list
        handler.Enqueue(HttpStatusCode.OK, """
            {"tenantId":"22222222-2222-2222-2222-222222222222","tenantName":"Cafe Tân Quy","voiceFeatureEnabled":false,"voiceOpenCalling":true,"isDefault":false,"lastChangedAt":"2026-11-11T00:00:00Z","lastChangedBy":"sysadmin"}
            """); // PUT response

        var cut = RenderComponent<VoiceAdmin>();
        cut.FindAll("[data-testid='voice-config-row']").Should().HaveCount(2);

        cut.FindAll("[data-testid='voice-config-row'] .vanan-button").ElementAt(1).Click(); // nút Lưu của tenant 2

        handler.RequestCount.Should().Be(2);
        handler.LastMethod.Should().Be(HttpMethod.Put);
        handler.LastUri.Should().Contain("api/admin/voice-config/22222222-2222-2222-2222-222222222222");
        cut.Markup.Should().Contain("Đã lưu cấu hình voice cho 'Cafe Tân Quy'");
    }
}