using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OfficeOpenXml;
using System.Globalization;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Bug 2B fix: Export VAS Financial Reports to DOCX (Open XML SDK) + XLSX (EPPlus).
    /// Pattern mirrors IHKDBookExportService. 3 statement reports share FinancialStatementLine
    /// structure; TrialBalance uses TrialBalanceAccount (separate methods).
    /// </summary>
    public interface IFinancialReportExportService
    {
        /// <summary>Export a financial statement (BalanceSheet/IncomeStatement/CashFlowStatement) to DOCX.</summary>
        Task<byte[]> ExportStatementToDocxAsync(string title, string period, IReadOnlyList<(string SectionName, IReadOnlyList<FinancialStatementLine> Lines)> sections);

        /// <summary>Export a financial statement to XLSX.</summary>
        Task<byte[]> ExportStatementToXlsxAsync(string title, string period, IReadOnlyList<(string SectionName, IReadOnlyList<FinancialStatementLine> Lines)> sections);

        /// <summary>Export Trial Balance to DOCX.</summary>
        Task<byte[]> ExportTrialBalanceToDocxAsync(string title, string period, IReadOnlyList<TrialBalanceAccount> accounts, decimal totalDebit, decimal totalCredit, bool isBalanced);

        /// <summary>Export Trial Balance to XLSX.</summary>
        Task<byte[]> ExportTrialBalanceToXlsxAsync(string title, string period, IReadOnlyList<TrialBalanceAccount> accounts, decimal totalDebit, decimal totalCredit, bool isBalanced);
    }

    public class FinancialReportExportService(ILogger<FinancialReportExportService> logger) : IFinancialReportExportService
    {
        private readonly ILogger<FinancialReportExportService> _logger = logger;
        private static readonly CultureInfo VnCulture = CultureInfo.GetCultureInfo("vi-VN");

        // ─── FINANCIAL STATEMENT EXPORT (BalanceSheet / IncomeStatement / CashFlowStatement) ───

        public async Task<byte[]> ExportStatementToDocxAsync(string title, string period, IReadOnlyList<(string SectionName, IReadOnlyList<FinancialStatementLine> Lines)> sections)
        {
            using MemoryStream ms = new();
            using WordprocessingDocument doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document);
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // Header
            body.AppendChild(MakeParagraph(title, bold: true, centered: true));
            body.AppendChild(MakeParagraph($"Kỳ báo cáo: {period}", centered: true));
            body.AppendChild(MakeParagraph("Đơn vị tính: VNĐ", centered: true));
            body.AppendChild(MakeParagraph(""));

            // Section tables
            foreach (var (sectionName, lines) in sections)
            {
                body.AppendChild(MakeParagraph(sectionName, bold: true));
                body.AppendChild(BuildStatementTable(lines));
                body.AppendChild(MakeParagraph(""));
            }

            // Footer
            body.AppendChild(MakeParagraph($"Ngày ... tháng ... năm ..."));
            body.AppendChild(MakeParagraph("NGƯỜI LẬP BÁO CÁO", bold: true, centered: true));
            body.AppendChild(MakeParagraph("(Ký, ghi rõ họ tên)", centered: true));

            mainPart.Document.Save();
            byte[] bytes = ms.ToArray();
            _logger.LogInformation("Exported financial statement '{Title}' to DOCX ({Bytes} bytes)", title, bytes.Length);
            return await Task.FromResult(bytes);
        }

        public async Task<byte[]> ExportStatementToXlsxAsync(string title, string period, IReadOnlyList<(string SectionName, IReadOnlyList<FinancialStatementLine> Lines)> sections)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();
            ExcelWorksheet ws = package.Workbook.Worksheets.Add("Report");

            // Header
            ws.Cells["A1"].Value = title;
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A2"].Value = $"Kỳ báo cáo: {period}";
            ws.Cells["A3"].Value = "Đơn vị tính: VNĐ";

            int row = 5;
            foreach (var (sectionName, lines) in sections)
            {
                ws.Cells[$"A{row}"].Value = sectionName;
                ws.Cells[$"A{row}"].Style.Font.Bold = true;
                row++;

                // Column headers
                ws.Cells[$"A{row}"].Value = "Mã chỉ tiêu";
                ws.Cells[$"B{row}"].Value = "Tên chỉ tiêu";
                ws.Cells[$"C{row}"].Value = "Số cuối kỳ";
                ws.Cells[$"D{row}"].Value = "Số đầu năm";
                using (ExcelRange range = ws.Cells[$"A{row}:D{row}"])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                }
                row++;

                // Data rows
                foreach (FinancialStatementLine line in lines)
                {
                    ws.Cells[$"A{row}"].Value = line.ReportItemCode;
                    ws.Cells[$"B{row}"].Value = line.ReportItemName;
                    ws.Cells[$"C{row}"].Value = (double)line.EndingAmount;
                    ws.Cells[$"D{row}"].Value = (double)line.OpeningAmount;
                    ws.Cells[$"C{row}"].Style.Numberformat.Format = "#,##0";
                    ws.Cells[$"D{row}"].Style.Numberformat.Format = "#,##0";
                    if (line.Level == 1)
                    {
                        ws.Cells[$"A{row}:D{row}"].Style.Font.Bold = true;
                    }
                    row++;
                }
                row++; // blank row between sections
            }

            ws.Cells.AutoFitColumns(0, 200);

            byte[] bytes = package.GetAsByteArray();
            _logger.LogInformation("Exported financial statement '{Title}' to XLSX ({Bytes} bytes)", title, bytes.Length);
            return await Task.FromResult(bytes);
        }

        // ─── TRIAL BALANCE EXPORT ───────────────────────────────────────────────────────────

        public async Task<byte[]> ExportTrialBalanceToDocxAsync(string title, string period, IReadOnlyList<TrialBalanceAccount> accounts, decimal totalDebit, decimal totalCredit, bool isBalanced)
        {
            using MemoryStream ms = new();
            using WordprocessingDocument doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document);
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // Header
            body.AppendChild(MakeParagraph(title, bold: true, centered: true));
            body.AppendChild(MakeParagraph($"Kỳ báo cáo: {period}", centered: true));
            body.AppendChild(MakeParagraph("Đơn vị tính: VNĐ", centered: true));
            body.AppendChild(MakeParagraph(""));

            // Table
            Table table = new();
            TableProperties props = new(new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }));
            table.AppendChild(props);

            table.AppendChild(MakeDocxRow(["Số tài khoản", "Tên tài khoản", "Phát sinh Nợ", "Phát sinh Có", "Số dư"], header: true));

            foreach (TrialBalanceAccount acc in accounts)
            {
                table.AppendChild(MakeDocxRow([
                    acc.AccountNumber,
                    acc.AccountName,
                    acc.DebitTotal.ToString("N0", VnCulture),
                    acc.CreditTotal.ToString("N0", VnCulture),
                    acc.Balance.ToString("N0", VnCulture)
                ]));
            }

            // Totals
            table.AppendChild(MakeDocxRow(["", "TỔNG CỘNG", totalDebit.ToString("N0", VnCulture), totalCredit.ToString("N0", VnCulture), ""], bold: true));
            body.AppendChild(table);

            body.AppendChild(MakeParagraph(""));
            body.AppendChild(MakeParagraph(isBalanced ? "✓ Bảng cân đối số phát sinh: Cân bằng" : "✗ Bảng cân đối số phát sinh: Không cân bằng", bold: true));
            body.AppendChild(MakeParagraph(""));
            body.AppendChild(MakeParagraph($"Ngày ... tháng ... năm ..."));
            body.AppendChild(MakeParagraph("NGƯỜI LẬP BÁO CÁO", bold: true, centered: true));
            body.AppendChild(MakeParagraph("(Ký, ghi rõ họ tên)", centered: true));

            mainPart.Document.Save();
            byte[] bytes = ms.ToArray();
            _logger.LogInformation("Exported Trial Balance to DOCX ({Bytes} bytes, {Count} accounts, balanced={IsBalanced})", bytes.Length, accounts.Count, isBalanced);
            return await Task.FromResult(bytes);
        }

        public async Task<byte[]> ExportTrialBalanceToXlsxAsync(string title, string period, IReadOnlyList<TrialBalanceAccount> accounts, decimal totalDebit, decimal totalCredit, bool isBalanced)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();
            ExcelWorksheet ws = package.Workbook.Worksheets.Add("TrialBalance");

            // Header
            ws.Cells["A1"].Value = title;
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A2"].Value = $"Kỳ báo cáo: {period}";
            ws.Cells["A3"].Value = "Đơn vị tính: VNĐ";

            // Column headers (row 5)
            int row = 5;
            ws.Cells[$"A{row}"].Value = "Số tài khoản";
            ws.Cells[$"B{row}"].Value = "Tên tài khoản";
            ws.Cells[$"C{row}"].Value = "Phát sinh Nợ";
            ws.Cells[$"D{row}"].Value = "Phát sinh Có";
            ws.Cells[$"E{row}"].Value = "Số dư";
            using (ExcelRange range = ws.Cells[$"A{row}:E{row}"])
            {
                range.Style.Font.Bold = true;
                range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            }

            // Data rows
            foreach (TrialBalanceAccount acc in accounts)
            {
                row++;
                ws.Cells[$"A{row}"].Value = acc.AccountNumber;
                ws.Cells[$"B{row}"].Value = acc.AccountName;
                ws.Cells[$"C{row}"].Value = (double)acc.DebitTotal;
                ws.Cells[$"D{row}"].Value = (double)acc.CreditTotal;
                ws.Cells[$"E{row}"].Value = (double)acc.Balance;
                ws.Cells[$"C{row}"].Style.Numberformat.Format = "#,##0";
                ws.Cells[$"D{row}"].Style.Numberformat.Format = "#,##0";
                ws.Cells[$"E{row}"].Style.Numberformat.Format = "#,##0";
            }

            // Totals row
            row++;
            ws.Cells[$"B{row}"].Value = "TỔNG CỘNG";
            ws.Cells[$"C{row}"].Value = (double)totalDebit;
            ws.Cells[$"D{row}"].Value = (double)totalCredit;
            ws.Cells[$"C{row}"].Style.Numberformat.Format = "#,##0";
            ws.Cells[$"D{row}"].Style.Numberformat.Format = "#,##0";
            ws.Cells[$"A{row}:E{row}"].Style.Font.Bold = true;

            // Balance status
            row += 2;
            ws.Cells[$"A{row}"].Value = isBalanced ? "✓ Cân bằng" : "✗ Không cân bằng";
            ws.Cells[$"A{row}"].Style.Font.Bold = true;

            ws.Cells.AutoFitColumns(0, 200);

            byte[] bytes = package.GetAsByteArray();
            _logger.LogInformation("Exported Trial Balance to XLSX ({Bytes} bytes, {Count} accounts, balanced={IsBalanced})", bytes.Length, accounts.Count, isBalanced);
            return await Task.FromResult(bytes);
        }

        // ─── HELPERS ────────────────────────────────────────────────────────────────────────

        private static Table BuildStatementTable(IReadOnlyList<FinancialStatementLine> lines)
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

            table.AppendChild(MakeDocxRow(["Mã chỉ tiêu", "Tên chỉ tiêu", "Số cuối kỳ", "Số đầu năm"], header: true));

            foreach (FinancialStatementLine line in lines)
            {
                table.AppendChild(MakeDocxRow([
                    line.ReportItemCode,
                    line.ReportItemName,
                    line.EndingAmount.ToString("N0", VnCulture),
                    line.OpeningAmount.ToString("N0", VnCulture)
                ], bold: line.Level == 1));
            }

            return table;
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
    }
}
