using Bunit;
using Xunit;
using Moq;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using FluentAssertions;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class TransactionHistoryTests : ComponentTestBase
{
    [Fact]
    public void TransactionHistory_ShouldRender_WhenComponentMounted()
    {
        // Act - Render component with layout
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.TransactionHistory>();

        // Assert - Verify component renders
        cut.Markup.Should().Contain("Lịch Sử Giao Dịch");
    }

    [Fact]
    public void TransactionHistory_ShouldHaveServiceRegistered_WhenComponentMounted()
    {
        // Arrange
        var mockService = new Mock<IAccountingService>();
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.TransactionHistory>();

        // Assert - Component renders without errors, service is available
        cut.Markup.Should().NotBeNullOrEmpty();
    }
}
