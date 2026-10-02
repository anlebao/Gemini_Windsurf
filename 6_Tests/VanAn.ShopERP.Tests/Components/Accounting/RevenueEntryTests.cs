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

    // T6 (TT 71 Phase 4a): tenant HTX → phiếu thu chỉ 511/512/558 (KHÔNG 515/711)
    [Fact]
    public void RevenueEntry_HtxTenant_ShowsOnlyTt71RevenueAccounts()
    {
        // Arrange — override FeatureFlagService mock: tenant type = HTX
        var featureFlagMock = new Mock<IVasFeatureFlagService>();
        featureFlagMock.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(TenantType.HTX);
        Services.AddSingleton(featureFlagMock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

        // Assert — options theo TT 71 (Phụ lục I): 511/512/558, không có 515/711
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("558 - Thu nhập khác"));
        cut.Markup.Should().Contain("511 - Doanh thu giao dịch bên ngoài");
        cut.Markup.Should().Contain("512 - Doanh thu giao dịch nội bộ");
        cut.Markup.Should().NotContain("515");
        cut.Markup.Should().NotContain("711");
    }

    // T6 (TT 71 Phase 4a): tenant DN → options giữ nguyên (không regress)
    [Fact]
    public void RevenueEntry_EnterpriseTenant_KeepsExistingAccounts()
    {
        // Arrange — tenant type = Enterprise_SME (DN vừa → TT 133)
        var featureFlagMock = new Mock<IVasFeatureFlagService>();
        featureFlagMock.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(TenantType.Enterprise_SME);
        Services.AddSingleton(featureFlagMock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

        // Assert — options DN giữ nguyên (511/512/515/711), không có 558
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("711 - Chênh lệch tỷ giá hối đoái"));
        cut.Markup.Should().Contain("515");
        cut.Markup.Should().NotContain("558 - Thu nhập khác");
    }
}
