using System.Globalization;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Reports
{
    /// <summary>
    /// Wave 3: Inventory Excel report generator.
    /// Highlights rows with quantity below ingredient MinStockThreshold.
    /// </summary>
    public static class InventoryExcelReport
    {
        public static async Task<byte[]> GenerateAsync(IReadOnlyList<Inventory> inventories, IReadOnlyDictionary<Guid, Ingredient> ingredients)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using ExcelPackage package = new();
            ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Tồn kho");

            string[] headers = ["Mã nguyên liệu", "Tên nguyên liệu", "Đơn vị", "Tồn kho hiện tại", "Tồn kho tối thiểu", "Chênh lệch", "Giá/Đơn vị", "Cập nhật lần cuối"];
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, 1 + i].Value = headers[i];
                sheet.Cells[1, 1 + i].Style.Font.Bold = true;
                sheet.Cells[1, 1 + i].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, 1 + i].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            int row = 2;
            foreach (Inventory inventory in inventories.OrderBy(i => i.IngredientId))
            {
                Ingredient? ingredient = ingredients.GetValueOrDefault(inventory.IngredientId);
                decimal minStock = ingredient?.MinStockThreshold ?? 0;
                decimal variance = inventory.Quantity - minStock;
                bool isLowStock = inventory.Quantity < minStock;

                sheet.Cells[row, 1].Value = inventory.IngredientId.ToString();
                sheet.Cells[row, 2].Value = ingredient?.Name ?? "Không xác định";
                sheet.Cells[row, 3].Value = ingredient?.Unit ?? "N/A";
                sheet.Cells[row, 4].Value = inventory.Quantity;
                sheet.Cells[row, 5].Value = minStock;
                sheet.Cells[row, 6].Value = variance;
                sheet.Cells[row, 7].Value = ingredient?.PricePerUnit ?? 0;
                sheet.Cells[row, 7].Style.Numberformat.Format = "#,##0\" ₫\"";
                sheet.Cells[row, 8].Value = inventory.LastUpdated.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                if (isLowStock)
                {
                    for (int col = 1; col <= 8; col++)
                    {
                        sheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 199, 206));
                    }
                }

                row++;
            }

            sheet.Cells[sheet.Dimension?.Address ?? "A1"].AutoFitColumns();
            return await package.GetAsByteArrayAsync();
        }
    }
}
