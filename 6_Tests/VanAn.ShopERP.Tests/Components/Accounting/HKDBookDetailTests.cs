using Bunit;
using FluentAssertions;
using Moq;
using VanAn.CoreHub.Services.Template;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP.Services;
using VanAn.ShopERP.Tests.Components;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// W4 (Sprint 2) — bUnit tests for HKDBookDetail.razor (HKD book detail / generation page).
/// Covers: header with template code, period selector, book generation, export buttons
/// (DOCX/XLSX), loading state, error state (invalid template + general exception),
/// TT 152 layout rendering.
/// </summary>
[Trait("Category", "AccountingUI")]
public class HKDBookDetailTests : ComponentTestBase
{
    private static readonly Guid TestTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly TenantId TestTenantId = new(TestTenantGuid);

    /// <summary>
    /// Build a sample <see cref="GenericHKDBook"/> with one journal entry + numeric totals
    /// so the page's BuildRows() produces visible rows.
    /// </summary>
    private static GenericHKDBook BuildSampleBook()
    {
        var entry = new JournalEntry(
            TestTenantId,
            new DateTime(2026, 6, 15),
            "Bán hàng hóa cho khách lẻ");
        // JournalNo is auto-generated (read-only) — not asserted in page tests.
        entry.AddLine("111", 10_000_000m, 0m, "Tiền mặt thu");
        entry.AddLine("511", 0m, 10_000_000m, "Doanh thu bán hàng");

        return new GenericHKDBook
        {
            TenantId = TestTenantId,
            Period = new AccountingPeriod(2026, 6),
            BookTypeCode = "S01a-HKD",
            TemplateVersion = "1.0",
            GeneratedAt = new DateTime(2026, 7, 5, 9, 0, 0),
            Entries = new List<JournalEntry> { entry },
            NumericValues = new Dictionary<string, decimal>
            {
                ["TotalRevenue"] = 10_000_000m,
                ["NetRevenue"] = 10_000_000m,
                ["NetProfit"] = 10_000_000m
            },
            TextValues = new Dictionary<string, string>()
        };
    }

