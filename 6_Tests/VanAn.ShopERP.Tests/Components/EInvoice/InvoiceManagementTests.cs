using System.Reflection;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Orchestration;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP.Services;
using VanAn.ShopERP.Tests.Components;

namespace VanAn.ShopERP.Tests.Components.EInvoice;

/// <summary>
/// W3 (P0.3 — Điều 10 TT 91/2026): bUnit tests for InvoiceManagement.razor —
/// điều chỉnh/thay thế hóa đơn điện tử.
///
/// NOTE: @rendermode InteractiveServer prevents bUnit click wiring (same constraint as
/// PeriodClosingTests). Modal coverage uses reflection to set private fields + re-render.
///
/// Covers: real list loading via IEInvoiceOrchestrator.ListInvoicesAsync, adjust/replace
/// action visibility (TaxApproved only), status badges (ĐC/TT linkage), adjust modal
/// markup + legal note, status filter options.
/// </summary>
[Trait("Category", "EInvoiceUI")]
public class InvoiceManagementTests : ComponentTestBase
{
    private static readonly Guid TestTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly TenantId TestTenantId = new(TestTenantGuid);

    public InvoiceManagementTests()
    {
        // TenantFilterDropdown injects TenantApiClient (sealed) — real instance + stub HTTP 500.
        var httpClient = new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://localhost") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        Services.AddSingleton(new TenantApiClient(
            factory.Object,
            new ConfigurationBuilder().Build(),
            Mock.Of<IJwtTokenService>(),
            new TestAuthenticationStateProvider(),
            NullLogger<TenantApiClient>.Instance));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
    }

    private static ElectronicInvoice MakeInvoice(
        InvoiceStatus targetStatus,
        InvoiceAdjustmentType? adjustmentType = null,
        ElectronicInvoice? original = null,
        string providerNo = "VT-001")
    {
        var inv = new ElectronicInvoice(
            TestTenantId, new OrderId(Guid.NewGuid()),
            new InvoiceIdempotencyKey(Guid.NewGuid().ToString("N")),
            InvoiceType.Goods, 100_000m, 10_000m, 110_000m,
            "Khách A", "0123456789", "Hà Nội");

        if (adjustmentType is not null && original is not null)
            inv.MarkAsAdjustmentOf(original, adjustmentType.Value, "Lý do test");

        switch (targetStatus)
        {
            case InvoiceStatus.PendingSend:
                inv.Submit();
                break;
            case InvoiceStatus.SentToProvider:
                inv.Submit();
                inv.MarkAsSentToProvider(new ProviderId("viettel"));
                break;
            case InvoiceStatus.TaxApproved:
                inv.Submit();
                inv.MarkAsSentToProvider(new ProviderId("viettel"));
                inv.MarkAsTaxApproved(providerNo);
                break;
            case InvoiceStatus.Adjusted:
                inv.Submit();
                inv.MarkAsSentToProvider(new ProviderId("viettel"));
                inv.MarkAsTaxApproved(providerNo);
                inv.MarkAsAdjusted();
                break;
            case InvoiceStatus.Replaced:
                inv.Submit();
                inv.MarkAsSentToProvider(new ProviderId("viettel"));
                inv.MarkAsTaxApproved(providerNo);
                inv.MarkAsReplaced();
                break;
            // Draft = default constructor state
        }
        return inv;
    }

    private Mock<IEInvoiceOrchestrator> SetupOrchestrator(IReadOnlyList<ElectronicInvoice> invoices)
    {
        var mock = new Mock<IEInvoiceOrchestrator>();
        mock.Setup(o => o.ListInvoicesAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);
        return mock;
    }

    private IRenderedComponent<ShopERP.Components.Pages.EInvoice.InvoiceManagement> RenderPage(
        IReadOnlyList<ElectronicInvoice> invoices, out Mock<IEInvoiceOrchestrator> mock)
    {
        mock = SetupOrchestrator(invoices);
        Services.AddSingleton(mock.Object);
        return RenderComponent<ShopERP.Components.Pages.EInvoice.InvoiceManagement>();
    }

    private static void SetPrivateField<T>(
        IRenderedComponent<ShopERP.Components.Pages.EInvoice.InvoiceManagement> cut,
        string fieldName, T value)
    {
        typeof(ShopERP.Components.Pages.EInvoice.InvoiceManagement)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, value);
        cut.Render();
    }

    [Fact(DisplayName = "W3-UI-1: Page loads invoice list via ListInvoicesAsync")]
    public void Page_Loads_Invoices_ViaOrchestrator()
    {
        var inv = MakeInvoice(InvoiceStatus.TaxApproved);

        var cut = RenderPage(new List<ElectronicInvoice> { inv }, out var mock);

        mock.Verify(o => o.ListInvoicesAsync(
            It.Is<TenantId>(t => t == TestTenantId), It.IsAny<CancellationToken>()), Times.Once);
        cut.Markup.Should().Contain("Khách A");
        cut.Markup.Should().Contain("VT-001");
    }

    [Fact(DisplayName = "W3-UI-2: TaxApproved invoice shows Điều Chỉnh + Thay Thế actions")]
    public void TaxApprovedInvoice_Shows_Adjust_Replace_Buttons()
    {
        var inv = MakeInvoice(InvoiceStatus.TaxApproved);

        var cut = RenderPage(new List<ElectronicInvoice> { inv }, out _);

        cut.Markup.Should().Contain("btn-adjust-invoice");
        cut.Markup.Should().Contain("btn-replace-invoice");
    }

    [Fact(DisplayName = "W3-UI-3: Draft invoice has NO adjust/replace actions")]
    public void DraftInvoice_Hides_Adjust_Replace_Buttons()
    {
        var inv = MakeInvoice(InvoiceStatus.Draft);

        var cut = RenderPage(new List<ElectronicInvoice> { inv }, out _);

        cut.Markup.Should().NotContain("btn-adjust-invoice");
        cut.Markup.Should().NotContain("btn-replace-invoice");
        cut.Markup.Should().Contain("btn-submit");
    }

    [Fact(DisplayName = "W3-UI-4: Adjusted original shows 'Đã Điều Chỉnh' badge")]
    public void AdjustedInvoice_Shows_Adjusted_Status_Label()
    {
        var inv = MakeInvoice(InvoiceStatus.Adjusted);

        var cut = RenderPage(new List<ElectronicInvoice> { inv }, out _);

        cut.Markup.Should().Contain("Đã Điều Chỉnh");
        // Original đã điều chỉnh → không cho điều chỉnh lại
        cut.Markup.Should().NotContain("btn-adjust-invoice");
    }

    [Fact(DisplayName = "W3-UI-5: Adjustment invoice shows ĐC linkage badge")]
    public void AdjustmentInvoice_Shows_Linkage_Badge()
    {
        var original = MakeInvoice(InvoiceStatus.Adjusted);
        var adjustment = MakeInvoice(
            InvoiceStatus.PendingSend, InvoiceAdjustmentType.Adjust, original);

        var cut = RenderPage(new List<ElectronicInvoice> { original, adjustment }, out _);

        cut.Markup.Should().Contain("badge-adjusted-invoice");
    }

    [Fact(DisplayName = "W3-UI-6: Adjust modal renders reason field + legal note")]
    public void AdjustModal_Renders_Reason_And_LegalNote()
    {
        var inv = MakeInvoice(InvoiceStatus.TaxApproved);
        var cut = RenderPage(new List<ElectronicInvoice> { inv }, out _);

        SetPrivateField(cut, "adjustInvoiceNo", "VT-001");
        SetPrivateField(cut, "adjustType", InvoiceAdjustmentType.Adjust);
        SetPrivateField(cut, "showAdjustModal", true);

        cut.Markup.Should().Contain("adjust-modal");
        cut.Markup.Should().Contain("adjust-reason-input");
        cut.Markup.Should().Contain("adjust-legal-note");
        cut.Markup.Should().Contain("Điều 10 TT 91/2026");
        cut.Markup.Should().Contain("kê khai ở kỳ phát sinh");
    }

    [Fact(DisplayName = "W3-UI-7: Replace modal title shows Thay Thế")]
    public void ReplaceModal_Shows_Replace_Title()
    {
        var inv = MakeInvoice(InvoiceStatus.TaxApproved);
        var cut = RenderPage(new List<ElectronicInvoice> { inv }, out _);

        SetPrivateField(cut, "adjustInvoiceNo", "VT-001");
        SetPrivateField(cut, "adjustType", InvoiceAdjustmentType.Replace);
        SetPrivateField(cut, "showAdjustModal", true);

        cut.Markup.Should().Contain("Thay Thế Hóa Đơn");
    }

    [Fact(DisplayName = "W3-UI-8: Status filter includes Adjusted/Replaced options")]
    public void StatusFilter_Includes_Adjusted_Replaced_Options()
    {
        var cut = RenderPage(new List<ElectronicInvoice>(), out _);

        cut.Markup.Should().Contain("value=\"Adjusted\"");
        cut.Markup.Should().Contain("value=\"Replaced\"");
        cut.Markup.Should().Contain("value=\"TaxApproved\"");
    }
}
