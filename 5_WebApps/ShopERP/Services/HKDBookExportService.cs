using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OfficeOpenXml;
using VanAn.Shared.DTOs;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Wave 8: HKD Book export service — generates DOCX (via Open XML SDK) and XLSX (via EPPlus)
    /// files matching the TT 152/2025/TT-BTC layout (header + bảng + footer + chữ ký).
    /// </summary>
    public interface IHKDBookExportService
    {
        Task<byte[]> ExportToDocxAsync(HKDBookDto book);
        Task<byte[]> ExportToXlsxAsync(HKDBookDto book);
    }

    public class HKDBookExportService(ILogger<HKDBookExportService> logger) : IHKDBookExportService
    {
        private readonly ILogger<HKDBookExportService> _logger = logger;

        // ─── DOCX EXPORT (DocumentFormat.OpenXml) ───────────────────────────────

        public async Task<byte[]> ExportToDocxAsync(HKDBookDto book)
        {
            using MemoryStream ms = new();
            using WordprocessingDocument doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document);
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            AppendDocxHeader(body, book);
            AppendDocxTable(body, book);
            AppendDocxFooter(body, book);

            mainPart.Document.Save();
            byte[] bytes = ms.ToArray();

            _logger.LogInformation("Exported HKD book {TemplateCode} to DOCX ({Bytes} bytes)", book.BookTypeCode, bytes.Length);
            return await Task.FromResult(bytes);
        }

        private static void AppendDocxHeader(Body body, HKDBookDto book)
        {
            body.AppendChild(MakeParagraph($"HỘ, CÁ NHÂN KINH DOANH: {book.TenantId}"));
            body.AppendChild(MakeParagraph("Địa chỉ: ....................................."));
            body.AppendChild(MakeParagraph($"Mã số thuế: ....................................."));
            body.AppendChild(MakeParagraph($"Mẫu số {book.BookTypeCode} (Kèm theo Thông tư số 152/2025/TT-BTC)"));
            body.AppendChild(MakeParagraph("SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ", bold: true, centered: true));
            body.AppendChild(MakeParagraph("Địa điểm kinh doanh: ....................................."));
            body.AppendChild(MakeParagraph($"Kỳ kê khai: {book.Month:D2}/{book.Year}"));
            body.AppendChild(MakeParagraph("Đơn vị tính: VNĐ"));
        }

        private static void AppendDocxTable(Body body, HKDBookDto book)
        {
            Table table = new();
            TableProperties props = new(new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }));
            table.AppendChild(props);

            // Header row: Chứng từ (Số hiệu | Ngày, tháng) | Diễn giải | Số tiền
            table.AppendChild(MakeDocxRow(["Số hiệu", "Ngày, tháng", "Diễn giải", "Số tiền"], header: true));

            // Entry rows
            foreach (HKDBookEntryDto entry in book.Entries)
            {
                table.AppendChild(MakeDocxRow([
                    entry.JournalNo,
                    entry.EntryDate.ToString("dd/MM/yyyy"),
                    entry.Description,
                    SumEntryLines(entry).ToString("N0")
                ]));
            }

            // Totals rows from NumericValues
            foreach (KeyValuePair<string, decimal> kvp in book.NumericValues.Where(IsTotalField))
            {
                table.AppendChild(MakeDocxRow(["", "", DisplayTotalLabel(kvp.Key), kvp.Value.ToString("N0")], bold: true));
            }

            body.AppendChild(table);
        }

        private static void AppendDocxFooter(Body body, HKDBookDto book)
        {
            body.AppendChild(MakeParagraph($"Ngày ... tháng ... năm {book.Year}"));
            body.AppendChild(MakeParagraph("NGƯỜI ĐẠI DIỆN HỘ KINH DOANH / CÁ NHÂN KINH DOANH", bold: true, centered: true));
            body.AppendChild(MakeParagraph("(Ký, ghi rõ họ tên, đóng dấu (nếu có))", centered: true));
        }

        private static Paragraph MakeParagraph(string text, bool bold = false, bool centered = false)
        {
            Paragraph p = new();
            if (centered)
            {
                p.AppendChild(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            }
            Run run = new(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            if (bold)
            {
                run.AppendChild(new RunProperties(new Bold()));
            }
            p.AppendChild(run);
            return p;
        }

        private static TableRow MakeDocxRow(string[] cells, bool header = false, bool bold = false)
        {
            TableRow row = new();
            foreach (string cell in cells)
            {
                TableCell tc = new();
                Paragraph p = new();
                Run run = new(new Text(cell) { Space = SpaceProcessingModeValues.Preserve });
                if (header || bold)
                {
                    run.AppendChild(new RunProperties(new Bold()));
                }
                p.AppendChild(run);
                tc.AppendChild(p);
                row.AppendChild(tc);
            }
            return row;
        }

        // ─── XLSX EXPORT (EPPlus) ───────────────────────────────────────────────

        public async Task<byte[]> ExportToXlsxAsync(HKDBookDto book)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();
            ExcelWorksheet ws = package.Workbook.Worksheets.Add(book.BookTypeCode);

            // Header
            ws.Cells["A1"].Value = $"HỘ, CÁ NHÂN KINH DOANH: {book.TenantId}";
            ws.Cells["A2"].Value = "Địa chỉ: .....................................";
            ws.Cells["A3"].Value = "Mã số thuế: .....................................";
            ws.Cells["A4"].Value = $"Mẫu số {book.BookTypeCode} (Kèm theo Thông tư số 152/2025/TT-BTC)";
            ws.Cells["A5"].Value = "SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ";
            ws.Cells["A5"].Style.Font.Bold = true;
            ws.Cells["A6"].Value = "Địa điểm kinh doanh: .....................................";
            ws.Cells["A7"].Value = $"Kỳ kê khai: {book.Month:D2}/{book.Year}";
            ws.Cells["A8"].Value = "Đơn vị tính: VNĐ";

            // Table header (row 10)
            int row = 10;
            ws.Cells[$"A{row}"].Value = "Số hiệu";
            ws.Cells[$"B{row}"].Value = "Ngày, tháng";
            ws.Cells[$"C{row}"].Value = "Diễn giải";
            ws.Cells[$"D{row}"].Value = "Số tiền";
            using (ExcelRange range = ws.Cells[$"A{row}:D{row}"])
            {
                range.Style.Font.Bold = true;
                range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            }

            // Entry rows
            foreach (HKDBookEntryDto entry in book.Entries)
            {
                row++;
                ws.Cells[$"A{row}"].Value = entry.JournalNo;
                ws.Cells[$"B{row}"].Value = entry.EntryDate.ToString("dd/MM/yyyy");
                ws.Cells[$"C{row}"].Value = entry.Description;
                ws.Cells[$"D{row}"].Value = (double)SumEntryLines(entry);
                ws.Cells[$"D{row}"].Style.Numberformat.Format = "#,##0";
            }

            // Totals
            foreach (KeyValuePair<string, decimal> kvp in book.NumericValues.Where(IsTotalField))
            {
                row++;
                ws.Cells[$"C{row}"].Value = DisplayTotalLabel(kvp.Key);
                ws.Cells[$"D{row}"].Value = (double)kvp.Value;
                ws.Cells[$"C{row}"].Style.Font.Bold = true;
                ws.Cells[$"D{row}"].Style.Font.Bold = true;
                ws.Cells[$"D{row}"].Style.Numberformat.Format = "#,##0";
            }

            // Footer
            row += 2;
            ws.Cells[$"A{row}"].Value = $"Ngày ... tháng ... năm {book.Year}";
            row++;
            ws.Cells[$"A{row}"].Value = "NGƯỜI ĐẠI DIỆN HỘ KINH DOANH / CÁ NHÂN KINH DOANH";
            ws.Cells[$"A{row}"].Style.Font.Bold = true;
            row++;
            ws.Cells[$"A{row}"].Value = "(Ký, ghi rõ họ tên, đóng dấu (nếu có))";

            ws.Cells[ws.Dimension.Address].AutoFitColumns();

            byte[] bytes = package.GetAsByteArray();
            _logger.LogInformation("Exported HKD book {TemplateCode} to XLSX ({Bytes} bytes)", book.BookTypeCode, bytes.Length);
            return await Task.FromResult(bytes);
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        private static decimal SumEntryLines(HKDBookEntryDto entry) =>
            entry.Lines.Sum(l => l.DebitAmount > 0 ? l.DebitAmount : l.CreditAmount);

        private static bool IsTotalField(KeyValuePair<string, decimal> kvp) =>
            kvp.Key.StartsWith("Total", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("NetRevenue", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("NetProfit", StringComparison.OrdinalIgnoreCase);

        private static string DisplayTotalLabel(string key) => key switch
        {
            "TotalRevenue" => "Tổng doanh thu",
            "TotalExpense" => "Tổng chi phí",
            "TotalVat" => "Tổng số thuế GTGT phải nộp",
            "TotalPIT" => "Tổng số thuế TNCN phải nộp",
            "NetRevenue" => "Doanh thu thuần",
            "NetProfit" => "Lợi nhuận",
            _ => key
        };
    }
}
