using Moq;
using VanAn.CoreHub.Services.GeneralLedger;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// TUÂN THỦ PHÁP LÝ P0.2-Đ2 (W2, 2026-10-17): trang Sổ Chi Tiết TK —
/// render header TK + đối tượng (dư đầu kỳ/PS Nợ/PS Có/dư cuối kỳ) + dòng phát sinh + dropdown đối tượng.
/// </summary>
public class SubLedgerPageTests : ComponentTestBase
{
    private void RegisterSubLedgerService()
    {
        var mock = new Mock<IGeneralLedgerService>();
        mock.Setup(s => s.GetAccountsAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountActivityDto>
            {
                new("131", "Phải thu khách hàng", 5_000_000, 2_000_000),
                new("111", "Tiền mặt", 3_000_000, 0)
            });
        mock.Setup(s => s.GetSubLedgerDoiTuongAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), "131", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Khách A", "Khách B" });
        mock.Setup(s => s.GetSubLedgerAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), "131", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubLedgerReportDto(
                "131",
                "Phải thu khách hàng",
                null,
                3_000_000,
                5_000_000,
                2_000_000,
                6_000_000,
                new List<SubLedgerRowDto>
                {
                    new(new DateTime(2026, 7, 5), "PT-001", "Bán chịu — Khách A", null, "Khách A", 5_000_000, 0, 8_000_000),
                    new(new DateTime(2026, 7, 10), "PT-002", "Thu tiền khách trả nợ — Khách A", null, "Khách A", 0, 2_000_000, 6_000_000)
                }));
        Services.AddSingleton(mock.Object);
    }

    [Fact]
    public void SubLedger_Renders_HeaderRowsAndDoiTuongDropdown()
    {
        // Arrange
        RegisterSubLedgerService();

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.SubLedger>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Sổ Chi Tiết"));
        cut.Markup.Should().Contain("Phải thu khách hàng");
        cut.Markup.Should().Contain("Dư đầu kỳ");
        cut.Markup.Should().Contain("Dư cuối kỳ");
        cut.Markup.Should().Contain("Bán chịu — Khách A");
        cut.Markup.Should().Contain("Khách B"); // đối tượng dropdown
        cut.Markup.Should().Contain("Đối tượng");
    }

    [Fact]
    public void SubLedger_Shows_Error_WhenNoAccountSelected()
    {
        // Arrange — không có tài khoản nào
        var mock = new Mock<IGeneralLedgerService>();
        mock.Setup(s => s.GetAccountsAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountActivityDto>());
        Services.AddSingleton(mock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.SubLedger>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Vui lòng chọn tài khoản"));
    }

    [Fact]
    public void SubLedger_ExportExcel_DownloadsXlsx_WhenClicked()
    {
        // Arrange — nút Xuất Excel → vanAn.downloadFile với mime xlsx (Mẫu S38-DN).
        var mock = new Mock<IGeneralLedgerService>();
        mock.Setup(s => s.GetAccountsAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountActivityDto> { new("131", "Phải thu khách hàng", 5_000_000, 0) });
        mock.Setup(s => s.GetSubLedgerDoiTuongAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), "131", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        mock.Setup(s => s.GetSubLedgerAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), "131", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubLedgerReportDto(
                "131", "Phải thu khách hàng", null, 0, 5_000_000, 0, 5_000_000,
                new List<SubLedgerRowDto>
                {
                    new(new DateTime(2026, 10, 5), "PT-001", "Bán chịu — Khách A", null, "Khách A", 5_000_000, 0, 5_000_000)
                }));
        mock.Setup(s => s.GetTenantInfoAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantInfoDto("Vạn An Test", "123 Lê Lợi, Q.1"));
        Services.AddSingleton(mock.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.SubLedger>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Xuất Excel"));

        // Act
        cut.FindAll("button").First(b => b.TextContent.Contains("Xuất Excel")).Click();

        // Assert — gọi vanAn.downloadFile với base64 + mime xlsx + tên file .xlsx
        cut.WaitForAssertion(() => JSInterop.Invocations.Should().Contain(i => i.Identifier == "vanAn.downloadFile"));
        var args = JSInterop.Invocations.First(i => i.Identifier == "vanAn.downloadFile").Arguments;
        args[1].Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        args[2].ToString()!.Should().EndWith(".xlsx");
    }
}
