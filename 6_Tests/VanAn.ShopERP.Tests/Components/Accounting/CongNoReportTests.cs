using VanAn.CoreHub.Services.CongNo;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// THU CHI & CÔNG NỢ MVP (P2.3, SRS §5.3 FR-7): báo cáo công nợ tổng hợp —
/// 2 khối 131/331 · đầu kỳ/PS/cuối kỳ · tuổi nợ · lọc tháng · chips Tất cả/Còn nợ/Hết nợ.
/// </summary>
public class CongNoReportTests : ComponentTestBase
{
    private static CongNoReportDto BuildReport()
    {
        return new CongNoReportDto
        {
            Year = 2026,
            Month = 10,
            ReportDate = new DateTime(2026, 10, 31),
            Groups =
            [
                new CongNoGroupDto
                {
                    AccountCode = "131",
                    AccountName = "Phải thu khách hàng",
                    Rows =
                    [
                        new CongNoRowDto
                        {
                            DoiTuong = "Khách A",
                            DauKy = 0m,
                            PhatSinh = 5_000_000m,
                            DaThuTra = 2_000_000m,
                            CuoiKy = 3_000_000m,
                            Aging = new CongNoAgingDto { Tu30Den60 = 3_000_000m }
                        },
                        new CongNoRowDto
                        {
                            DoiTuong = "Khách B",
                            DauKy = 0m,
                            PhatSinh = 1_000_000m,
                            DaThuTra = 1_000_000m,
                            CuoiKy = 0m // hết nợ
                        }
                    ],
                    TongDauKy = 0m,
                    TongPhatSinh = 6_000_000m,
                    TongDaThuTra = 3_000_000m,
                    TongCuoiKy = 3_000_000m,
                    TongAging = new CongNoAgingDto { Tu30Den60 = 3_000_000m }
                },
                new CongNoGroupDto
                {
                    AccountCode = "331",
                    AccountName = "Phải trả người bán",
                    Rows = [],
                    TongCuoiKy = 0m
                }
            ]
        };
    }

    private void RegisterCongNoService(CongNoReportDto report)
    {
        var congNoMock = new Mock<ICongNoService>();
        congNoMock.Setup(c => c.GetCongNoReportAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(report);
        Services.AddSingleton(congNoMock.Object);
    }

    [Fact]
    public void CongNoReport_ShouldRender_WhenComponentMounted()
    {
        // Arrange
        RegisterCongNoService(BuildReport());

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CongNoReport>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Báo Cáo Công Nợ"));
        cut.Markup.Should().Contain("Bộ Lọc");
    }

    [Fact]
    public void CongNoReport_ShouldRenderTwoBlocks_WithRowsAndTotals()
    {
        // Arrange
        RegisterCongNoService(BuildReport());

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CongNoReport>();

        // Assert — 2 khối 131/331
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("congno-group-131"));
        cut.Markup.Should().Contain("congno-group-331");

        // Row dữ liệu: Khách A cuối kỳ 3.000.000 + tuổi nợ
        cut.Markup.Should().Contain("Khách A");
        cut.Markup.Should().Contain("3.000.000");
        // Tổng
        cut.Markup.Should().Contain("TỔNG");
    }

    [Fact]
    public void CongNoReport_ShouldRenderFilterChips()
    {
        // Arrange
        RegisterCongNoService(BuildReport());

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CongNoReport>();

        // Assert — chips lọc
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("congno-filter-chip"));
        cut.Markup.Should().Contain("Tất cả");
        cut.Markup.Should().Contain("Còn nợ");
        cut.Markup.Should().Contain("Hết nợ");
    }

    [Fact]
    public void CongNoReport_FilterDue_HidesPaidOffRows()
    {
        // Arrange
        RegisterCongNoService(BuildReport());

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CongNoReport>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Khách B"));

        // Bấm "Còn nợ" → chỉ còn đối tượng có số dư
        cut.FindAll("button").First(b => b.TextContent.Contains("Còn nợ")).Click();
        cut.WaitForAssertion(() => cut.Markup.Should().NotContain("Khách B"));
        cut.Markup.Should().Contain("Khách A");
    }

    [Fact]
    public void CongNoReport_RowLink_NavigatesToLedgerUrl()
    {
        // Arrange
        RegisterCongNoService(BuildReport());

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CongNoReport>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("congno-row-link"));

        // Assert — link đối tượng 131 → /accounting/cong-no/thu/{doiTuong}?year=&month=
        var link = cut.FindAll("a[data-testid='congno-row-link']").First();
        link.GetAttribute("href").Should().Contain("/accounting/cong-no/thu/");
        link.GetAttribute("href").Should().Contain("year=");
        link.GetAttribute("href").Should().Contain("month=");
    }
}
