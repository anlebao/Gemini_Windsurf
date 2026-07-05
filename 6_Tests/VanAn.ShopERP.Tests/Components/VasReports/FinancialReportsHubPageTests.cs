using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace VanAn.ShopERP.Tests.Components.VasReports;

/// <summary>
/// W6 TDD — bUnit tests for FinancialReports.razor navigation hub page.
/// Tests written BEFORE implementation. Page must:
///   - Render header "Báo Cáo Tài Chính"
///   - Render navigation links to 4 report pages
///   - Links point to correct routes (/accounting/balance-sheet, etc.)
/// </summary>
[Trait("Category", "VASReportsUI")]
public class FinancialReportsHubPageTests : VasReportPageTestBase
{
    [Fact(DisplayName = "W6-HUB-1: Page renders header 'Báo Cáo Tài Chính'")]
    public void Page_Renders_Header()
    {
        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.FinancialReports>();

        cut.Markup.Should().Contain("Báo Cáo Tài Chính");
    }

    [Fact(DisplayName = "W6-HUB-2: Page renders link to Balance Sheet")]
    public void Page_Renders_BalanceSheetLink()
    {
        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.FinancialReports>();

        cut.Markup.Should().Contain("Bảng Cân Đối Kế Toán");
        cut.Markup.Should().Contain("/accounting/balance-sheet");
    }

    [Fact(DisplayName = "W6-HUB-3: Page renders link to Income Statement")]
    public void Page_Renders_IncomeStatementLink()
    {
        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.FinancialReports>();

        cut.Markup.Should().Contain("Báo Cáo Kết Quả Hoạt Động Kinh Doanh");
        cut.Markup.Should().Contain("/accounting/income-statement");
    }

    [Fact(DisplayName = "W6-HUB-4: Page renders link to Cash Flow Statement")]
    public void Page_Renders_CashFlowLink()
    {
        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.FinancialReports>();

        cut.Markup.Should().Contain("Báo Cáo Lưu Chuyển Tiền Tệ");
        cut.Markup.Should().Contain("/accounting/cash-flow-statement");
    }

    [Fact(DisplayName = "W6-HUB-5: Page renders link to Trial Balance")]
    public void Page_Renders_TrialBalanceLink()
    {
        var cut = RenderWithReRender<VanAn.ShopERP.Components.Pages.Accounting.FinancialReports>();

        cut.Markup.Should().Contain("Bảng Cân Đối Số Phát Sinh");
        cut.Markup.Should().Contain("/accounting/trial-balance");
    }
}
