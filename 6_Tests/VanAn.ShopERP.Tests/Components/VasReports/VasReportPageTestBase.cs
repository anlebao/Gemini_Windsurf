using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.UI.Platform.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.ShopERP.Tests.Components;

namespace VanAn.ShopERP.Tests.Components.VasReports;

/// <summary>
/// Base class for VAS Wave 6 report page bUnit tests.
/// Registers mock W4 services (IBalanceSheetService, IIncomeStatementService, etc.)
/// so pages can be rendered in isolation with deterministic test data.
/// </summary>
public abstract class VasReportPageTestBase : ComponentTestBase
{
    protected static readonly Guid TestTenantGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
    protected static readonly TenantId TestTenantId = new(TestTenantGuid);

    protected VasReportPageTestBase()
    {
        // Register mock W4 services — tests override with specific setups.
        Services.AddSingleton<IBalanceSheetService>(sp => new Mock<IBalanceSheetService>().Object);
        Services.AddSingleton<IIncomeStatementService>(sp => new Mock<IIncomeStatementService>().Object);
        Services.AddSingleton<ICashFlowStatementService>(sp => new Mock<ICashFlowStatementService>().Object);
        Services.AddSingleton<ITrialBalanceService>(sp => new Mock<ITrialBalanceService>().Object);

        // bUnit provides FakeNavigationManager automatically — no need to register.
    }

