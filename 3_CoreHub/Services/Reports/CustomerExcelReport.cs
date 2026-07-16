using System.Globalization;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Reports
{
    /// <summary>
    /// Wave 3: Customer Excel report generator.
    /// Includes loyalty tier color coding.
    /// </summary>
    public static class CustomerExcelReport
    {
        public static async Task<byte[]> GenerateAsync(IReadOnlyList<Customer> customers)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using ExcelPackage package = new();
            ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Khách hàng");

            string[] headers = ["Mã khách hàng", "Họ tên", "Số điện thoại", "Email", "Điểm tích lũy", "Hạng", "Tổng chi tiêu", "Đơn hàng cuối", "Trạng thái"];
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, 1 + i].Value = headers[i];
                sheet.Cells[1, 1 + i].Style.Font.Bold = true;
                sheet.Cells[1, 1 + i].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, 1 + i].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            int row = 2;
            foreach (Customer customer in customers.OrderByDescending(c => c.TotalSpent))
            {
                sheet.Cells[row, 1].Value = customer.Id.ToString();
                sheet.Cells[row, 2].Value = customer.FullName;
                sheet.Cells[row, 3].Value = customer.PhoneNumber;
                sheet.Cells[row, 4].Value = customer.Email;
                sheet.Cells[row, 5].Value = customer.LoyaltyPoints;
                sheet.Cells[row, 6].Value = customer.CustomerTier;
                sheet.Cells[row, 7].Value = customer.TotalSpent;
                sheet.Cells[row, 7].Style.Numberformat.Format = "#,##0\" ₫\"";
                sheet.Cells[row, 8].Value = customer.LastOrderDate?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                sheet.Cells[row, 9].Value = customer.IsActive ? "Hoạt động" : "Không hoạt động";

                // Tier color coding
                if (TryGetTierColor(customer.CustomerTier, out System.Drawing.Color color))
                {
                    sheet.Cells[row, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 6].Style.Fill.BackgroundColor.SetColor(color);
                }

                row++;
            }

            sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
            return await package.GetAsByteArrayAsync();
        }

        private static bool TryGetTierColor(string tier, out System.Drawing.Color color)
        {
            switch (tier.ToUpperInvariant())
            {
                case "BRONZE":
                    color = System.Drawing.Color.FromArgb(205, 127, 50);
                    return true;
                case "SILVER":
                    color = System.Drawing.Color.FromArgb(192, 192, 192);
                    return true;
                case "GOLD":
                    color = System.Drawing.Color.FromArgb(255, 215, 0);
                    return true;
                case "PLATINUM":
                    color = System.Drawing.Color.FromArgb(229, 228, 226);
                    return true;
                default:
                    color = System.Drawing.Color.Transparent;
                    return false;
            }
        }
    }
}
