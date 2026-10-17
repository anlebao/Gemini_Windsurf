using System.Globalization;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using VanAn.CoreHub.Services.GeneralLedger;
using VanAn.Shared.DTOs;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// TUÂN THỦ PHÁP LÝ P0.2-Đ1 (2026-10-16, bổ sung): Xuất Excel Sổ Cái + Sổ Nhật ký chung
    /// theo MẪU TT 99/2025/TT-BTC — EPPlus (precedent VaIIeExportService).
    /// - Sổ Cái      → Mẫu S02c1-DN (docs/Accounting_Doc/MauSoCaiKeToan.docx — hình thức Chứng từ ghi sổ)
    /// - Sổ Nhật ký  → Mẫu S03b-DN (docs/Accounting_Doc/SoNhatKy_HT_NhatKyChung.docx — hình thức Nhật ký chung)
    /// Cấu trúc bảng (cột A→G + Nợ/Có) giữ NGUYÊN như mẫu; dữ liệu từ IGeneralLedgerService.
    /// </summary>
    public static class LedgerExcelExportService
    {
        private const string Form99Note = "(Kèm theo Thông tư số 99/2025/TT-BTC ngày 27 tháng 10 năm 2025 của Bộ trưởng Bộ Tài chính)";
        private const string AmountFormat = "#,##0";

        /// <summary>Xuất Sổ Cái (Mẫu S02c1-DN) — dư đầu kỳ + phát sinh + dư cuối kỳ + lũy kế quý.</summary>
        public static byte[] ExportSoCaiExcel(
            LedgerReportDto report,
            int year,
            int month,
            TenantInfoDto? tenant,
            QuarterTotalsDto quarter)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();
            ExcelWorksheet ws = package.Workbook.Worksheets.Add("Sổ Cái");

            // ── Phần đầu (như mẫu) ──
            ws.Cells["A1"].Value = $"Đơn vị: {tenant?.Name ?? "............................"}";
            ws.Cells["A2"].Value = $"Địa chỉ: {tenant?.Address ?? "............................"}";
            ws.Cells["H1"].Value = "Mẫu số S02c1-DN";
            ws.Cells["H2"].Value = Form99Note;
            ws.Cells["H1:H2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells["H1"].Style.Font.Bold = true;

            ws.Cells["A4"].Value = "SỔ CÁI";
            ws.Cells["A4:H4"].Merge = true;
            Center(ws.Cells["A4:H4"], bold: true, size: 16);
            ws.Cells["A5"].Value = "(Dùng cho hình thức kế toán Chứng từ ghi sổ)";
            ws.Cells["A5:H5"].Merge = true;
            Center(ws.Cells["A5:H5"]);

            ws.Cells["A6"].Value = $"Năm: {year}";
            ws.Cells["G6"].Value = $"Tên tài khoản: {report.AccountName ?? ""}";
            ws.Cells["G6"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells["A7"].Value = $"Số hiệu: {report.AccountCode}";
            ws.Cells["G7"].Value = $"Tháng: {month}";
            ws.Cells["G7"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            // ── Header bảng 2 dòng (A | B/C | D | E | 1/2 | G) ──
            int headerRow = 9;
            string[] top = ["Ngày, tháng ghi sổ", "Chứng từ ghi sổ", "Diễn giải", "Số hiệu TK đối ứng", "Số tiền", "Ghi chú"];
            string[] sub = ["", "Số hiệu", "Ngày, tháng", "", "", "Nợ", "Có", ""];
            int[] widths = [14, 12, 12, 32, 14, 15, 15, 10];
            for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];

            // Dòng 1 header: cột A(1) · B:C gộp "Chứng từ ghi sổ" · D(4) · E(5) · F:G gộp "Số tiền" · H(8)
            ws.Cells[headerRow, 1].Value = top[0];
            ws.Cells[headerRow, 2, headerRow, 3].Merge = true;
            ws.Cells[headerRow, 2].Value = top[1];
            ws.Cells[headerRow, 4].Value = top[2];
            ws.Cells[headerRow, 5].Value = top[3];
            ws.Cells[headerRow, 6, headerRow, 7].Merge = true;
            ws.Cells[headerRow, 6].Value = top[4];
            ws.Cells[headerRow, 8].Value = top[5];
            // Dòng 2: A(1) · B "Số hiệu" · C "Ngày, tháng" · D(4) · E(5) · F "Nợ" · G "Có" · H(8)
            ws.Cells[headerRow + 1, 2].Value = sub[1];
            ws.Cells[headerRow + 1, 3].Value = sub[2];
            ws.Cells[headerRow + 1, 6].Value = sub[5];
            ws.Cells[headerRow + 1, 7].Value = sub[6];
            // Hàng ký hiệu cột
            ws.Cells[headerRow + 2, 1].Value = "A";
            ws.Cells[headerRow + 2, 2].Value = "B";
            ws.Cells[headerRow + 2, 3].Value = "C";
            ws.Cells[headerRow + 2, 4].Value = "D";
            ws.Cells[headerRow + 2, 5].Value = "E";
            ws.Cells[headerRow + 2, 6].Value = "1";
            ws.Cells[headerRow + 2, 7].Value = "2";
            ws.Cells[headerRow + 2, 8].Value = "G";

            ExcelRange headerRange = ws.Cells[headerRow, 1, headerRow + 2, 8];
            Center(headerRange, bold: true);
            headerRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

            // ── Dữ liệu ──
            int row = headerRow + 3;
            // Số dư đầu kỳ
            WriteBalanceRow(ws, row++, "- Số dư đầu kỳ", report.OpeningBalance);

            foreach (LedgerRowDto r in report.Rows)
            {
                ws.Cells[row, 1].Value = r.EntryDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                ws.Cells[row, 2].Value = r.JournalNo;
                ws.Cells[row, 3].Value = r.EntryDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                ws.Cells[row, 4].Value = r.Description;
                ws.Cells[row, 5].Value = r.CounterpartAccount;
                if (r.Debit > 0) ws.Cells[row, 6].Value = r.Debit;
                if (r.Credit > 0) ws.Cells[row, 7].Value = r.Credit;
                row++;
            }

            // Cộng phát sinh tháng
            WriteSummaryRow(ws, row++, "- Cộng số phát sinh tháng", report.PeriodDebitTotal, report.PeriodCreditTotal, bold: true);
            // Số dư cuối kỳ
            WriteBalanceRow(ws, row++, "- Số dư cuối kỳ", report.ClosingBalance, bold: true);
            // Lũy kế từ đầu quý
            WriteSummaryRow(ws, row++, "- Cộng lũy kế từ đầu quý", quarter.DebitTotal, quarter.CreditTotal);

            ExcelRange dataRange = ws.Cells[headerRow, 1, row - 1, 8];
            dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

            // ── Phần cuối (chữ ký — như mẫu) ──
            row += 2;
            ws.Cells[row, 1].Value = "Người ghi sổ";
            ws.Cells[row, 4].Value = "Kế toán trưởng";
            ws.Cells[row, 7].Value = $"Ngày ..... tháng ..... năm {year}";
            ws.Cells[row + 1, 7].Value = "Người đại diện theo pháp luật";
            ws.Cells[row + 1, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            Center(ws.Cells[row, 1, row, 4], bold: true);
            Center(ws.Cells[row, 7]);

            // Số đầu kỳ/cuối kỳ đưa vào cột Nợ/Có theo dấu (dương → Nợ, âm → Có) — đúng nghiệp vụ sổ cái.
            return package.GetAsByteArray();
        }

        /// <summary>Xuất Sổ Nhật ký chung (Mẫu S03b-DN — hình thức Nhật ký chung, có cột Trang sổ/STT dòng).</summary>
        public static byte[] ExportSoNhatKyExcel(
            IReadOnlyList<JournalEntryDto> entries,
            int year,
            int month,
            TenantInfoDto? tenant)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();
            ExcelWorksheet ws = package.Workbook.Worksheets.Add("Sổ Nhật ký chung");

            // ── Phần đầu (như mẫu) ──
            ws.Cells["A1"].Value = $"Đơn vị: {tenant?.Name ?? "............................"}";
            ws.Cells["A2"].Value = $"Địa chỉ: {tenant?.Address ?? "............................"}";
            ws.Cells["H1"].Value = "Mẫu số S03b-DN";
            ws.Cells["H2"].Value = Form99Note;
            ws.Cells["H1:H2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells["H1"].Style.Font.Bold = true;

            ws.Cells["A4"].Value = "SỔ NHẬT KÝ CHUNG";
            ws.Cells["A4:I4"].Merge = true;
            Center(ws.Cells["A4:I4"], bold: true, size: 16);
            ws.Cells["A5"].Value = "(Dùng cho hình thức kế toán Nhật ký chung)";
            ws.Cells["A5:I5"].Merge = true;
            Center(ws.Cells["A5:I5"]);

            ws.Cells["A6"].Value = $"Năm: {year}";
            ws.Cells["G6"].Value = $"Tháng: {month}";
            ws.Cells["G6"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            // ── Header bảng (10 cột vật lý: A | B/C | D | E/(F trống)/G | H | 1/2 — đúng mẫu A B C D E G H 1 2) ──
            int headerRow = 8;
            int[] widths = [14, 12, 12, 34, 12, 4, 12, 14, 15, 15];
            for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];

            // Dòng 1: A "Ngày, tháng ghi sổ" · B:C "Chứng từ" · D "Diễn giải" · E:G "Nhật ký chung" · H "Số hiệu TK đối ứng" · I:J "Số tiền"
            ws.Cells[headerRow, 1].Value = "Ngày, tháng ghi sổ";
            ws.Cells[headerRow, 2, headerRow, 3].Merge = true;
            ws.Cells[headerRow, 2].Value = "Chứng từ";
            ws.Cells[headerRow, 4].Value = "Diễn giải";
            ws.Cells[headerRow, 5, headerRow, 7].Merge = true;
            ws.Cells[headerRow, 5].Value = "Nhật ký chung";
            ws.Cells[headerRow, 8].Value = "Số hiệu TK đối ứng";
            ws.Cells[headerRow, 9, headerRow, 10].Merge = true;
            ws.Cells[headerRow, 9].Value = "Số tiền";
            // Dòng 2: B "Số hiệu" · C "Ngày tháng" · E "Trang sổ" · G "STT dòng" · I "Nợ" · J "Có"
            ws.Cells[headerRow + 1, 2].Value = "Số hiệu";
            ws.Cells[headerRow + 1, 3].Value = "Ngày tháng";
            ws.Cells[headerRow + 1, 5].Value = "Trang sổ";
            ws.Cells[headerRow + 1, 7].Value = "STT dòng";
            ws.Cells[headerRow + 1, 9].Value = "Nợ";
            ws.Cells[headerRow + 1, 10].Value = "Có";
            // Hàng ký hiệu cột — đúng mẫu: A(1) B(2) C(3) D(4) E(5) [F(6) trống] G(7) H(8) 1(9) 2(10)
            ws.Cells[headerRow + 2, 1].Value = "A";
            ws.Cells[headerRow + 2, 2].Value = "B";
            ws.Cells[headerRow + 2, 3].Value = "C";
            ws.Cells[headerRow + 2, 4].Value = "D";
            ws.Cells[headerRow + 2, 5].Value = "E";
            ws.Cells[headerRow + 2, 7].Value = "G";
            ws.Cells[headerRow + 2, 8].Value = "H";
            ws.Cells[headerRow + 2, 9].Value = "1";
            ws.Cells[headerRow + 2, 10].Value = "2";

            ExcelRange headerRange = ws.Cells[headerRow, 1, headerRow + 2, 10];
            Center(headerRange, bold: true);
            headerRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

            // ── Dữ liệu ──
            int row = headerRow + 3;
            int lineNo = 0;
            decimal totalDebit = 0, totalCredit = 0;
            foreach (JournalEntryDto entry in entries)
            {
                string counterpartDebit = string.Join(", ", entry.Lines.Where(l => l.CreditAmount > 0).Select(l => l.AccountNumber).Distinct());
                string counterpartCredit = string.Join(", ", entry.Lines.Where(l => l.DebitAmount > 0).Select(l => l.AccountNumber).Distinct());
                foreach (JournalEntryLineDto line in entry.Lines)
                {
                    lineNo++;
                    ws.Cells[row, 1].Value = entry.EntryDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                    ws.Cells[row, 2].Value = entry.JournalNo;
                    ws.Cells[row, 3].Value = entry.EntryDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                    ws.Cells[row, 4].Value = line.Description ?? entry.Description;
                    ws.Cells[row, 7].Value = lineNo; // STT dòng
                    if (line.DebitAmount > 0)
                    {
                        ws.Cells[row, 8].Value = counterpartDebit;
                        ws.Cells[row, 9].Value = line.DebitAmount;
                        totalDebit += line.DebitAmount;
                    }
                    if (line.CreditAmount > 0)
                    {
                        ws.Cells[row, 8].Value = counterpartCredit;
                        ws.Cells[row, 10].Value = line.CreditAmount;
                        totalCredit += line.CreditAmount;
                    }
                    row++;
                }
            }

            // Cộng số phát sinh tháng
            ws.Cells[row, 4].Value = "- Cộng số phát sinh tháng";
            ws.Cells[row, 9].Value = totalDebit;
            ws.Cells[row, 10].Value = totalCredit;
            StyleSummary(ws, row);
            row++;

            ExcelRange dataRange = ws.Cells[headerRow, 1, row - 1, 10];
            dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

            // ── Phần cuối (chữ ký — như mẫu) ──
            row += 2;
            ws.Cells[row, 1].Value = "Người ghi sổ";
            ws.Cells[row, 4].Value = "Kế toán trưởng";
            ws.Cells[row, 8].Value = $"Ngày ..... tháng ..... năm {year}";
            ws.Cells[row + 1, 8].Value = "Người đại diện theo pháp luật";
            Center(ws.Cells[row, 1, row, 4], bold: true);
            Center(ws.Cells[row, 8]);

            return package.GetAsByteArray();
        }

        /// <summary>
        /// P0.2-Đ2 (W2, 2026-10-17): Xuất Sổ chi tiết các TK (Mẫu S38-DN — SoChiTiet_Mau-S38-DN.docx)
        /// — 9 cột: Ngày ghi sổ | Chứng từ (Số hiệu, Ngày tháng) | Diễn giải | TK đối ứng | PS Nợ/Có | Dư Nợ/Có.
        /// Đối tượng ghi ở phần đầu (theo mẫu); khi xem tất cả đối tượng thì gắn "— ĐT: X" vào diễn giải.
        /// </summary>
        public static byte[] ExportSoChiTietExcel(
            SubLedgerReportDto report,
            int year,
            int month,
            TenantInfoDto? tenant)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();
            ExcelWorksheet ws = package.Workbook.Worksheets.Add("Sổ chi tiết");

            // ── Phần đầu (như mẫu S38-DN) ──
            ws.Cells["A1"].Value = $"Đơn vị: {tenant?.Name ?? "............................"}";
            ws.Cells["A2"].Value = $"Địa chỉ: {tenant?.Address ?? "............................"}";
            ws.Cells["H1"].Value = "Mẫu số S38-DN";
            ws.Cells["H2"].Value = Form99Note;
            ws.Cells["H1:H2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            ws.Cells["H1"].Style.Font.Bold = true;

            ws.Cells["A4"].Value = "SỔ CHI TIẾT CÁC TÀI KHOẢN";
            ws.Cells["A4:I4"].Merge = true;
            Center(ws.Cells["A4:I4"], bold: true, size: 16);

            ws.Cells["A6"].Value = $"Tài khoản: {report.AccountCode}{(report.AccountName is null ? "" : $" - {report.AccountName}")}";
            ws.Cells["F6"].Value = $"Đối tượng: {report.DoiTuong ?? "Tất cả"}";
            ws.Cells["G6"].Value = "Loại tiền: VNĐ";
            ws.Cells["H6"].Value = $"Năm: {year} — Tháng: {month}";
            ws.Cells["H6"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            // ── Header bảng 3 dòng (mẫu S38-DN): A | B/C | D | E | F:G (PS) | H:I (Dư) ──
            int headerRow = 8;
            int[] widths = [14, 12, 12, 34, 12, 15, 15, 15, 15];
            for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];

            // Dòng 1: A "Ngày, tháng ghi sổ" · B:C "Chứng từ" · D "Diễn giải" · E "TK đối ứng" · F:G "Số phát sinh" · H:I "Số dư"
            ws.Cells[headerRow, 1].Value = "Ngày, tháng ghi sổ";
            ws.Cells[headerRow, 2, headerRow, 3].Merge = true;
            ws.Cells[headerRow, 2].Value = "Chứng từ";
            ws.Cells[headerRow, 4].Value = "Diễn giải";
            ws.Cells[headerRow, 5].Value = "TK đối ứng";
            ws.Cells[headerRow, 6, headerRow, 7].Merge = true;
            ws.Cells[headerRow, 6].Value = "Số phát sinh";
            ws.Cells[headerRow, 8, headerRow, 9].Merge = true;
            ws.Cells[headerRow, 8].Value = "Số dư";
            // Dòng 2: B "Số hiệu" · C "Ngày tháng" · F "Nợ" · G "Có" · H "Nợ" · I "Có"
            ws.Cells[headerRow + 1, 2].Value = "Số hiệu";
            ws.Cells[headerRow + 1, 3].Value = "Ngày tháng";
            ws.Cells[headerRow + 1, 6].Value = "Nợ";
            ws.Cells[headerRow + 1, 7].Value = "Có";
            ws.Cells[headerRow + 1, 8].Value = "Nợ";
            ws.Cells[headerRow + 1, 9].Value = "Có";
            // Hàng ký hiệu cột — đúng mẫu: A B C D E 1 2 3 4
            ws.Cells[headerRow + 2, 1].Value = "A";
            ws.Cells[headerRow + 2, 2].Value = "B";
            ws.Cells[headerRow + 2, 3].Value = "C";
            ws.Cells[headerRow + 2, 4].Value = "D";
            ws.Cells[headerRow + 2, 5].Value = "E";
            ws.Cells[headerRow + 2, 6].Value = "1";
            ws.Cells[headerRow + 2, 7].Value = "2";
            ws.Cells[headerRow + 2, 8].Value = "3";
            ws.Cells[headerRow + 2, 9].Value = "4";

            ExcelRange headerRange = ws.Cells[headerRow, 1, headerRow + 2, 9];
            Center(headerRange, bold: true);
            headerRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

            bool showPartyInDesc = report.DoiTuong is null; // xem tất cả đối tượng → gắn ĐT vào diễn giải

            // ── Dữ liệu ──
            int row = headerRow + 3;
            WriteBalanceRow38(ws, row++, "- Số dư đầu kỳ", report.OpeningBalance);

            foreach (SubLedgerRowDto r in report.Rows)
            {
                ws.Cells[row, 1].Value = r.EntryDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                ws.Cells[row, 2].Value = r.JournalNo;
                ws.Cells[row, 3].Value = r.EntryDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                ws.Cells[row, 4].Value = showPartyInDesc && r.DoiTuong != null
                    ? $"{r.Description} — ĐT: {r.DoiTuong}"
                    : r.Description;
                ws.Cells[row, 5].Value = r.CounterpartAccount;
                if (r.Debit > 0) ws.Cells[row, 6].Value = r.Debit;
                if (r.Credit > 0) ws.Cells[row, 7].Value = r.Credit;
                WriteRunningBalance(ws, row, r.RunningBalance);
                row++;
            }

            // Cộng số phát sinh
            ws.Cells[row, 4].Value = "- Cộng số phát sinh";
            if (report.PeriodDebitTotal != 0) ws.Cells[row, 6].Value = report.PeriodDebitTotal;
            if (report.PeriodCreditTotal != 0) ws.Cells[row, 7].Value = report.PeriodCreditTotal;
            ws.Cells[row, 5].Value = "x";
            ws.Cells[row, 8].Value = "x";
            ws.Cells[row, 9].Value = "x";
            StyleSummary38(ws, row);
            row++;
            // Số dư cuối kỳ
            WriteBalanceRow38(ws, row++, "- Số dư cuối kỳ", report.ClosingBalance, bold: true);

            ExcelRange dataRange = ws.Cells[headerRow, 1, row - 1, 9];
            dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

            // ── Phần cuối (như mẫu: số trang + ngày mở sổ + chữ ký) ──
            row += 2;
            ws.Cells[row, 1].Value = "- Sổ này có .... trang, đánh số từ trang 01 đến trang ....\n- Ngày mở sổ: ...";
            ws.Cells[row, 1].Style.WrapText = true;
            ws.Cells[row, 4].Value = "Người ghi sổ";
            ws.Cells[row, 6].Value = "Kế toán trưởng";
            ws.Cells[row, 8].Value = $"Ngày ..... tháng ..... năm {year}";
            ws.Cells[row + 1, 4].Value = "(Ký, họ tên)";
            ws.Cells[row + 1, 6].Value = "(Ký, họ tên)";
            ws.Cells[row + 1, 8].Value = "Người đại diện theo pháp luật";
            ws.Cells[row + 2, 8].Value = "(Ký, họ tên, đóng dấu)";
            Center(ws.Cells[row, 4, row + 1, 6], bold: true);
            Center(ws.Cells[row, 8, row + 1, 8]);

            return package.GetAsByteArray();
        }

        /// <summary>
        /// 2026-10-17 (user directive): Báo cáo công nợ tổng hợp — 1 workbook 2 sheets theo mẫu
        /// docs/Accounting_Doc/KeToan_CongNo_BaoCao:
        ///   "Phải thu KH (131)"  ← mẫu "4. Tổng hợp công nợ phải thu khách hàng.xlsx"
        ///   "Phải trả NCC (331)" ← mẫu "1. Tổng hợp công nợ phải trả nhà cung cấp.xlsx"
        /// Quy ước dấu CongNoService: dương = còn nợ. 131 dư Nợ (dương→Nợ, âm→Có);
        /// 331 dư Có (dương→Có, âm→Nợ). Phát sinh = tăng nợ, Đã thu/trả = giảm nợ.
        /// Lũy kế phát sinh (331) = |dư đầu kỳ cùng chiều| + phát sinh trong kỳ.
        /// Các cột mẫu chưa có nguồn dữ liệu (Mã, Địa chỉ, MST, nhóm, nợ tối đa, ghi chú) để trống.
        /// </summary>
        public static byte[] ExportCongNoTongHopExcel(
            CongNoReportDto report,
            TenantInfoDto? tenant)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using ExcelPackage package = new();

            CongNoGroupDto? receivable = report.Groups.FirstOrDefault(g => g.AccountCode == "131");
            CongNoGroupDto? payable = report.Groups.FirstOrDefault(g => g.AccountCode == "331");

            WriteReceivableSheet(package, receivable, report, tenant);
            WritePayableSheet(package, payable, report, tenant);

            return package.GetAsByteArray();
        }

        /// <summary>Sheet phải thu KH — mẫu 4: A Mã KH | B Tên KH | C TK CN | D:E Đầu kỳ | F:G PS | H:I Cuối kỳ | J Ghi chú.</summary>
        private static void WriteReceivableSheet(ExcelPackage package, CongNoGroupDto? group, CongNoReportDto report, TenantInfoDto? tenant)
        {
            ExcelWorksheet ws = package.Workbook.Worksheets.Add("Phải thu KH (131)");

            ws.Cells["A1"].Value = "TỔNG HỢP CÔNG NỢ PHẢI THU KHÁCH HÀNG";
            ws.Cells["A1:J1"].Merge = true;
            Center(ws.Cells["A1:J1"], bold: true, size: 14);
            ws.Cells["A2"].Value = $"Chi nhánh: {tenant?.Name ?? "............................"}";
            ws.Cells["A3"].Value = $"Kỳ báo cáo: Tháng {report.Month}/{report.Year}";

            int[] widths = [12, 30, 10, 15, 15, 15, 15, 15, 15, 14];
            for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];

            int headerRow = 5;
            ws.Cells[headerRow, 1].Value = "Mã khách hàng";
            ws.Cells[headerRow, 2].Value = "Tên khách hàng";
            ws.Cells[headerRow, 3].Value = "TK công nợ";
            MergeGroupHeader(ws, headerRow, 4, "Số dư đầu kỳ");
            MergeGroupHeader(ws, headerRow, 6, "Phát sinh");
            MergeGroupHeader(ws, headerRow, 8, "Số dư cuối kỳ");
            ws.Cells[headerRow, 10].Value = "Ghi chú";
            ws.Cells[headerRow, 1, headerRow + 1, 3].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Cells[headerRow + 1, 4].Value = "Nợ"; ws.Cells[headerRow + 1, 5].Value = "Có";
            ws.Cells[headerRow + 1, 6].Value = "Nợ"; ws.Cells[headerRow + 1, 7].Value = "Có";
            ws.Cells[headerRow + 1, 8].Value = "Nợ"; ws.Cells[headerRow + 1, 9].Value = "Có";

            ExcelRange headerRange = ws.Cells[headerRow, 1, headerRow + 1, 10];
            Center(headerRange, bold: true);
            headerRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

            int row = headerRow + 2;
            if (group != null)
            {
                foreach (CongNoRowDto r in group.Rows)
                {
                    ws.Cells[row, 2].Value = r.DoiTuong;
                    ws.Cells[row, 3].Value = "131";
                    // 131 dư Nợ: dương → Nợ, âm → Có; PS tăng → Nợ; Đã thu → Có
                    WriteSignedPair(ws, row, 4, r.DauKy);
                    if (r.PhatSinh != 0) ws.Cells[row, 6].Value = r.PhatSinh;
                    if (r.DaThuTra != 0) ws.Cells[row, 7].Value = r.DaThuTra;
                    WriteSignedPair(ws, row, 8, r.CuoiKy);
                    row++;
                }

                ws.Cells[row, 1].Value = "Tổng cộng";
                WriteSignedPair(ws, row, 4, group.TongDauKy);
                if (group.TongPhatSinh != 0) ws.Cells[row, 6].Value = group.TongPhatSinh;
                if (group.TongDaThuTra != 0) ws.Cells[row, 7].Value = group.TongDaThuTra;
                WriteSignedPair(ws, row, 8, group.TongCuoiKy);
                ws.Cells[row, 1, row, 10].Style.Font.Bold = true;
                ws.Cells[row, 1, row, 10].Style.Border.Top.Style = ExcelBorderStyle.Double;
            }

            ApplyAmountFormat(ws, headerRow + 2, row, 4, 9);
        }

        /// <summary>Sheet phải trả NCC — mẫu 1: A Mã NCC | B Tên | C Địa chỉ | D MST | E:F Đầu kỳ | G:H PS | I:J Lũy kế | K:L Cuối kỳ | M:N nhóm | O nợ tối đa.</summary>
        private static void WritePayableSheet(ExcelPackage package, CongNoGroupDto? group, CongNoReportDto report, TenantInfoDto? tenant)
        {
            ExcelWorksheet ws = package.Workbook.Worksheets.Add("Phải trả NCC (331)");

            ws.Cells["A1"].Value = "TỔNG HỢP CÔNG NỢ PHẢI TRẢ NHÀ CUNG CẤP";
            ws.Cells["A1:O1"].Merge = true;
            Center(ws.Cells["A1:O1"], bold: true, size: 14);
            ws.Cells["A2"].Value = $"Chi nhánh: {tenant?.Name ?? "............................"}";
            ws.Cells["A3"].Value = $"Kỳ báo cáo: Tháng {report.Month}/{report.Year}";

            int[] widths = [12, 30, 18, 14, 15, 15, 15, 15, 15, 15, 15, 15, 12, 18, 12];
            for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];

            int headerRow = 5;
            ws.Cells[headerRow, 1].Value = "Mã nhà cung cấp";
            ws.Cells[headerRow, 2].Value = "Tên nhà cung cấp";
            ws.Cells[headerRow, 3].Value = "Địa chỉ";
            ws.Cells[headerRow, 4].Value = "Mã số thuế";
            MergeGroupHeader(ws, headerRow, 5, "Số dư đầu kỳ");
            MergeGroupHeader(ws, headerRow, 7, "Phát sinh");
            MergeGroupHeader(ws, headerRow, 9, "Lũy kế phát sinh");
            MergeGroupHeader(ws, headerRow, 11, "Số dư cuối kỳ");
            ws.Cells[headerRow, 13].Value = "Mã nhóm nhà cung cấp";
            ws.Cells[headerRow, 14].Value = "Tên nhóm nhà cung cấp";
            ws.Cells[headerRow, 15].Value = "Số nợ tối đa";
            ws.Cells[headerRow, 1, headerRow + 1, 4].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            ws.Cells[headerRow, 13, headerRow + 1, 15].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            for (int c = 5; c <= 12; c += 2)
            {
                ws.Cells[headerRow + 1, c].Value = "Nợ";
                ws.Cells[headerRow + 1, c + 1].Value = "Có";
            }

            ExcelRange headerRange = ws.Cells[headerRow, 1, headerRow + 1, 15];
            Center(headerRange, bold: true);
            headerRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

            int row = headerRow + 2;
            if (group != null)
            {
                // Dòng "TK Công nợ: 331" + tổng của khối — đúng mẫu
                ws.Cells[row, 1].Value = "TK Công nợ: 331";
                WritePayablePair(ws, row, 5, group.TongDauKy);
                WritePayablePair(ws, row, 7, group.TongPhatSinh, group.TongDaThuTra);
                WriteLuyKePair(ws, row, group.TongDauKy, group.TongPhatSinh, group.TongDaThuTra);
                WritePayablePair(ws, row, 11, group.TongCuoiKy);
                ws.Cells[row, 1].Style.Font.Bold = true;
                row++;

                foreach (CongNoRowDto r in group.Rows)
                {
                    ws.Cells[row, 2].Value = r.DoiTuong;
                    // 331 dư Có: dương → Có, âm → Nợ; PS tăng (mua chịu) → Có; Đã trả → Nợ
                    WritePayablePair(ws, row, 5, r.DauKy);
                    WritePayablePair(ws, row, 7, r.PhatSinh, r.DaThuTra);
                    WriteLuyKePair(ws, row, r.DauKy, r.PhatSinh, r.DaThuTra);
                    WritePayablePair(ws, row, 11, r.CuoiKy);
                    row++;
                }

                ws.Cells[row, 1].Value = "Tổng cộng";
                WritePayablePair(ws, row, 5, group.TongDauKy);
                WritePayablePair(ws, row, 7, group.TongPhatSinh, group.TongDaThuTra);
                WriteLuyKePair(ws, row, group.TongDauKy, group.TongPhatSinh, group.TongDaThuTra);
                WritePayablePair(ws, row, 11, group.TongCuoiKy);
                ws.Cells[row, 1, row, 15].Style.Font.Bold = true;
                ws.Cells[row, 1, row, 15].Style.Border.Top.Style = ExcelBorderStyle.Double;
            }

            ApplyAmountFormat(ws, headerRow + 2, row, 5, 12);
        }

        /// <summary>TK dư Nợ (131): dương → cột Nợ (col), âm → cột Có (col+1).</summary>
        private static void WriteSignedPair(ExcelWorksheet ws, int row, int col, decimal value)
        {
            if (value > 0) ws.Cells[row, col].Value = value;
            else if (value < 0) ws.Cells[row, col + 1].Value = -value;
        }

        /// <summary>TK dư Có (331): dương → cột Có (col+1), âm → cột Nợ (col).</summary>
        private static void WritePayablePair(ExcelWorksheet ws, int row, int col, decimal balance)
        {
            if (balance > 0) ws.Cells[row, col + 1].Value = balance;
            else if (balance < 0) ws.Cells[row, col].Value = -balance;
        }

        /// <summary>331 phát sinh: tăng nợ (mua chịu) → Có; giảm nợ (đã trả) → Nợ.</summary>
        private static void WritePayablePair(ExcelWorksheet ws, int row, int col, decimal phatSinh, decimal daThuTra)
        {
            if (daThuTra != 0) ws.Cells[row, col].Value = daThuTra;
            if (phatSinh != 0) ws.Cells[row, col + 1].Value = phatSinh;
        }

        /// <summary>Lũy kế 331: Nợ = |dư đầu kỳ âm| + đã trả; Có = dư đầu kỳ dương + phát sinh.</summary>
        private static void WriteLuyKePair(ExcelWorksheet ws, int row, decimal dauKy, decimal phatSinh, decimal daThuTra)
        {
            decimal luyKeNo = (dauKy < 0 ? -dauKy : 0) + daThuTra;
            decimal luyKeCo = (dauKy > 0 ? dauKy : 0) + phatSinh;
            if (luyKeNo != 0) ws.Cells[row, 9].Value = luyKeNo;
            if (luyKeCo != 0) ws.Cells[row, 10].Value = luyKeCo;
        }

        private static void MergeGroupHeader(ExcelWorksheet ws, int row, int col, string label)
        {
            ws.Cells[row, col, row, col + 1].Merge = true;
            ws.Cells[row, col].Value = label;
        }

        private static void ApplyAmountFormat(ExcelWorksheet ws, int fromRow, int toRow, int fromCol, int toCol)
        {
            if (toRow < fromRow) return;
            ws.Cells[fromRow, fromCol, toRow, toCol].Style.Numberformat.Format = AmountFormat;
        }

        // ── Helpers ──

        private static void WriteBalanceRow(ExcelWorksheet ws, int row, string label, decimal balance, bool bold = false)
        {
            ws.Cells[row, 4].Value = label;
            if (balance > 0) ws.Cells[row, 6].Value = balance;
            else if (balance < 0) ws.Cells[row, 7].Value = -balance;
            if (bold) StyleSummary(ws, row);
        }

        private static void WriteSummaryRow(ExcelWorksheet ws, int row, string label, decimal debit, decimal credit, bool bold = false)
        {
            ws.Cells[row, 4].Value = label;
            if (debit != 0) ws.Cells[row, 6].Value = debit;
            if (credit != 0) ws.Cells[row, 7].Value = credit;
            if (bold) StyleSummary(ws, row);
        }

        private static void StyleSummary(ExcelWorksheet ws, int row)
        {
            ws.Cells[row, 4].Style.Font.Bold = true;
            for (int c = 6; c <= 7; c++)
            {
                ws.Cells[row, c].Style.Font.Bold = true;
                ws.Cells[row, c].Style.Border.Top.Style = ExcelBorderStyle.Double;
            }
        }

        // ── Helpers S38-DN (Dư Nợ = cột 8, Dư Có = cột 9) ──

        private static void WriteBalanceRow38(ExcelWorksheet ws, int row, string label, decimal balance, bool bold = false)
        {
            ws.Cells[row, 4].Value = label;
            if (balance > 0) ws.Cells[row, 8].Value = balance;
            else if (balance < 0) ws.Cells[row, 9].Value = -balance;
            if (bold) StyleSummary38(ws, row);
        }

        private static void WriteRunningBalance(ExcelWorksheet ws, int row, decimal balance)
        {
            if (balance > 0) ws.Cells[row, 8].Value = balance;
            else if (balance < 0) ws.Cells[row, 9].Value = -balance;
        }

        private static void StyleSummary38(ExcelWorksheet ws, int row)
        {
            ws.Cells[row, 4].Style.Font.Bold = true;
            for (int c = 6; c <= 9; c++)
            {
                ws.Cells[row, c].Style.Font.Bold = true;
                ws.Cells[row, c].Style.Border.Top.Style = ExcelBorderStyle.Double;
            }
        }

        private static void Center(ExcelRange range, bool bold = false, int size = 11)
        {
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Font.Bold = bold;
            if (size != 11) range.Style.Font.Size = size;
        }
    }
}