    /// <summary>
    /// Render a component and force a re-render so VanAnDataGrid columns are populated.
    /// VanAnDataGrid registers columns in OnInitialized (after table renders) — needs
    /// a second render pass for column headers and data to appear in bUnit output.
    /// </summary>
    protected IRenderedComponent<TComponent> RenderWithReRender<TComponent>() where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        var cut = RenderComponent<TComponent>();
        cut.Render();
        return cut;
    }

    /// <summary>
    /// Build a sample BalanceSheet for testing.
    /// </summary>
    protected static BalanceSheet BuildSampleBalanceSheet()
    {
        var assets = new List<FinancialStatementLine>
        {
            new("100", "Tài sản ngắn hạn", 161_000_000m, 430_000_000m, 1, false),
            new("110", "Tiền và các khoản tương đương tiền", 59_000_000m, 150_000_000m, 2, false),
            new("111", "Tiền mặt", 59_000_000m, 50_000_000m, 3, false),
            new("112", "Tiền gửi ngân hàng", 0m, 100_000_000m, 3, false),
            new("150", "Hàng hóa", 73_000_000m, 80_000_000m, 2, false),
            new("210", "Tài sản cố định", 200_000_000m, 200_000_000m, 2, false),
            new("211", "Tài sản cố định hữu hình", 200_000_000m, 200_000_000m, 3, false),
        };
        var liabilities = new List<FinancialStatementLine>
        {
            new("300", "Nợ phải trả", 81_000_000m, 80_000_000m, 1, false),
            new("310", "Nợ ngắn hạn", 81_000_000m, 80_000_000m, 2, false),
            new("331", "Phải trả người bán", 43_000_000m, 50_000_000m, 3, false),
            new("3331", "Thuế GTGT phải nộp", 38_000_000m, 30_000_000m, 3, false),
        };
        var equity = new List<FinancialStatementLine>
        {
            new("400", "Vốn chủ sở hữu", 280_000_000m, 350_000_000m, 1, false),
            new("411", "Vốn góp của chủ sở hữu", 350_000_000m, 350_000_000m, 2, false),
            new("421", "Lợi nhuận sau thuế chưa phân phối", -70_000_000m, 0m, 2, true),
        };

        return new BalanceSheet(
            TestTenantId,
            new AccountingPeriod(2026, 6),
            new DateTime(2026, 7, 5, 9, 0, 0),
            assets, liabilities, equity,
            TotalAssetsEnding: 361_000_000m, TotalAssetsOpening: 430_000_000m,
            TotalLiabilitiesAndEquityEnding: 361_000_000m, TotalLiabilitiesAndEquityOpening: 430_000_000m
        );
    }

    /// <summary>
    /// Build a sample IncomeStatement for testing.
    /// </summary>
    protected static IncomeStatement BuildSampleIncomeStatement()
    {
        var lines = new List<FinancialStatementLine>
        {
            new("01", "Doanh thu bán hàng và cung cấp dịch vụ", 10_000_000m, 0m, 1, false),
            new("02", "Các khoản giảm trừ doanh thu", 0m, 0m, 2, false),
            new("10", "Doanh thu thuần bán hàng và cung cấp dịch vụ", 10_000_000m, 0m, 1, false),
            new("20", "Giá vốn hàng bán", 7_000_000m, 0m, 1, false),
            new("30", "Lợi nhuận gộp về bán hàng và cung cấp dịch vụ", 3_000_000m, 0m, 1, false),
            new("40", "Chi phí bán hàng", 2_000_000m, 0m, 1, false),
            new("50", "Chi phí quản lý doanh nghiệp", 0m, 0m, 1, false),
            new("60", "Lợi nhuận thuần từ hoạt động kinh doanh", 1_000_000m, 0m, 1, false),
            new("70", "Chi phí khác", 0m, 0m, 1, false),
            new("80", "Lợi nhuận khác", 0m, 0m, 1, false),
            new("90", "Tổng lợi nhuận kế toán trước thuế", 1_000_000m, 0m, 1, false),
            new("100", "Chi phí thuế TNDN", 0m, 0m, 1, false),
            new("110", "Lợi nhuận sau thuế TNDN", 1_000_000m, 0m, 1, false),
        };

        return new IncomeStatement(
            TestTenantId,
            new AccountingPeriod(2026, 6),
            new DateTime(2026, 7, 5, 9, 0, 0),
            TotalRevenueEnding: 10_000_000m, TotalRevenueOpening: 0m,
            NetProfitEnding: 1_000_000m, NetProfitOpening: 0m,
            Lines: lines
        );
    }

    /// <summary>
    /// Build a sample CashFlowStatement for testing.
    /// </summary>
    protected static CashFlowStatement BuildSampleCashFlowStatement()
    {
        var operating = new List<FinancialStatementLine>
        {
            new("01", "Tiền thu từ bán hàng, cung cấp dịch vụ và thu khác", 11_000_000m, 0m, 1, false),
            new("02", "Tiền chi để mua hàng hóa, dịch vụ", -2_000_000m, 0m, 1, false),
            new("20", "Lưu chuyển tiền thuần từ hoạt động kinh doanh", 9_000_000m, 0m, 1, false),
        };
        var investing = new List<FinancialStatementLine>();
        var financing = new List<FinancialStatementLine>();

        return new CashFlowStatement(
            TestTenantId,
            new AccountingPeriod(2026, 6),
            new DateTime(2026, 7, 5, 9, 0, 0),
            OpeningCash: 150_000_000m, ClosingCash: 159_000_000m, NetChange: 9_000_000m,
            OperatingActivities: operating,
            InvestingActivities: investing,
            FinancingActivities: financing
        );
    }

    /// <summary>
    /// Build a sample TrialBalance for testing.
    /// </summary>
    protected static TrialBalance BuildSampleTrialBalance()
    {
        var accounts = new List<TrialBalanceAccount>
        {
            new("111", "Tiền mặt", 59_000_000m, 0m, 59_000_000m),
            new("112", "Tiền gửi ngân hàng", 0m, 0m, 0m),
            new("156", "Hàng hóa", 7_000_000m, 7_000_000m, 0m),
            new("211", "Tài sản cố định hữu hình", 0m, 0m, 0m),
            new("331", "Phải trả người bán", 0m, 43_000_000m, -43_000_000m),
            new("3331", "Thuế GTGT phải nộp", 0m, 38_000_000m, -38_000_000m),
            new("411", "Vốn góp của chủ sở hữu", 0m, 350_000_000m, -350_000_000m),
            new("511", "Doanh thu bán hàng", 0m, 10_000_000m, -10_000_000m),
            new("632", "Giá vốn hàng bán", 7_000_000m, 0m, 7_000_000m),
            new("6421", "Chi phí bán hàng", 2_000_000m, 0m, 2_000_000m),
        };

        return new TrialBalance(
            Period: new AccountingPeriod(2026, 6),
            GeneratedAt: new DateTime(2026, 7, 5, 9, 0, 0),
            Accounts: accounts,
            TotalDebit: 75_000_000m,
            TotalCredit: 448_000_000m,
            IsBalanced: false // Test data intentionally unbalanced to verify rendering
        );
    }
}
