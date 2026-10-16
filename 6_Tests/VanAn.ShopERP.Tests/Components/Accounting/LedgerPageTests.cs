using Moq;
using VanAn.CoreHub.Services.GeneralLedger;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// TUÂN THỦ PHÁP LÝ P0.2-Đ1 (2026-10-16): trang Sổ Cái —
/// render header TK (dư đầu kỳ/PS Nợ/PS Có/dư cuối kỳ) + dòng phát sinh.
/// </summary>
public class LedgerPageTests : ComponentTestBase
{
    private void RegisterLedgerService()
    {
        var mock = new Mock<IGeneralLedgerService>();
        mock.Setup(s => s.GetAccountsAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountActivityDto>
            {
                new("111", "Tiền mặt", 3_000_000, 0),
                new("511", "Doanh thu", 0, 3_000_000)
            });
        mock.Setup(s => s.GetLedgerAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), "111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerReportDto(
                "111",
                "Tiền mặt",
                5_000_000,
                3_000_000,
                2_000_000,
                6_000_000,
                new List<LedgerRowDto>
                {
                    new(new DateTime(2026, 7, 5), "J20260705-ABC", "Thu 7", 3_000_000, 0, 8_000_000),
                    new(new DateTime(2026, 7, 15), "J20260715-DEF", "Chi 7", 0, 2_000_000, 6_000_000)
                }));
        Services.AddSingleton(mock.Object);
    }

    [Fact]
    public void Ledger_Renders_HeaderAndRows()
    {
        // Arrange
        RegisterLedgerService();

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.Ledger>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Sổ Cái"));
        cut.Markup.Should().Contain("Tiền mặt");
        cut.Markup.Should().Contain("Dư đầu kỳ");
        cut.Markup.Should().Contain("Dư cuối kỳ");
        cut.Markup.Should().Contain("Thu 7");
        cut.Markup.Should().Contain("Chi 7");
    }

    [Fact]
    public void Ledger_Shows_Error_WhenNoAccountSelected()
    {
        // Arrange — không có tài khoản nào
        var mock = new Mock<IGeneralLedgerService>();
        mock.Setup(s => s.GetAccountsAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccountActivityDto>());
        Services.AddSingleton(mock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.Ledger>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Vui lòng chọn tài khoản"));
    }
}