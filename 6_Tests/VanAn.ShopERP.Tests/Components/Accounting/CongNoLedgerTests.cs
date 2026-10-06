using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.CongNo;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// THU CHI & CÔNG NỢ MVP (P2.4, SRS §5.4 FR-8/FR-8.1): sổ chi tiết theo đối tượng —
/// cộng dồn Tăng/Giảm/Số dư · modal "Lịch sử thanh toán khoản nợ" · nút Đảo bút toán [G10].
/// </summary>
public class CongNoLedgerTests : ComponentTestBase
{
    private static DoiTuongLedgerDto BuildLedger()
    {
        return new DoiTuongLedgerDto
        {
            AccountCode = "131",
            DoiTuong = "Khách A",
            Year = 2026,
            Month = 10,
            DauKy = 0m,
            CuoiKy = 3_000_000m,
            Lines =
            [
                new DoiTuongLedgerLineDto
                {
                    EntryId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    TransactionDate = new DateTime(2026, 10, 1),
                    Description = "Bán chịu — Khách A",
                    Tang = 5_000_000m,
                    Giam = 0m,
                    SoDu = 5_000_000m
                },
                new DoiTuongLedgerLineDto
                {
                    EntryId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    TransactionDate = new DateTime(2026, 10, 15),
                    Description = "Thu tiền khách trả nợ — Khách A",
                    Tang = 0m,
                    Giam = 2_000_000m,
                    SoDu = 3_000_000m
                }
            ]
        };
    }

    private void RegisterCongNoService(
        DoiTuongLedgerDto? ledger = null,
        KhoanNoPaymentsDto? khoanNo = null)
    {
        var congNoMock = new Mock<ICongNoService>();
        congNoMock.Setup(c => c.GetDoiTuongLedgerAsync(
                It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(ledger ?? BuildLedger());
        if (khoanNo != null)
        {
            congNoMock.Setup(c => c.GetKhoanNoPaymentsAsync(It.IsAny<TenantId>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(khoanNo);
        }
        Services.AddSingleton(congNoMock.Object);
    }

    [Fact]
    public void CongNoLedger_ShouldRender_WithRunningBalance()
    {
        // Arrange
        RegisterCongNoService();

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CongNoLedger>(
            p => p.Add(x => x.Loai, "thu").Add(x => x.DoiTuong, "Khách A"));

        // Assert — sổ phải thu + dòng cộng dồn
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Sổ Theo Dõi Phải Thu"));
        cut.Markup.Should().Contain("Khách A");
        cut.Markup.Should().Contain("Bán chịu — Khách A");
        cut.Markup.Should().Contain("Thu tiền khách trả nợ — Khách A");
        cut.Markup.Should().Contain("Số dư đầu kỳ");
        cut.Markup.Should().Contain("Cuối kỳ");
        cut.Markup.Should().Contain("congno-ledger-table");
    }

    [Fact]
    public void CongNoLedger_InvalidLoai_ShowsError()
    {
        // Arrange
        RegisterCongNoService();

        // Act — loai không hợp lệ
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CongNoLedger>(
            p => p.Add(x => x.Loai, "xyz").Add(x => x.DoiTuong, "Khách A"));

        // Assert — thông báo lỗi
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đường dẫn không hợp lệ"));
    }

    [Fact]
    public void CongNoLedger_KhoanNoHistory_OpensModal_WithPayments()
    {
        // Arrange
        RegisterCongNoService(khoanNo: new KhoanNoPaymentsDto
        {
            KhoanNoEntryId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AccountCode = "131",
            DoiTuong = "Khách A",
            TransactionDate = new DateTime(2026, 10, 1),
            Description = "Bán chịu — Khách A",
            SoTien = 5_000_000m,
            DaThanhToan = 2_000_000m,
            ConLai = 3_000_000m,
            Payments =
            [
                new KhoanNoPaymentLineDto
                {
                    EntryId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    TransactionDate = new DateTime(2026, 10, 15),
                    Description = "Thu tiền khách trả nợ — Khách A",
                    SoTien = 2_000_000m,
                    SoDuConLai = 3_000_000m
                }
            ]
        });

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CongNoLedger>(
            p => p.Add(x => x.Loai, "thu").Add(x => x.DoiTuong, "Khách A"));

        // Click "Lịch sử" trên dòng khoản nợ (Tăng > 0)
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Lịch sử"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Lịch sử")).Click();

        // Assert — modal lịch sử thanh toán hiển thị
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Lịch Sử Thanh Toán Khoản Nợ"));
        cut.Markup.Should().Contain("Còn nợ");
        cut.Markup.Should().Contain("khoan-no-payments-table");
        cut.Markup.Should().Contain("2.000.000");
    }

    [Fact]
    public void CongNoLedger_Reversal_OpensModal_AndCallsReversalService()
    {
        // Arrange
        RegisterCongNoService();
        var reversalMock = new Mock<IReversalService>();
        Services.AddSingleton(reversalMock.Object);

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CongNoLedger>(
            p => p.Add(x => x.Loai, "thu").Add(x => x.DoiTuong, "Khách A"));

        // Click "Đảo" trên dòng đầu
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đảo"));
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Đảo").Click();

        // Assert — modal đảo bút toán
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Đảo Bút Toán"));

        // Nhập lý do + xác nhận
        cut.Find("input").Change("Nhập sai số tiền");
        cut.FindAll("button").First(b => b.TextContent.Contains("Xác Nhận Đảo")).Click();

        // Assert — gọi ReversalService với entry 11111111-1111-1111-1111-111111111111
        reversalMock.Verify(r => r.CreateReversalEntryAsync(
            It.Is<AccountingEntryId>(id => id.Value == Guid.Parse("11111111-1111-1111-1111-111111111111")),
            It.IsAny<TenantId>(),
            "Nhập sai số tiền",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