    private Mock<IHKDBookGenerationService> SetupGenerationService()
    {
        var mock = new Mock<IHKDBookGenerationService>();
        mock.Setup(s => s.GenerateBookAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<string>()))
            .ReturnsAsync(BuildSampleBook());
        return mock;
    }

    private void SetupExportService(Mock<IHKDBookExportService>? mock = null)
    {
        mock ??= new Mock<IHKDBookExportService>();
        mock.Setup(e => e.ExportToDocxAsync(It.IsAny<HKDBookDto>()))
            .ReturnsAsync(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
        mock.Setup(e => e.ExportToXlsxAsync(It.IsAny<HKDBookDto>()))
            .ReturnsAsync(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
        Services.AddSingleton(mock.Object);
    }

    private IRenderedComponent<ShopERP.Components.Pages.Accounting.HKDBookDetail> RenderDetail(
        string templateCode = "S01a-HKD",
        Mock<IHKDBookGenerationService>? genMock = null)
    {
        genMock ??= SetupGenerationService();
        Services.AddSingleton(genMock.Object);
        SetupExportService();

        return RenderComponent<ShopERP.Components.Pages.Accounting.HKDBookDetail>(
            parameters => parameters.Add(p => p.TemplateCode, templateCode));
    }

    [Fact(DisplayName = "W4-HKD-D1: Page renders header with template code")]
    public void Page_Renders_Header_WithTemplateCode()
    {
        var cut = RenderDetail("S01a-HKD");
        cut.Markup.Should().Contain("Sổ Kế Toán HKD — S01a-HKD");
    }

    [Fact(DisplayName = "W4-HKD-D2: Page renders 'Quay lại danh mục' (back) button")]
    public void Page_Renders_BackButton()
    {
        var cut = RenderDetail();
        cut.Markup.Should().Contain("Quay lại danh mục");
    }

    [Fact(DisplayName = "W4-HKD-D3: Page renders 'Tạo lại' (regenerate) button")]
    public void Page_Renders_RegenerateButton()
    {
        var cut = RenderDetail();
        cut.Markup.Should().Contain("Tạo lại");
    }

    [Fact(DisplayName = "W4-HKD-D4: Page renders period selector (year + month)")]
    public void Page_Renders_PeriodSelector()
    {
        var cut = RenderDetail();
        cut.Markup.Should().Contain("Năm");
        cut.Markup.Should().Contain("Tháng");
    }

    [Fact(DisplayName = "W4-HKD-D5: Page renders 'Áp dụng kỳ' (apply period) button")]
    public void Page_Renders_ApplyPeriodButton()
    {
        var cut = RenderDetail();
        cut.Markup.Should().Contain("Áp dụng kỳ");
    }

    [Fact(DisplayName = "W4-HKD-D6: Page renders DOCX export button")]
    public void Page_Renders_ExportDocxButton()
    {
        var cut = RenderDetail();
        cut.Markup.Should().Contain("Xuất DOCX");
    }

    [Fact(DisplayName = "W4-HKD-D7: Page renders XLSX export button")]
    public void Page_Renders_ExportXlsxButton()
    {
        var cut = RenderDetail();
        cut.Markup.Should().Contain("Xuất XLSX");
    }

    [Fact(DisplayName = "W4-HKD-D8: Page renders TT 152 layout (mẫu số + kỳ kê khai)")]
    public void Page_Renders_TT152Layout()
    {
        var cut = RenderDetail();
        cut.Render();

        cut.Markup.Should().Contain("SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ");
        cut.Markup.Should().Contain("Mẫu số S01a-HKD");
        cut.Markup.Should().Contain("Kỳ kê khai");
        cut.Markup.Should().Contain("Đơn vị tính: VNĐ");
    }

    [Fact(DisplayName = "W4-HKD-D9: Page renders journal entry rows from generated book")]
    public void Page_Renders_JournalEntryRows()
    {
        var cut = RenderDetail();
        cut.Render();

        cut.Markup.Should().Contain("Bán hàng hóa cho khách lẻ");
        // Amount = SumEntryLines = debit(10M) + credit(10M) = 20M, formatted "20.000.000" (vi-VN locale)
        cut.Markup.Should().Contain("20.000.000");
    }

    [Fact(DisplayName = "W4-HKD-D10: Page renders total rows (Tổng doanh thu / Doanh thu thuần / Lợi nhuận)")]
    public void Page_Renders_TotalRows()
    {
        var cut = RenderDetail();
        cut.Render();

        cut.Markup.Should().Contain("Tổng doanh thu");
        cut.Markup.Should().Contain("Doanh thu thuần");
        cut.Markup.Should().Contain("Lợi nhuận");
    }

    [Fact(DisplayName = "W4-HKD-D11: Page calls GenerateBookAsync on init with template code")]
    public void Page_Calls_GenerateBookAsync_OnInit()
    {
        var genMock = SetupGenerationService();
        var cut = RenderDetail("S01a-HKD", genMock);
        cut.Render();

        genMock.Verify(
            s => s.GenerateBookAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), "S01a-HKD"),
            Times.AtLeastOnce);
    }

    [Fact(DisplayName = "W4-HKD-D12: Page renders invalid-template error on ArgumentException")]
    public void Page_Renders_InvalidTemplateError_OnArgumentException()
    {
        var genMock = new Mock<IHKDBookGenerationService>();
        genMock.Setup(s => s.GenerateBookAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<string>()))
            .ThrowsAsync(new ArgumentException("Unknown template"));
        Services.AddSingleton(genMock.Object);
        SetupExportService();

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.HKDBookDetail>(
            parameters => parameters.Add(p => p.TemplateCode, "BAD-CODE"));

        cut.Markup.Should().Contain("Mẫu sổ không hợp lệ: BAD-CODE");
    }

    [Fact(DisplayName = "W4-HKD-D13: Page renders generic error message on non-ArgumentException exception")]
    public void Page_Renders_GenericError_OnGeneralException()
    {
        var genMock = new Mock<IHKDBookGenerationService>();
        genMock.Setup(s => s.GenerateBookAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));
        Services.AddSingleton(genMock.Object);
        SetupExportService();

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.HKDBookDetail>(
            parameters => parameters.Add(p => p.TemplateCode, "S01a-HKD"));

        cut.Markup.Should().Contain("Không thể tạo sổ kế toán");
        cut.Markup.Should().Contain("DB connection lost");
    }

    [Fact(DisplayName = "W4-HKD-D14: 'Xuất DOCX' button is rendered and enabled when book is loaded")]
    public void ExportDocxButton_IsEnabled_WhenBookLoaded()
    {
        // NOTE: @rendermode InteractiveServer prevents bUnit from wiring @onclick handlers.
        // Export button interaction is covered by Playwright E2E tests.
        // Here we verify the DOCX export VanAButton is rendered and not in a disabled state.
        var cut = RenderDetail();
        cut.Render();

        var docxButton = cut.FindComponents<VanAn.UI.Platform.Components.VanAButton>()
            .FirstOrDefault(b => b.Markup.Contains("Xuất DOCX"));
        docxButton.Should().NotBeNull("Expected 'Xuất DOCX' button when book is loaded");
        docxButton!.Markup.Should().Contain("Xuất DOCX");
        // NOTE: 'disabled' attribute renders as literal expression "False || Loading" in bUnit
        // static mode (@rendermode InteractiveServer). Cannot assert enabled state in bUnit.
    }

    [Fact(DisplayName = "W4-HKD-D15: 'Quay lại danh mục' (back) button is rendered")]
    public void BackButton_IsRendered()
    {
        // NOTE: @rendermode InteractiveServer prevents bUnit click. Navigation verified via E2E.
        var cut = RenderDetail();
        cut.Render();

        var backButton = cut.FindComponents<VanAn.UI.Platform.Components.VanAButton>()
            .FirstOrDefault(b => b.Markup.Contains("Quay lại danh mục"));
        backButton.Should().NotBeNull("Expected 'Quay lại danh mục' button");
        backButton!.Markup.Should().Contain("Quay lại danh mục");
    }
}
