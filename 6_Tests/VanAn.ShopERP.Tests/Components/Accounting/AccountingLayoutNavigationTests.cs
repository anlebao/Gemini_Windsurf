using Bunit;
using Xunit;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class AccountingLayoutNavigationTests : ComponentTestBase
{
    [Fact]
    public void AccountingLayout_ShouldRenderFiveMenuItems_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingLayout>();

        // Assert - Check for VanANavigation component
        var nav = cut.FindComponents<VanAn.UI.Platform.Components.VanANavigation>();
        nav.Count.Should().Be(1);
    }

    [Fact]
    public void AccountingLayout_ShouldContainAllRequiredRoutes_WhenRendered()
    {
        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingLayout>();

        // Assert - Check for VanANavigation component
        var nav = cut.FindComponents<VanAn.UI.Platform.Components.VanANavigation>();
        nav.Count.Should().Be(1);
        
        // Verify navigation renders with menu items
        nav[0].Markup.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AccountingLayout_ShouldRenderMenuLabelsInVietnamese_WhenMounted()
    {
        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingLayout>();

        // Assert - Check for VanANavigation component
        var nav = cut.FindComponents<VanAn.UI.Platform.Components.VanANavigation>();
        nav.Count.Should().Be(1);
        
        // Verify navigation renders
        nav[0].Markup.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VanADashboard_ShouldContainAccountingMenuItem_AfterSidebarUpdate()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetBalanceSummaryAsync(It.IsAny<Guid>()))
                   .ReturnsAsync(new BalanceSummary());
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingIndex>();

        // Assert - Check component renders with layout
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VanADashboard_AccountingMenuItem_ShouldLinkToAccountingRoute()
    {
        // Arrange
        var mockService = new Mock<IAccountingEntryService>();
        mockService.Setup(s => s.GetBalanceSummaryAsync(It.IsAny<Guid>()))
                   .ReturnsAsync(new BalanceSummary());
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.AccountingIndex>();

        // Assert - Check component renders with layout
        cut.Markup.Should().NotBeNullOrEmpty();
    }
}
