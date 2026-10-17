using OfficeOpenXml;
using VanAn.CoreHub.Services.GeneralLedger;
using VanAn.Shared.DTOs;
using VanAn.ShopERP.Services;

namespace VanAn.ShopERP.Tests.Services;

/// <summary>
/// 2026-10-17 (user directive): Xuất báo cáo công nợ theo mẫu KeToan_CongNo_BaoCao —
/// 1 workbook 2 sheets: "Phải thu KH (131)" (mẫu 4) + "Phải trả NCC (331)" (mẫu 1).
/// Verify cấu trúc sheet + quy ước dấu: 131 dương→Nợ/âm→Có · 331 dương→Có/âm→Nợ.
/// </summary>
public class LedgerExcelExportServiceTests
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
                        new CongNoRowDto { DoiTuong = "Khách A", DauKy = 1_000_000m, PhatSinh = 5_000_000m, DaThuTra = 2_000_000m, CuoiKy = 4_000_000m },
                        new CongNoRowDto { DoiTuong = "Khách B (trả trước)", DauKy = 0, PhatSinh = 0, DaThuTra = 500_000m, CuoiKy = -500_000m },
                    ],
                    TongDauKy = 1_000_000m,
                    TongPhatSinh = 5_000_000m,
                    TongDaThuTra = 2_500_000m,
                    TongCuoiKy = 3_500_000m,
                },
                new CongNoGroupDto
                {
                    AccountCode = "331",
                    AccountName = "Phải trả người bán",
                    Rows =
                    [
                        new CongNoRowDto { DoiTuong = "NCC X", DauKy = 10_000_000m, PhatSinh = 3_000_000m, DaThuTra = 4_000_000m, CuoiKy = 9_000_000m },
                        new CongNoRowDto { DoiTuong = "NCC Y (ứng trước)", DauKy = -2_000_000m, PhatSinh = 1_000_000m, DaThuTra = 0, CuoiKy = -1_000_000m },
                    ],
                    TongDauKy = 8_000_000m,
                    TongPhatSinh = 4_000_000m,
                    TongDaThuTra = 4_000_000m,
                    TongCuoiKy = 8_000_000m,
                },
            ]
        };
    }

    [Fact]
    public void ExportCongNoTongHopExcel_ProducesTwoSheets_WithTemplateHeaders()
    {
        byte[] bytes = LedgerExcelExportService.ExportCongNoTongHopExcel(
            BuildReport(), new TenantInfoDto("Vạn An Test", "123 Lê Lợi"));

        using var ms = new MemoryStream(bytes);
        using var pkg = new ExcelPackage(ms);

        pkg.Workbook.Worksheets.Select(w => w.Name).Should()
            .Equal("Phải thu KH (131)", "Phải trả NCC (331)");

        var thu = pkg.Workbook.Worksheets["Phải thu KH (131)"];
        thu.Cells["A1"].Text.Should().Be("TỔNG HỢP CÔNG NỢ PHẢI THU KHÁCH HÀNG");
        thu.Cells["A2"].Text.Should().Contain("Vạn An Test");
        thu.Cells["A5"].Text.Should().Be("Mã khách hàng");
        thu.Cells["B5"].Text.Should().Be("Tên khách hàng");
        thu.Cells["C5"].Text.Should().Be("TK công nợ");
        thu.Cells["D5"].Text.Should().Be("Số dư đầu kỳ");
        thu.Cells["F5"].Text.Should().Be("Phát sinh");
        thu.Cells["H5"].Text.Should().Be("Số dư cuối kỳ");

        var tra = pkg.Workbook.Worksheets["Phải trả NCC (331)"];
        tra.Cells["A1"].Text.Should().Be("TỔNG HỢP CÔNG NỢ PHẢI TRẢ NHÀ CUNG CẤP");
        tra.Cells["A5"].Text.Should().Be("Mã nhà cung cấp");
        tra.Cells["D5"].Text.Should().Be("Mã số thuế");
        tra.Cells["I5"].Text.Should().Be("Lũy kế phát sinh");
        tra.Cells["O5"].Text.Should().Be("Số nợ tối đa");
        // Dòng "TK Công nợ: 331" đúng mẫu
        tra.Cells["A7"].Text.Should().Be("TK Công nợ: 331");
    }

    [Fact]
    public void ExportCongNoTongHopExcel_Sheet131_DebitPositiveCreditNegative()
    {
        byte[] bytes = LedgerExcelExportService.ExportCongNoTongHopExcel(BuildReport(), null);
        using var pkg = new ExcelPackage(new MemoryStream(bytes));
        var thu = pkg.Workbook.Worksheets["Phải thu KH (131)"];

        // Row 7 = Khách A: đầu kỳ dương → D (Nợ); PS tăng → F (Nợ); đã thu → G (Có); cuối kỳ → H (Nợ)
        thu.Cells["B7"].Text.Should().Be("Khách A");
        thu.Cells["C7"].Text.Should().Be("131");
        thu.Cells["D7"].GetValue<double>().Should().Be(1_000_000);
        thu.Cells["F7"].GetValue<double>().Should().Be(5_000_000);
        thu.Cells["G7"].GetValue<double>().Should().Be(2_000_000);
        thu.Cells["H7"].GetValue<double>().Should().Be(4_000_000);

        // Row 8 = Khách B âm cuối kỳ → cột I (Có)
        thu.Cells["B8"].Text.Should().Be("Khách B (trả trước)");
        thu.Cells["I8"].GetValue<double>().Should().Be(500_000);

        // Tổng cộng
        thu.Cells["A9"].Text.Should().Be("Tổng cộng");
        thu.Cells["H9"].GetValue<double>().Should().Be(3_500_000);
    }

    [Fact]
    public void ExportCongNoTongHopExcel_Sheet331_CreditPositive_AndLuyKe()
    {
        byte[] bytes = LedgerExcelExportService.ExportCongNoTongHopExcel(BuildReport(), null);
        using var pkg = new ExcelPackage(new MemoryStream(bytes));
        var tra = pkg.Workbook.Worksheets["Phải trả NCC (331)"];

        // Row 8 = NCC X: đầu kỳ dương → F (Có 10tr); đã trả 4tr → G (Nợ); PS 3tr → H (Có);
        // lũy kế: Nợ = 4tr (I), Có = 10+3 = 13tr (J); cuối kỳ 9tr → L (Có)
        tra.Cells["B8"].Text.Should().Be("NCC X");
        tra.Cells["F8"].GetValue<double>().Should().Be(10_000_000);
        tra.Cells["G8"].GetValue<double>().Should().Be(4_000_000);
        tra.Cells["H8"].GetValue<double>().Should().Be(3_000_000);
        tra.Cells["I8"].GetValue<double>().Should().Be(4_000_000);
        tra.Cells["J8"].GetValue<double>().Should().Be(13_000_000);
        tra.Cells["L8"].GetValue<double>().Should().Be(9_000_000);

        // Row 9 = NCC Y ứng trước: đầu kỳ âm → E (Nợ 2tr); cuối kỳ âm → K (Nợ 1tr)
        tra.Cells["B9"].Text.Should().Be("NCC Y (ứng trước)");
        tra.Cells["E9"].GetValue<double>().Should().Be(2_000_000);
        tra.Cells["K9"].GetValue<double>().Should().Be(1_000_000);
        // Lũy kế Nợ = |dư đầu âm| + đã trả = 2tr + 0 = 2tr; Có = PS = 1tr
        tra.Cells["I9"].GetValue<double>().Should().Be(2_000_000);
        tra.Cells["J9"].GetValue<double>().Should().Be(1_000_000);
    }
}
