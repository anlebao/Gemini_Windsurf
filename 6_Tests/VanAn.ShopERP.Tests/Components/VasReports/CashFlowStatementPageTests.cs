using Bunit;
using FluentAssertions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.Components.VasReports;

/// <summary>
/// W6 TDD — bUnit tests for CashFlowStatement.razor page.
/// Tests written BEFORE implementation. Page must:
///   - Render header "Báo Cáo Lưu Chuyển Tiền Tệ"
///   - Render period picker
///   - Render 3 activity sections (Operating/Investing/Financing)
///   - Render OpeningCash + ClosingCash + NetChange totals
///   - Show error alert on service exception
/// </summary>
[Trait("Category", "VASReportsUI")]
public class CashFlowStatementPageTests : VasReportPageTestBase
{
    [Fact(DisplayName = "W6-CF-1: Page renders header 'Báo Cáo Lưu Chuyển Tiền Tệ'")]
    public void Page_Renders_Header()
    {
        var mockService = new Mock<ICashFlowStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleCashFlowStatement());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.CashFlowStatement>();

        cut.Markup.Should().Contain("Báo Cáo Lưu Chuyển Tiền Tệ");
    }

    [Fact(DisplayName = "W6-CF-2: Page renders period picker (year + month)")]
    public void Page_Renders_PeriodPicker()
    {
        var mockService = new Mock<ICashFlowStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleCashFlowStatement());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.CashFlowStatement>();

        cut.Markup.Should().Contain("Năm");
        cut.Markup.Should().Contain("Tháng");
    }

    [Fact(DisplayName = "W6-CF-3: Page renders Operating Activities section with sample data")]
    public void Page_Renders_OperatingActivities()
    {
        var mockService = new Mock<ICashFlowStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleCashFlowStatement());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.CashFlowStatement>();

        cut.Markup.Should().Contain("hoạt động kinh doanh");
        cut.Markup.Should().Contain("Tiền thu từ bán hàng");
    }

    [Fact(DisplayName = "W6-CF-4: Page renders OpeningCash + ClosingCash + NetChange totals")]
    public void Page_Renders_Totals()
    {
        var mockService = new Mock<ICashFlowStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleCashFlowStatement());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.CashFlowStatement>();

        cut.Markup.Should().Contain("Tiền đầu kỳ");
        cut.Markup.Should().Contain("Tiền cuối kỳ");
        cut.Markup.Should().Contain("Lưu chuyển tiền thuần");
    }

    [Fact(DisplayName = "W6-CF-5: Page renders 2-column comparative (Ending + Opening)")]
    public void Page_Renders_TwoColumn_Comparative()
    {
        var mockService = new Mock<ICashFlowStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleCashFlowStatement());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.CashFlowStatement>();

        cut.Markup.Should().Contain("Số cuối kỳ");
        cut.Markup.Should().Contain("Số đầu năm");
    }

    [Fact(DisplayName = "W6-CF-6: Page renders error alert on service exception")]
    public void Page_Renders_ErrorAlert_OnException()
    {
        var mockService = new Mock<ICashFlowStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB connection failed"));
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.CashFlowStatement>();

        Assert.True(cut.Markup.Contains("error", StringComparison.OrdinalIgnoreCase) || cut.Markup.Contains("lỗi"),
            $"Expected error indicator in markup, got: {cut.Markup}");
    }
}
