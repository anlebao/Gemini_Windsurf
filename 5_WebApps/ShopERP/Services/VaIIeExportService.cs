using System.Globalization;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.CoreHub.Services.InventoryIntelligence.Dtos;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// VA-IIE Phase 4 (2026-10-05): Export Excel cho báo cáo ca + forecast + lợi nhuận (SRS §5.2, §8.1/8.6).
    /// EPPlus (đã có trong Directory.Packages.props 7.6.1 — precedent FinancialExportService).
    /// PDF = print view qua vananPrintBill (window.print) — repo không có PDF lib server-side (task card §5.4).
    /// </summary>
    public static class VaIIeExportService
    {
        /// <summary>Export Shift Report đầy đủ (6 phần — SRS §5.1).</summary>
        public static async Task<byte[]> ExportShiftReportExcelAsync(ShiftReportDto report)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();
            ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Báo cáo ca");
            sheet.Cells[1, 1].Value = $"BÁO CÁO CUỐI CA — {report.Shift.StartTime.ToLocalTime():dd/MM/yyyy}";
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 14;

            string[][] info =
            [
                ["Chỉ tiêu", "Giá trị"],
                ["Loại ca", report.Shift.ShiftType.ToString()],
                ["Trạng thái", report.Shift.Status.ToString()],
                ["Mở ca", report.Shift.StartTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)],
                ["Đóng ca", report.Shift.EndTime?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "—"],
                ["Ghi chú bàn giao", report.Shift.HandoverNotes ?? "—"],
                ["Tiền mặt đếm", FormatVnd(report.Cash.CashCount)],
                ["Tổng tiền POS", FormatVnd(report.Cash.PosCashTotal)],
                ["Chênh lệch tiền mặt", FormatVnd(report.Cash.Difference)],
            ];
            WriteRows(sheet, info, startRow: 3);

            // Phần kiểm kê (đầu + cuối)
            int headerRow = 3 + info.Length + 1;
            WriteTableHeader(sheet, headerRow, ["Nguyên liệu", "Loại", "Số lượng", "Đơn vị", "Nhập thêm"]);
            int row = headerRow + 1;
            foreach (InventoryCountDto c in report.OpeningCounts.Concat(report.ClosingCounts))
            {
                sheet.Cells[row, 1].Value = c.IngredientName;
                sheet.Cells[row, 2].Value = c.CountType == CountType.Opening ? "Đầu ca" : "Cuối ca";
                sheet.Cells[row, 3].Value = (double)c.Quantity;
                sheet.Cells[row, 4].Value = c.Unit;
                sheet.Cells[row, 5].Value = c.MidShiftStockIn is null ? "—" : (double)c.MidShiftStockIn.Value;
                row++;
            }
            row++;

            // Phần tiêu hao + variance
            WriteTableHeader(sheet, row, ["Nguyên liệu", "Lý thuyết", "Thực tế", "Variance", "Variance %"]);
            row++;
            foreach (ConsumptionDto c in report.Consumptions)
            {
                sheet.Cells[row, 1].Value = c.IngredientName;
                sheet.Cells[row, 2].Value = (double)c.TheoreticalQuantity;
                sheet.Cells[row, 3].Value = (double)c.ActualQuantity;
                sheet.Cells[row, 4].Value = (double)c.Variance;
                sheet.Cells[row, 5].Value = (double)c.VariancePercent;
                row++;
            }
            row++;

            // Phần cảnh báo
            WriteTableHeader(sheet, row, ["Mã", "Mức độ", "Nội dung"]);
            row++;
            foreach (ShiftAlertDto a in report.Alerts)
            {
                sheet.Cells[row, 1].Value = a.AlertCode;
                sheet.Cells[row, 2].Value = a.Severity.ToString();
                sheet.Cells[row, 3].Value = a.Message;
                row++;
            }

            // Phần đơn hàng
            row++;
            WriteTableHeader(sheet, row, ["Đơn hàng", "Loại", "Tổng tiền", "Thời gian", "Số món"]);
            row++;
            foreach (OrderSummaryDto o in report.Orders)
            {
                sheet.Cells[row, 1].Value = o.OrderId.ToString();
                sheet.Cells[row, 2].Value = o.OrderType;
                sheet.Cells[row, 3].Value = (double)o.TotalPrice;
                sheet.Cells[row, 4].Value = o.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                sheet.Cells[row, 5].Value = o.ItemCount;
                row++;
            }

            sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
            return await package.GetAsByteArrayAsync();
        }

        /// <summary>Export Restock + Stockout forecast (SRS §8.6).</summary>
        public static async Task<byte[]> ExportForecastExcelAsync(ForecastReport report)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();

            // Sheet Restock
            ExcelWorksheet restockSheet = package.Workbook.Worksheets.Add("Restock");
            restockSheet.Cells[1, 1].Value = $"RESTOCK FORECAST — {report.GeneratedAt.ToLocalTime():dd/MM/yyyy HH:mm}";
            restockSheet.Cells[1, 1].Style.Font.Bold = true;
            restockSheet.Cells[1, 1].Style.Font.Size = 14;
            restockSheet.Cells[2, 1].Value = $"Window {report.WindowDays} ngày · Lead {report.LeadTimeDays} ngày · Safety {report.SafetyDays} ngày";
            WriteTableHeader(restockSheet, 4, ["Nguyên liệu", "Tồn kho", "Tiêu hao/ngày", "Ngày còn lại", "Đề xuất nhập", "Mức độ"]);
            int row = 5;
            foreach (ForecastItem i in report.RestockItems)
            {
                restockSheet.Cells[row, 1].Value = i.Name;
                restockSheet.Cells[row, 2].Value = (double)i.CurrentStock;
                restockSheet.Cells[row, 3].Value = (double)i.AvgDailyConsumption;
                restockSheet.Cells[row, 4].Value = i.DaysRemaining == int.MaxValue ? "—" : i.DaysRemaining;
                restockSheet.Cells[row, 5].Value = (double)i.SuggestedOrderQuantity;
                restockSheet.Cells[row, 6].Value = StatusName(i.Status);
                row++;
            }
            restockSheet.Cells[restockSheet.Dimension?.Address ?? "A1"].AutoFitColumns();

            // Sheet Stockout
            ExcelWorksheet stockoutSheet = package.Workbook.Worksheets.Add("Stockout");
            stockoutSheet.Cells[1, 1].Value = "STOCKOUT FORECAST — ngày còn lại theo tiêu hao trung bình";
            stockoutSheet.Cells[1, 1].Style.Font.Bold = true;
            stockoutSheet.Cells[1, 1].Style.Font.Size = 14;
            WriteTableHeader(stockoutSheet, 3, ["Nguyên liệu", "Tồn kho", "Tiêu hao/ngày", "Ngày còn lại", "Ngưỡng (Lead+Safety)", "Mức độ"]);
            row = 4;
            foreach (ForecastItem i in report.StockoutItems)
            {
                stockoutSheet.Cells[row, 1].Value = i.Name;
                stockoutSheet.Cells[row, 2].Value = (double)i.CurrentStock;
                stockoutSheet.Cells[row, 3].Value = (double)i.AvgDailyConsumption;
                stockoutSheet.Cells[row, 4].Value = i.DaysRemaining == int.MaxValue ? "—" : i.DaysRemaining;
                stockoutSheet.Cells[row, 5].Value = i.ThresholdDays;
                stockoutSheet.Cells[row, 6].Value = StatusName(i.Status);
                row++;
            }
            stockoutSheet.Cells[stockoutSheet.Dimension?.Address ?? "A1"].AutoFitColumns();

            return await package.GetAsByteArrayAsync();
        }

        /// <summary>Export Profitability per Item / per Shift (SRS §5.2).</summary>
        public static async Task<byte[]> ExportProfitabilityExcelAsync(ProfitabilityReport report)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();
            ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Lợi nhuận ca");

            sheet.Cells[1, 1].Value = "LỢI NHUẬN CA";
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 14;

            string[][] summary =
            [
                ["Chỉ tiêu", "Giá trị"],
                ["Doanh số ca", FormatVnd(report.Sales)],
                ["COGS (thực tế)", FormatVnd(report.Cogs)],
                ["Lợi nhuận ca", FormatVnd(report.ShiftProfit)],
                ["Tỷ lệ lợi nhuận", report.ShiftProfitPercent.ToString("F1", CultureInfo.InvariantCulture) + "%"],
            ];
            WriteRows(sheet, summary, startRow: 3);

            WriteTableHeader(sheet, 9, ["Món", "SL bán", "Doanh thu", "Food Cost", "Lợi nhuận", "Tỷ lệ %"]);
            int row = 10;
            foreach (ProfitabilityPerItem i in report.Items)
            {
                sheet.Cells[row, 1].Value = i.ProductName;
                sheet.Cells[row, 2].Value = i.UnitsSold;
                sheet.Cells[row, 3].Value = (double)i.Revenue;
                sheet.Cells[row, 4].Value = (double)i.FoodCost;
                sheet.Cells[row, 5].Value = (double)i.Profit;
                sheet.Cells[row, 6].Value = (double)i.ProfitPercent;
                sheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                sheet.Cells[row, 6].Style.Numberformat.Format = "0.0";
                row++;
            }
            sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
            return await package.GetAsByteArrayAsync();
        }

        private static void WriteRows(ExcelWorksheet sheet, string[][] rows, int startRow)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    sheet.Cells[startRow + r, 1 + c].Value = rows[r][c];
                }
                if (r == 0)
                {
                    for (int c = 0; c < rows[r].Length; c++)
                    {
                        sheet.Cells[startRow + r, 1 + c].Style.Font.Bold = true;
                        sheet.Cells[startRow + r, 1 + c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[startRow + r, 1 + c].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }
                }
            }
        }

        private static void WriteTableHeader(ExcelWorksheet sheet, int row, string[] headers)
        {
            for (int c = 0; c < headers.Length; c++)
            {
                sheet.Cells[row, 1 + c].Value = headers[c];
                sheet.Cells[row, 1 + c].Style.Font.Bold = true;
                sheet.Cells[row, 1 + c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[row, 1 + c].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }
        }

        private static string StatusName(ForecastStatus status) => status switch
        {
            ForecastStatus.Critical => "Nguy cơ hết",
            ForecastStatus.Warning => "Sắp cạn",
            ForecastStatus.Ok => "Đủ",
            _ => "Chưa đủ dữ liệu"
        };

        private static string FormatVnd(decimal? v) => v is null ? "—" : v.Value.ToString("N0", CultureInfo.InvariantCulture) + " ₫";
    }
}
