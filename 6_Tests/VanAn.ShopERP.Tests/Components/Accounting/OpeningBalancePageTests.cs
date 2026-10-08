using VanAn.CoreHub.Services.CongNo;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// NHẬP LIỆU & SỔ SÁCH P3 (#2): trang khai báo số dư đầu kỳ —
/// render 2 phần (TK + công nợ cũ) · thêm/xoá dòng · tổng Nợ/Có + nút bù 421 · lưu gọi service.
/// </summary>
public class OpeningBalancePageTests : ComponentTestBase
{
    private void RegisterOpeningBalanceService(OpeningBalanceSaveResultDto? result = null)
    {
        var mock = new Mock<IOpeningBalanceService>();
        if (result != null)
        {
            mock.Setup(s => s.SaveAsync(It.IsAny<TenantId>(), It.IsAny<OpeningBalanceRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }
        mock.Setup(s => s.GetAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpeningBalanceDto { Exists = false });
        Services.AddSingleton(mock.Object);
    }

    [Fact]
    public void OpeningBalance_ShouldRender_TwoSections()
    {
        // Arrange
        RegisterOpeningBalanceService();

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.OpeningBalance>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Khai Báo Số Dư Đầu Kỳ"));
        cut.Markup.Should().Contain("Số Dư Các Tài Khoản");
        cut.Markup.Should().Contain("Công Nợ Cũ Theo Đối Tượng");
        cut.Markup.Should().Contain("Lưu Số Dư Đầu Kỳ");
    }

    [Fact]
    public void OpeningBalance_AddAccountLine_AddsRow()
    {
        // Arrange
        RegisterOpeningBalanceService();
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.OpeningBalance>();

        // Act — thêm 1 tài khoản
        cut.FindAll("button").First(b => b.TextContent.Contains("Thêm tài khoản")).Click();

        // Assert — có 1 dòng TK
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='ob-account-line']").Count.Should().Be(1));
    }

    [Fact]
    public void OpeningBalance_AddCongNoLine_AddsRow()
    {
        // Arrange
        RegisterOpeningBalanceService();
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.OpeningBalance>();

        // Act
        cut.FindAll("button").First(b => b.TextContent.Contains("Thêm đối tượng")).Click();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='ob-congno-line']").Count.Should().Be(1));
    }

    [Fact]
    public void OpeningBalance_Unbalanced_ShowsDiffAndBalance421Button()
    {
        // Arrange
        RegisterOpeningBalanceService();
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.OpeningBalance>();

        // Act — thêm 1 TK Nợ 10tr (không Có → chênh)
        cut.FindAll("button").First(b => b.TextContent.Contains("Thêm tài khoản")).Click();
        var line = cut.Find("[data-testid='ob-account-line']");
        line.QuerySelectorAll("input")[0].Change("111");
        line.QuerySelectorAll("input")[1].Change("10000000");

        // Assert — hiện chênh lệch + nút "Bù vào 421"
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("chênh"));
        cut.Markup.Should().Contain("Bù vào 421");
    }

    [Fact]
    public void OpeningBalance_Save_CallsService()
    {
        // Arrange
        RegisterOpeningBalanceService(new OpeningBalanceSaveResultDto { JournalEntryCount = 1, CongNoEntryCount = 0, Message = "Đã lưu số dư đầu kỳ 10/2026" });
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.OpeningBalance>();

        // Act — thêm 1 TK hợp lệ + lưu
        cut.FindAll("button").First(b => b.TextContent.Contains("Thêm tài khoản")).Click();
        var line = cut.Find("[data-testid='ob-account-line']");
        line.QuerySelectorAll("input")[0].Change("111");
        line.QuerySelectorAll("input")[1].Change("10000000");
        cut.FindAll("button").First(b => b.TextContent.Contains("Lưu Số Dư Đầu Kỳ")).Click();

        // Assert — success alert hiển thị
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đã lưu số dư đầu kỳ"));
    }
}
