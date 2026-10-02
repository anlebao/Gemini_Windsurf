using Bunit;
using Xunit;
using Moq;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using FluentAssertions;

namespace VanAn.ShopERP.Tests.Components.Accounting;

public class ExpenseEntryTests : ComponentTestBase
{
    [Fact]
    public void ExpenseEntry_ShouldRender_WhenComponentMounted()
    {
        // Act - Render component with layout
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Assert - Verify component renders
        cut.Markup.Should().Contain("Nhập Chi Phí");
    }

    [Fact]
    public void ExpenseEntry_ShouldHaveServiceRegistered_WhenComponentMounted()
    {
        // Arrange
        var mockService = new Mock<IAccountingService>();
        Services.AddSingleton(mockService.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Assert - Component renders without errors, service is available
        cut.Markup.Should().NotBeNullOrEmpty();
    }

    // T6 (TT 71 Phase 4a): tenant HTX → phiếu chi chỉ 642/658 (KHÔNG 621/622/627/641)
    [Fact]
    public void ExpenseEntry_HtxTenant_ShowsOnlyTt71ExpenseAccounts()
    {
        // Arrange — override FeatureFlagService mock: tenant type = HTX
        var featureFlagMock = new Mock<IVasFeatureFlagService>();
        featureFlagMock.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(TenantType.HTX);
        Services.AddSingleton(featureFlagMock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Assert — options theo TT 71 (Phụ lục I): 642/658, không có 621/622/627/641
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("658 - Chi phí khác"));
        cut.Markup.Should().Contain("642 - Chi phí quản lý kinh doanh");
        cut.Markup.Should().NotContain("621 - Chi phí nguyên vật liệu");
        cut.Markup.Should().NotContain("622 - Chi phí nhân công");
        cut.Markup.Should().NotContain("627 - Chi phí bán hàng");
        cut.Markup.Should().NotContain("641 - Chi phí quản lý doanh nghiệp");
    }

    // T6 (TT 71 Phase 4a): tenant DN → options giữ nguyên (không regress)
    [Fact]
    public void ExpenseEntry_EnterpriseTenant_KeepsExistingAccounts()
    {
        // Arrange — tenant type = Enterprise_SME (DN vừa → TT 133)
        var featureFlagMock = new Mock<IVasFeatureFlagService>();
        featureFlagMock.Setup(f => f.GetTenantTypeAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(TenantType.Enterprise_SME);
        Services.AddSingleton(featureFlagMock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.ExpenseEntry>();

        // Assert — options DN giữ nguyên (621/622/627/641/642), không có 658
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("627 - Chi phí bán hàng"));
        cut.Markup.Should().Contain("621 - Chi phí nguyên vật liệu");
        cut.Markup.Should().NotContain("658 - Chi phí khác");
    }
}
