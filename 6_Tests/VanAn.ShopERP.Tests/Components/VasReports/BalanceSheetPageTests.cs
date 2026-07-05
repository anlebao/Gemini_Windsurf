using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.Components.VasReports;

/// <summary>
/// W6 TDD — bUnit tests for BalanceSheet.razor page.
/// Tests written BEFORE implementation. Page must:
///   - Render page header "Bảng Cân Đối Kế Toán"
///   - Render period picker (year + month dropdowns)
///   - Render Assets/Liabilities/Equity sections with VanAnDataGrid
///   - Render totals (TotalAssets, TotalLiabilitiesAndEquity)
///   - Show error alert on service exception
/// </summary>
[Trait("Category", "VASReportsUI")]
public class BalanceSheetPageTests : VasReportPageTestBase
{
    [Fact(DisplayName = "W6-BS-1: Page renders header 'Bảng Cân Đối Kế Toán'")]
    public void Page_Renders_Header()
    {
        // Arrange — mock service returns sample BS
        var mockService = new Mock<IBalanceSheetService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleBalanceSheet());
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.BalanceSheet>();

        // Assert
        cut.Markup.Should().Contain("Bảng Cân Đối Kế Toán");
    }

    [Fact(DisplayName = "W6-BS-2: Page renders period picker (year + month)")]
    public void Page_Renders_PeriodPicker()
    {
        var mockService = new Mock<IBalanceSheetService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleBalanceSheet());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.BalanceSheet>();

        cut.Markup.Should().Contain("Năm");
        cut.Markup.Should().Contain("Tháng");
    }

    [Fact(DisplayName = "W6-BS-3: Page renders Asset section with sample data")]
    public void Page_Renders_AssetSection()
    {
        var mockService = new Mock<IBalanceSheetService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleBalanceSheet());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.BalanceSheet>();

        // Should contain "TÀI SẢN" section header and sample account names
        cut.Markup.Should().Contain("TÀI SẢN");
        cut.Markup.Should().Contain("Tiền mặt");
        cut.Markup.Should().Contain("Tài sản cố định hữu hình");
    }

    [Fact(DisplayName = "W6-BS-4: Page renders totals (TotalAssets + TotalLiabilitiesAndEquity)")]
    public void Page_Renders_Totals()
    {
        var mockService = new Mock<IBalanceSheetService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleBalanceSheet());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.BalanceSheet>();

        // TotalAssets = 361,000,000 → "361.000.000" or "361,000,000" (culture-dependent)
        cut.Markup.Should().Contain("361");
        cut.Markup.Should().Contain("Tổng cộng tài sản");
    }

    [Fact(DisplayName = "W6-BS-5: Page renders error alert on service exception")]
    public void Page_Renders_ErrorAlert_OnException()
    {
        var mockService = new Mock<IBalanceSheetService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("BS invariant violated: unbalanced"));
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.BalanceSheet>();

        // Should show error alert with the exception message
        Assert.True(cut.Markup.Contains("BS invariant violated") || cut.Markup.Contains("error", StringComparison.OrdinalIgnoreCase) || cut.Markup.Contains("lỗi"),
            $"Expected error indicator in markup, got: {cut.Markup}");
    }

    [Fact(DisplayName = "W6-BS-6: Page renders 2-column comparative (Ending + Opening)")]
    public void Page_Renders_TwoColumn_Comparative()
    {
        var mockService = new Mock<IBalanceSheetService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleBalanceSheet());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.BalanceSheet>();

        // Should have both "Số cuối kỳ" (Ending) and "Số đầu năm" (Opening) column headers
        cut.Markup.Should().Contain("Số cuối kỳ");
        cut.Markup.Should().Contain("Số đầu năm");
    }
}
