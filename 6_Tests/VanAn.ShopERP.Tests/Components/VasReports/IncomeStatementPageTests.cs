using Bunit;
using FluentAssertions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.Components.VasReports;

/// <summary>
/// W6 TDD — bUnit tests for IncomeStatement.razor page.
/// Tests written BEFORE implementation. Page must:
///   - Render header "Báo Cáo Kết Quả Hoạt Động Kinh Doanh"
///   - Render period picker
///   - Render Lines with VanAnDataGrid (2-column: Ending + Opening)
///   - Render TotalRevenue + NetProfit totals
///   - Show error alert on service exception
/// </summary>
[Trait("Category", "VASReportsUI")]
public class IncomeStatementPageTests : VasReportPageTestBase
{
    [Fact(DisplayName = "W6-IS-1: Page renders header 'Báo Cáo Kết Quả Hoạt Động Kinh Doanh'")]
    public void Page_Renders_Header()
    {
        var mockService = new Mock<IIncomeStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleIncomeStatement());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.IncomeStatement>();

        cut.Markup.Should().Contain("Báo Cáo Kết Quả Hoạt Động Kinh Doanh");
    }

    [Fact(DisplayName = "W6-IS-2: Page renders period picker (year + month)")]
    public void Page_Renders_PeriodPicker()
    {
        var mockService = new Mock<IIncomeStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleIncomeStatement());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.IncomeStatement>();

        cut.Markup.Should().Contain("Năm");
        cut.Markup.Should().Contain("Tháng");
    }

    [Fact(DisplayName = "W6-IS-3: Page renders line items with sample data")]
    public void Page_Renders_LineItems()
    {
        var mockService = new Mock<IIncomeStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleIncomeStatement());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.IncomeStatement>();

        cut.Markup.Should().Contain("Doanh thu bán hàng");
        cut.Markup.Should().Contain("Giá vốn hàng bán");
        cut.Markup.Should().Contain("Lợi nhuận sau thuế TNDN");
    }

    [Fact(DisplayName = "W6-IS-4: Page renders TotalRevenue + NetProfit totals")]
    public void Page_Renders_Totals()
    {
        var mockService = new Mock<IIncomeStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleIncomeStatement());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.IncomeStatement>();

        cut.Markup.Should().Contain("Tổng doanh thu");
        cut.Markup.Should().Contain("Lợi nhuận sau thuế");
    }

    [Fact(DisplayName = "W6-IS-5: Page renders 2-column comparative (Ending + Opening)")]
    public void Page_Renders_TwoColumn_Comparative()
    {
        var mockService = new Mock<IIncomeStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleIncomeStatement());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.IncomeStatement>();

        cut.Markup.Should().Contain("Số cuối kỳ");
        cut.Markup.Should().Contain("Số đầu năm");
    }

    [Fact(DisplayName = "W6-IS-6: Page renders error alert on service exception")]
    public void Page_Renders_ErrorAlert_OnException()
    {
        var mockService = new Mock<IIncomeStatementService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB connection failed"));
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.IncomeStatement>();

        Assert.True(cut.Markup.Contains("error", StringComparison.OrdinalIgnoreCase) || cut.Markup.Contains("lỗi"),
            $"Expected error indicator in markup, got: {cut.Markup}");
    }
}
