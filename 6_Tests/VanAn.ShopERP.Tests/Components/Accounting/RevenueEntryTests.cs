using Bunit;
using Xunit;
using Moq;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.CongNo;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Services;
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

    // THU CHI & CÔNG NỢ MVP (P2.1, SRS §5.1 FR-1): phiếu thu 3 loại
    [Fact]
    public void RevenueEntry_ShouldRenderVoucherTypeSelect_WithThreeOptions()
    {
        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

        // Assert — dropdown "Loại Phiếu Thu" + 3 lựa chọn
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Loại Phiếu Thu"));
        cut.Markup.Should().Contain("Thu doanh thu");
        cut.Markup.Should().Contain("Ghi nhận phải thu");
        cut.Markup.Should().Contain("Thu tiền khách trả nợ");
    }

    [Fact]
    public void RevenueEntry_CongNoType_ShowsDoiTuongRequired_AndAccount131()
    {
        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

        // Chọn "Ghi nhận phải thu" → hiện Đối tượng + TK 131
        cut.Find("#voucherType").Change("receivable");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đối Tượng (khách hàng)"));
        cut.Markup.Should().Contain("131 - Phải thu khách hàng");
        // Không còn options doanh thu
        cut.Markup.Should().NotContain("711 - Chênh lệch tỷ giá hối đoái");
    }

    [Fact]
    public void RevenueEntry_RevenueType_DoesNotShowDoiTuong()
    {
        // Act — loại mặc định "Thu doanh thu"
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

        // Assert — không có ô Đối tượng
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Đối Tượng (khách hàng)"));
    }

    [Fact]
    public void RevenueEntry_CongNoType_MstLookup_FillsDoiTuong()
    {
        // Arrange — mock tra MST
        var businessInfoMock = new Mock<IBusinessInfoApiClient>();
        businessInfoMock.Setup(b => b.GetByMstAsync("0312345678", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new BusinessInfoDto
                        {
                            TaxCode = "0312345678",
                            BusinessName = "Công Ty TNHH X",
                            Status = "active"
                        });
        Services.AddSingleton(businessInfoMock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

        // Chọn loại công nợ + nhập MST + bấm Tra cứu [G11]
        cut.Find("#voucherType").Change("receivable");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đối Tượng (khách hàng)"));
        cut.Find("#mst").Change("0312345678");
        cut.FindAll("button").First(b => b.TextContent.Contains("Tra cứu MST")).Click();

        // Assert — tự điền tên công ty vào Đối tượng [G11]
        cut.WaitForAssertion(() => cut.Find("#doiTuong").GetAttribute("value").Should().Be("Công Ty TNHH X"));
    }

    [Fact]
    public void RevenueEntry_CongNoType_ShowsDoiTuongMarkedRequired()
    {
        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.RevenueEntry>();

        // Chọn "Ghi nhận phải thu" → ô Đối tượng hiện kèm dấu * bắt buộc
        cut.Find("#voucherType").Change("receivable");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đối Tượng (khách hàng)"));
        cut.Markup.Should().Contain("<span class=\"text-danger\">*</span>");
    }
}
