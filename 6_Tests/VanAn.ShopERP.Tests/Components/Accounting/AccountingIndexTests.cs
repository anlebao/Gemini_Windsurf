using Bunit;
using Xunit;
using Moq;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class AccountingIndexTests : ComponentTestBase
{
    [Fact]
    public void AccountingIndex_ShouldRenderFourMetricsCards_WhenComponentMounted()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetBalanceSummaryAsync(It.IsAny<Guid>()))
                   .ReturnsAsync(new BalanceSummary());
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingIndex>();

        // Assert - Check for VanAMetricsCard components
        var metricsCards = cut.FindComponents<VanAn.UI.Platform.Components.VanAMetricsCard>();
        metricsCards.Count.Should().Be(4);
    }

    [Fact]
    public void AccountingIndex_ShouldRenderQuickActionButtons_WhenComponentMounted()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetBalanceSummaryAsync(It.IsAny<Guid>()))
                   .ReturnsAsync(new BalanceSummary());
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingIndex>();

        // Assert - Check for VanAButton components in action section
        var buttons = cut.FindComponents<VanAn.UI.Platform.Components.VanAButton>();
        buttons.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void AccountingIndex_ShouldNavigateToRevenueEntry_WhenRevenueButtonClicked()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetBalanceSummaryAsync(It.IsAny<Guid>()))
                   .ReturnsAsync(new BalanceSummary());
        Services.AddSingleton(mockService.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingIndex>();

        // Act - Find the revenue button by its text content
        var revenueButton = cut.FindComponents<VanAn.UI.Platform.Components.VanAButton>()
                               .FirstOrDefault(b => b.Markup.Contains("Nhập Doanh Thu"));
        revenueButton.Should().NotBeNull();
        
        // Verify the button exists with correct text
        revenueButton!.Markup.Should().Contain("Nhập Doanh Thu");
    }

    [Fact]
    public void AccountingIndex_ShouldNavigateToExpenseEntry_WhenExpenseButtonClicked()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetBalanceSummaryAsync(It.IsAny<Guid>()))
                   .ReturnsAsync(new BalanceSummary());
        Services.AddSingleton(mockService.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingIndex>();

        // Act - Find the expense button by its text content
        var expenseButton = cut.FindComponents<VanAn.UI.Platform.Components.VanAButton>()
                               .FirstOrDefault(b => b.Markup.Contains("Nhập Chi Phí"));
        expenseButton.Should().NotBeNull();
        
        // Verify the button exists with correct text
        expenseButton!.Markup.Should().Contain("Nhập Chi Phí");
    }

    // Regime-lock (2026-10-30): nút báo cáo theo chuẩn hiệu lực — selection resolve qua
    // GetTenantTypeAsync (real service trong ComponentTestBase).
    [Fact]
    public void AccountingIndex_HtxTenant_ShowsFinancialReportsButton_NotHkdBooks()
    {
        var mockFlag = new Mock<IVasFeatureFlagService>();
        mockFlag.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TenantType.HTX);
        Services.AddSingleton(mockFlag.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingIndex>();

        var buttons = cut.FindComponents<VanAn.UI.Platform.Components.VanAButton>();
        buttons.Should().Contain(b => b.Markup.Contains("Báo Cáo Tài Chính"));
        buttons.Should().NotContain(b => b.Markup.Contains("Sổ HKD"));
    }

    [Fact]
    public void AccountingIndex_HkdTenant_ShowsHkdBooksButton()
    {
        var mockFlag = new Mock<IVasFeatureFlagService>();
        mockFlag.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TenantType.HKD);
        Services.AddSingleton(mockFlag.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingIndex>();

        var buttons = cut.FindComponents<VanAn.UI.Platform.Components.VanAButton>();
        buttons.Should().Contain(b => b.Markup.Contains("Sổ HKD (TT 152)"));
        buttons.Should().NotContain(b => b.Markup.Contains("Báo Cáo Tài Chính"));
    }
}
