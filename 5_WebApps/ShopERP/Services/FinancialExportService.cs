using System.Globalization;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using VanAn.CoreHub.Services.FinancialIntelligence.Dtos;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// VA-FI-MVP2 Phase 5 (2026-08-21): Excel export for Financial Intelligence reports.
    /// Uses EPPlus (already in Directory.Packages.props 7.6.1).
    /// Precedent: InventoryExcelReport.cs (Wave 3).
    /// Generates .xlsx for Break-even (single + multi) and Unit Economics.
    /// </summary>
    public static class FinancialExportService
    {
        public static async Task<byte[]> ExportBreakEvenAsync(
            BreakEvenAnalysisDto single,
            MultiProductBreakEvenDto? multi)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();

            // Sheet 1: Single break-even
            ExcelWorksheet sheet1 = package.Workbook.Worksheets.Add("Hòa vốn tổng hợp");
            WriteTitle(sheet1, $"Phân tích điểm hòa vốn — Kỳ {single.Year}-{single.Month:D2}");
            string[][] rows =
            [
                ["Chỉ tiêu", "Giá trị"],
                ["Tổng chi phí cố định", FormatVnd(single.TotalFixedCost)],
                ["Tổng doanh thu", FormatVnd(single.TotalRevenue)],
                ["Tổng chi phí biến đổi", FormatVnd(single.TotalVariableCost)],
                ["Tổng biên đóng góp", FormatVnd(single.TotalContributionMargin)],
                ["Tỷ lệ biên đóng góp", FormatPercent(single.ContributionMarginRatio)],
                ["Doanh thu hòa vốn", FormatVnd(single.BreakEvenRevenue)],
                ["Sản lượng hòa vốn", FormatUnits(single.BreakEvenUnits)],
                ["Biên an toàn (VND)", FormatVnd(single.MarginOfSafetyRevenue)],
                ["Biên an toàn (%)", FormatPercent(single.MarginOfSafetyPercent)],
                ["Trạng thái", BreakEvenStatusLabel(single.Status)],
                ["Phiên bản mô hình", single.ModelVersion],
            ];
            WriteRows(sheet1, rows, startRow: 3);

            // Sheet 2: Multi-product
            if (multi != null && multi.ProductLines.Count > 0)
            {
                ExcelWorksheet sheet2 = package.Workbook.Worksheets.Add("Hòa vốn đa sản phẩm");
                WriteTitle(sheet2, $"Hòa vốn đa sản phẩm — Kỳ {multi.Year}-{multi.Month:D2}");
                string[] headers = ["Sản phẩm", "Giá bán", "Chi phí biến đổi", "Biên đóng góp", "Tỷ lệ BCM", "Cơ cấu bán", "SL bán kỳ", "SL hòa vốn"];
                for (int i = 0; i < headers.Length; i++)
                {
                    sheet2.Cells[3, 1 + i].Value = headers[i];
                    sheet2.Cells[3, 1 + i].Style.Font.Bold = true;
                    sheet2.Cells[3, 1 + i].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet2.Cells[3, 1 + i].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }
                int row = 4;
                foreach (var line in multi.ProductLines)
                {
                    sheet2.Cells[row, 1].Value = line.ProductName;
                    sheet2.Cells[row, 2].Value = (double)line.SellingPrice;
                    sheet2.Cells[row, 3].Value = (double)line.VariableCost;
                    sheet2.Cells[row, 4].Value = (double)line.ContributionMargin;
                    sheet2.Cells[row, 5].Value = (double)line.ContributionMarginRatio;
                    sheet2.Cells[row, 6].Value = (double)line.SalesMixPercent;
                    sheet2.Cells[row, 7].Value = line.UnitsSoldInPeriod;
                    sheet2.Cells[row, 8].Value = line.ProductBreakEvenUnits > 0m ? (double)line.ProductBreakEvenUnits : 0;
                    sheet2.Cells[row, 8].Style.Numberformat.Format = line.ProductBreakEvenUnits > 0m ? "#,##0" : "\"N/A\"";
                    for (int c = 2; c <= 6; c++) sheet2.Cells[row, c].Style.Numberformat.Format = "#,##0";
                    row++;
                }
                sheet2.Cells[sheet2.Dimension?.Address ?? "A1"].AutoFitColumns();
            }

            return await package.GetAsByteArrayAsync();
        }

        public static async Task<byte[]> ExportUnitEconomicsAsync(UnitEconomicsReportDto report)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();
            ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Kinh tế đơn vị");
            WriteTitle(sheet, $"Kinh tế đơn vị sản phẩm — Kỳ {report.Year}-{report.Month:D2}");

            // Summary row
            sheet.Cells[3, 1].Value = "Tổng sản phẩm phân tích"; sheet.Cells[3, 2].Value = report.TotalProductsAnalyzed;
            sheet.Cells[4, 1].Value = "Tổng biên đóng góp"; sheet.Cells[4, 2].Value = (double)report.TotalContribution;
            sheet.Cells[4, 2].Style.Numberformat.Format = "#,##0";
            sheet.Cells[5, 1].Value = "Biên đóng góp TB"; sheet.Cells[5, 2].Value = FormatPercent(report.AverageContributionMargin);
            sheet.Cells[6, 1].Value = "Sản phẩm thiếu giá vốn"; sheet.Cells[6, 2].Value = report.ProductsWithMissingCostPrice;

            string[] headers = ["Sản phẩm", "Nhóm", "Giá bán", "Chi phí biến đổi", "Biên đóng góp", "Tỷ lệ BCM", "SL bán", "Doanh thu", "Đóng góp LN", "Hạng", "Thiếu giá vốn"];
            int headerRow = 8;
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[headerRow, 1 + i].Value = headers[i];
                sheet.Cells[headerRow, 1 + i].Style.Font.Bold = true;
                sheet.Cells[headerRow, 1 + i].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[headerRow, 1 + i].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }
            int row = headerRow + 1;
            foreach (var p in report.Products.OrderByDescending(p => p.ProfitContribution))
            {
                sheet.Cells[row, 1].Value = p.ProductName;
                sheet.Cells[row, 2].Value = p.Category;
                sheet.Cells[row, 3].Value = (double)p.SellingPrice;
                sheet.Cells[row, 4].Value = (double)p.VariableCost;
                sheet.Cells[row, 5].Value = (double)p.ContributionMargin;
                sheet.Cells[row, 6].Value = (double)p.ContributionMarginPercent;
                sheet.Cells[row, 7].Value = p.UnitsSold;
                sheet.Cells[row, 8].Value = (double)p.Revenue;
                sheet.Cells[row, 9].Value = (double)p.ProfitContribution;
                sheet.Cells[row, 10].Value = p.ProfitContributionRank;
                sheet.Cells[row, 11].Value = p.HasMissingCostPrice ? "Có" : "—";
                for (int c = 3; c <= 9; c++) sheet.Cells[row, c].Style.Numberformat.Format = "#,##0";
                if (p.HasMissingCostPrice)
                {
                    for (int col = 1; col <= 11; col++)
                    {
                        sheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 235, 156));
                    }
                }
                row++;
            }
            sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
            return await package.GetAsByteArrayAsync();
        }

        private static void WriteTitle(ExcelWorksheet sheet, string title)
        {
            sheet.Cells[1, 1].Value = title;
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 14;
        }

        private static void WriteRows(ExcelWorksheet sheet, string[][] rows, int startRow)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                    sheet.Cells[startRow + r, 1 + c].Value = rows[r][c];
                if (r == 0) // header
                {
                    for (int c = 0; c < rows[r].Length; c++)
                    {
                        sheet.Cells[startRow + r, 1 + c].Style.Font.Bold = true;
                        sheet.Cells[startRow + r, 1 + c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[startRow + r, 1 + c].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }
                }
            }
            sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
        }

        private static string FormatVnd(decimal v) => v.ToString("N0", CultureInfo.InvariantCulture) + " ₫";
        private static string FormatPercent(decimal v) => v.ToString("N1", CultureInfo.InvariantCulture) + "%";
        // Bug 2 Excel fix: show "N/A" when break-even units = 0 (CM=0 or no orders)
        private static string FormatUnits(decimal v) => v <= 0m ? "N/A" : v.ToString("N0", CultureInfo.InvariantCulture);
        private static string BreakEvenStatusLabel(BreakEvenStatus s) => s switch
        {
            BreakEvenStatus.AboveBreakEven => "Vượt hòa vốn",
            BreakEvenStatus.AtBreakEven => "Đạt hòa vốn",
            BreakEvenStatus.BelowBreakEven => "Chưa đạt hòa vốn",
            BreakEvenStatus.InsufficientData => "Chưa đủ dữ liệu",
            _ => "—"
        };
    }
}
