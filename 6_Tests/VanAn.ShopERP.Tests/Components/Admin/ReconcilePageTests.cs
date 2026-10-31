using System.Net;
using System.Text;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Reconcile;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Components.Pages.Admin;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.ShopERP.Tests.Components.Admin;

/// <summary>
/// Tra soát P4 — bUnit cho /admin/reconcile (SRS §8 T-UI).
/// Render form · chạy → summary 4 thẻ + check rows level badge + suggestions.
/// </summary>
public class ReconcilePageTests : ComponentTestBase
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public int RequestCount { get; private set; }
        public string? LastUri { get; private set; }

        public void Enqueue(HttpStatusCode code, string json)
            => _responses.Enqueue(new HttpResponseMessage(code)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestCount++;
            LastUri = request.RequestUri?.ToString();
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("[]", Encoding.UTF8, "application/json") });
        }
    }

    private const string TenantsJson = """
        [{"id":"00000000-0000-0000-0000-0000000000ab","name":"Test Tenant"}]
        """;

    private const string ResultJson = """
        {"period":"2026-10","tenantId":"00000000-0000-0000-0000-0000000000ab","tenantName":"Test Tenant",
         "ranAt":"2026-10-29T10:00:00Z",
         "summary":{"pass":10,"info":2,"warning":1,"error":0,"total":13},
         "checks":[
           {"code":"R1","name":"Journal cân bằng","level":"pass","summary":"0/55 journal lệch","details":""},
           {"code":"R12","name":"Ngày nghiệp vụ","level":"warning","summary":"2/267 entries ngày xấu","details":"id1; id2"}],
         "suggestions":["R12: Entries có TransactionDate xấu — chạy backfill"]}
        """;

    private StubHandler RegisterClients(string? jwtToken = "t", IReconciliationItemService? itemService = null)
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("GatewayClient")).Returns(http);
        var config = new ConfigurationBuilder().Build();
        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<System.Security.Claims.Claim>?>()))
            .Returns(jwtToken!);
        var auth = new TestAuthenticationStateProvider();
        auth.SetRoles("SystemAdmin");
        Services.AddSingleton<AuthenticationStateProvider>(auth);
        Services.AddSingleton(new ReconcileAdminApiClient(factory.Object, config, jwt.Object, auth,
            NullLogger<ReconcileAdminApiClient>.Instance));
        Services.AddSingleton(new TenantApiClient(factory.Object, config, jwt.Object, auth,
            NullLogger<TenantApiClient>.Instance));
        if (itemService != null)
        {
            Services.AddSingleton(itemService);
        }
        return handler;
    }

    [Fact]
    public void T01_RendersForm_TenantSelectPeriodButton()
    {
        var handler = RegisterClients();
        handler.Enqueue(HttpStatusCode.OK, TenantsJson); // TenantApi.ListAllAsync

        var cut = RenderComponent<Reconcile>();

        cut.Find("[data-testid='rc-tenant']").Should().NotBeNull();
        cut.Find("[data-testid='btn-run-reconcile']").Should().NotBeNull();
        cut.Markup.Should().Contain("Tra soát số liệu kế toán");
    }

    [Fact]
    public void T02_Run_ShowsSummaryCardsAndChecks()
    {
        var handler = RegisterClients();
        handler.Enqueue(HttpStatusCode.OK, TenantsJson); // ListAllAsync
        var cut = RenderComponent<Reconcile>();

        // chọn tenant + nhập kỳ
        cut.Find("[data-testid='rc-tenant']").Change("00000000-0000-0000-0000-0000000000ab");
        cut.Find("#rc-period").Input("2026-10");
        handler.Enqueue(HttpStatusCode.OK, ResultJson);

        cut.Find("[data-testid='btn-run-reconcile']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='rc-summary'] div").Should().NotBeEmpty();
            cut.FindAll("[data-testid='rc-check-row']").Should().HaveCount(2);
            cut.FindAll("[data-testid='rc-level-warning']").Should().HaveCount(1);
        }, TimeSpan.FromSeconds(5));
        cut.Markup.Should().Contain("R12: Entries có TransactionDate xấu");
        handler.LastUri.Should().Contain("api/admin/accounting/reconcile?tenantId=00000000-0000-0000-0000-0000000000ab&period=2026-10");
    }

    // ── W5/F (FR-RECNC-005): ReconciliationItems workflow section ─────────────

    [Fact]
    public void T03_Run_LoadsReconciliationItems_WithActions()
    {
        var tenantGuid = Guid.Parse("00000000-0000-0000-0000-0000000000ab");
        var item = new ReconciliationItemEntity(new TenantId(tenantGuid), "R12", 2026, 10,
            "warning", "R12 Ngày nghiệp vụ: 2/267 entries ngày xấu");
        var items = new Mock<IReconciliationItemService>();
        items.Setup(s => s.ListAsync(It.IsAny<TenantId>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        var handler = RegisterClients(itemService: items.Object);
        handler.Enqueue(HttpStatusCode.OK, TenantsJson); // ListAllAsync
        var cut = RenderComponent<Reconcile>();

        cut.Find("[data-testid='rc-tenant']").Change("00000000-0000-0000-0000-0000000000ab");
        cut.Find("#rc-period").Input("2026-10");
        handler.Enqueue(HttpStatusCode.OK, ResultJson);

        cut.Find("[data-testid='btn-run-reconcile']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='rc-item-row']").Should().HaveCount(1);
            cut.Markup.Should().Contain("R12 Ngày nghiệp vụ");
            // Item Open → có actions assign + resolve + dismiss
            cut.Find("[data-testid='rc-item-assign-input']").Should().NotBeNull();
            cut.Find("[data-testid='rc-item-resolve']").Should().NotBeNull();
            cut.Find("[data-testid='rc-item-dismiss']").Should().NotBeNull();
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void T04_ResolvedItem_HidesActions()
    {
        var tenantGuid = Guid.Parse("00000000-0000-0000-0000-0000000000ab");
        var item = new ReconciliationItemEntity(new TenantId(tenantGuid), "R1", 2026, 10,
            "error", "R1 JE balance: 1 JE lệch");
        item.Resolve("admin", "đã sửa bút toán");
        var items = new Mock<IReconciliationItemService>();
        items.Setup(s => s.ListAsync(It.IsAny<TenantId>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        var handler = RegisterClients(itemService: items.Object);
        handler.Enqueue(HttpStatusCode.OK, TenantsJson);
        var cut = RenderComponent<Reconcile>();

        cut.Find("[data-testid='rc-tenant']").Change("00000000-0000-0000-0000-0000000000ab");
        handler.Enqueue(HttpStatusCode.OK, ResultJson);
        cut.Find("[data-testid='btn-run-reconcile']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Resolved");
            cut.Markup.Should().Contain("đã sửa bút toán");
            cut.FindAll("[data-testid='rc-item-resolve']").Should().BeEmpty();
        }, TimeSpan.FromSeconds(5));
    }
}
