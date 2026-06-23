using System.Globalization;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Reports
{
    /// <summary>
    /// Wave 3: Revenue Excel report generator.
    /// Two sheets: summary and order detail. VND currency formatting.
    /// </summary>
    public static class RevenueExcelReport
    {
        public const string VndFormat = "#,##0\" ₫\"";

        public static async Task<byte[]> GenerateAsync(IReadOnlyList<Order> orders, DateTime from, DateTime to)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using ExcelPackage package = new();

            // Sheet 1: Tóm tắt
            ExcelWorksheet summarySheet = package.Workbook.Worksheets.Add("Tóm tắt");
            BuildSummarySheet(summarySheet, orders, from, to);

            // Sheet 2: Chi tiết đơn hàng
            ExcelWorksheet detailSheet = package.Workbook.Worksheets.Add("Chi tiết đơn hàng");
            BuildDetailSheet(detailSheet, orders);

            return await package.GetAsByteArrayAsync();
        }

        private static void BuildSummarySheet(ExcelWorksheet sheet, IReadOnlyList<Order> orders, DateTime from, DateTime to)
        {
            sheet.Cells[1, 1].Value = "BÁO CÁO DOANH THU";
            sheet.Cells[1, 1].Style.Font.Bold = true;
            sheet.Cells[1, 1].Style.Font.Size = 16;

            sheet.Cells[2, 1].Value = $"Từ {from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} đến {to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            sheet.Cells[2, 1].Style.Font.Italic = true;

            decimal totalRevenue = orders.Sum(o => o.TotalAmount);
            decimal totalVat = orders.Sum(o => o.TotalVatAmount);
            decimal totalShipping = orders.Sum(o => o.ShippingFee);
            decimal totalDiscount = orders.Sum(o => o.DiscountAmount);
            int completedCount = orders.Count(o => o.Status.Value == "Completed");

            string[] labels = ["Tổng doanh thu", "Tổng VAT", "Phí vận chuyển", "Giảm giá", "Số đơn hàng", "Đơn hoàn thành"];
            object[] values = [totalRevenue, totalVat, totalShipping, totalDiscount, orders.Count, completedCount];

            for (int i = 0; i < labels.Length; i++)
            {
                int row = 4 + i;
                sheet.Cells[row, 1].Value = labels[i];
                sheet.Cells[row, 1].Style.Font.Bold = true;
                sheet.Cells[row, 2].Value = values[i];
                if (values[i] is decimal)
                {
                    sheet.Cells[row, 2].Style.Numberformat.Format = VndFormat;
                }
            }

            // Daily breakdown table
            int dailyStartRow = 12;
            sheet.Cells[dailyStartRow, 1].Value = "Doanh thu theo ngày";
            sheet.Cells[dailyStartRow, 1].Style.Font.Bold = true;
            sheet.Cells[dailyStartRow, 1].Style.Font.Size = 12;

            dailyStartRow++;
            string[] dailyHeaders = ["Ngày", "Số đơn", "Doanh thu", "VAT", "Thuế suất VAT TB"];
            for (int i = 0; i < dailyHeaders.Length; i++)
            {
                sheet.Cells[dailyStartRow, 1 + i].Value = dailyHeaders[i];
                sheet.Cells[dailyStartRow, 1 + i].Style.Font.Bold = true;
                sheet.Cells[dailyStartRow, 1 + i].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[dailyStartRow, 1 + i].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            var dailyGroups = orders
                .GroupBy(o => o.OrderDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount),
                    Vat = g.Sum(o => o.TotalVatAmount),
                    AvgVatRate = g.Any() ? g.Average(o => o.TotalVatAmount / o.TotalAmount * 100) : 0
                })
                .ToList();

            int rowIndex = dailyStartRow + 1;
            foreach (var day in dailyGroups)
            {
                sheet.Cells[rowIndex, 1].Value = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                sheet.Cells[rowIndex, 2].Value = day.Count;
                sheet.Cells[rowIndex, 3].Value = day.Revenue;
                sheet.Cells[rowIndex, 3].Style.Numberformat.Format = VndFormat;
                sheet.Cells[rowIndex, 4].Value = day.Vat;
                sheet.Cells[rowIndex, 4].Style.Numberformat.Format = VndFormat;
                sheet.Cells[rowIndex, 5].Value = day.AvgVatRate / 100;
                sheet.Cells[rowIndex, 5].Style.Numberformat.Format = "0.00%";
                rowIndex++;
            }

            sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
        }

        private static void BuildDetailSheet(ExcelWorksheet sheet, IReadOnlyList<Order> orders)
        {
            string[] headers = ["Mã đơn hàng", "Ngày đặt", "Khách hàng", "Loại đơn", "Trạng thái", "Thanh toán", "Tổng tiền", "VAT", "Phí ship", "Giảm giá", "Ghi chú"];
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, 1 + i].Value = headers[i];
                sheet.Cells[1, 1 + i].Style.Font.Bold = true;
                sheet.Cells[1, 1 + i].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, 1 + i].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            int row = 2;
            foreach (Order order in orders.OrderByDescending(o => o.OrderDate))
            {
                sheet.Cells[row, 1].Value = order.OrderId.Value.ToString();
                sheet.Cells[row, 2].Value = order.OrderDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                sheet.Cells[row, 3].Value = order.Customer?.FullName ?? order.CustomerDeviceId ?? "Khách vãng lai";
                sheet.Cells[row, 4].Value = order.OrderType;
                sheet.Cells[row, 5].Value = order.Status.Value;
                sheet.Cells[row, 6].Value = order.PaymentMethod ?? "N/A";
                sheet.Cells[row, 7].Value = order.TotalAmount;
                sheet.Cells[row, 7].Style.Numberformat.Format = VndFormat;
                sheet.Cells[row, 8].Value = order.TotalVatAmount;
                sheet.Cells[row, 8].Style.Numberformat.Format = VndFormat;
                sheet.Cells[row, 9].Value = order.ShippingFee;
                sheet.Cells[row, 9].Style.Numberformat.Format = VndFormat;
                sheet.Cells[row, 10].Value = order.DiscountAmount;
                sheet.Cells[row, 10].Style.Numberformat.Format = VndFormat;
                sheet.Cells[row, 11].Value = order.CustomerNotes;
                row++;
            }

            sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
        }
    }
}
