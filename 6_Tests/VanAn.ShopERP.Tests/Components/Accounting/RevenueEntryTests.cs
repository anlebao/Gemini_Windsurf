using Bunit;
using Xunit;
using Moq;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using FluentAssertions;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class RevenueEntryTests : ComponentTestBase
{
    [Fact]
    public void RevenueEntry_ShouldRender_WhenComponentMounted()
    {
        // Act - Render component with layout
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

        // Assert - Verify component renders
        cut.Markup.Should().Contain("Nhập Doanh Thu");
        cut.Markup.Should().Contain("Lưu Doanh Thu");
    }

    [Fact]
    public void RevenueEntry_ShouldRenderBackButton_WhenComponentMounted()
    {
        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

        // Assert - Verify back button is rendered
        cut.Markup.Should().Contain("Quay Lại");
    }

    [Fact]
    public void RevenueEntry_ShouldHaveServiceRegistered_WhenComponentMounted()
    {
        // Arrange
        var mockService = new Mock<IAccountingService>();
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

        // Assert - Component renders without errors, service is available
        cut.Markup.Should().NotBeNullOrEmpty();
    }
}
