using Bunit;
using FluentAssertions;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.Components.VasReports;

/// <summary>
/// W6 TDD — bUnit tests for TrialBalance.razor page.
/// Tests written BEFORE implementation. Page must:
///   - Render header "Bảng Cân Đối Số Phát Sinh"
///   - Render period picker
///   - Render accounts table (AccountNumber, AccountName, Debit, Credit, Balance)
///   - Render TotalDebit + TotalCredit totals
///   - Render IsBalanced indicator
///   - Show error alert on service exception
/// </summary>
[Trait("Category", "VASReportsUI")]
public class TrialBalancePageTests : VasReportPageTestBase
{
    [Fact(DisplayName = "W6-TB-1: Page renders header 'Bảng Cân Đối Số Phát Sinh'")]
    public void Page_Renders_Header()
    {
        var mockService = new Mock<ITrialBalanceService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleTrialBalance());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.TrialBalance>();

        cut.Markup.Should().Contain("Bảng Cân Đối Số Phát Sinh");
    }

    [Fact(DisplayName = "W6-TB-2: Page renders period picker (year + month)")]
    public void Page_Renders_PeriodPicker()
    {
        var mockService = new Mock<ITrialBalanceService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleTrialBalance());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.TrialBalance>();

        cut.Markup.Should().Contain("Năm");
        cut.Markup.Should().Contain("Tháng");
    }

    [Fact(DisplayName = "W6-TB-3: Page renders account rows with sample data")]
    public void Page_Renders_AccountRows()
    {
        var mockService = new Mock<ITrialBalanceService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleTrialBalance());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.TrialBalance>();

        cut.Markup.Should().Contain("111");
        cut.Markup.Should().Contain("Tiền mặt");
        cut.Markup.Should().Contain("511");
        cut.Markup.Should().Contain("Doanh thu bán hàng");
    }

    [Fact(DisplayName = "W6-TB-4: Page renders TotalDebit + TotalCredit totals")]
    public void Page_Renders_Totals()
    {
        var mockService = new Mock<ITrialBalanceService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleTrialBalance());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.TrialBalance>();

        cut.Markup.Should().Contain("Tổng phát sinh Nợ");
        cut.Markup.Should().Contain("Tổng phát sinh Có");
    }

    [Fact(DisplayName = "W6-TB-5: Page renders IsBalanced indicator")]
    public void Page_Renders_IsBalancedIndicator()
    {
        var mockService = new Mock<ITrialBalanceService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleTrialBalance());
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.TrialBalance>();

        // Should show balance status — "Cân bằng" or "Không cân bằng"
        Assert.True(cut.Markup.Contains("Cân bằng") || cut.Markup.Contains("Không cân bằng"),
            $"Expected balance status in markup, got: {cut.Markup}");
    }

    [Fact(DisplayName = "W6-TB-6: Page renders error alert on service exception")]
    public void Page_Renders_ErrorAlert_OnException()
    {
        var mockService = new Mock<ITrialBalanceService>();
        mockService.Setup(s => s.GenerateAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<AccountingStandard>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB connection failed"));
        Services.AddSingleton(mockService.Object);

        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.TrialBalance>();

        Assert.True(cut.Markup.Contains("error", StringComparison.OrdinalIgnoreCase) || cut.Markup.Contains("lỗi"),
            $"Expected error indicator in markup, got: {cut.Markup}");
    }
}
