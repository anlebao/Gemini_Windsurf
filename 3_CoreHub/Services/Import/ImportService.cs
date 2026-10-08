using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services.CongNo;
using VanAn.Shared.Domain;
using AccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services.Import;

/// <summary>
/// NHẬP LIỆU & SỔ SÁCH P4 (#1 Import Excel, master plan §5.4):
/// - Mẫu xlsx + csv cùng bộ cột: Ngày | Loại phiếu | Tài khoản | Số tiền | Đối tượng | MST | Diễn giải | Số chứng từ.
/// - Dry-run bắt buộc (Q4): Preview validate toàn bộ → bảng lỗi theo dòng; ImportAsync chỉ lưu khi 0 lỗi.
/// - Lưu qua IAccountingService/ICongNoService → AccountingEntry immutable + period guard + dấu +/−
///   + JournalEntry tự sinh (P2 — chỉ thu doanh thu/chi phí; công nợ KHÔNG JE [G6]).
/// - Duplicate-check C-1 (5 phút — reference-aware) replicate: nếu 2 dòng cùng khóa trong file/batch gần đây
///   → báo lỗi dòng (tránh bị CheckDuplicateEntryAsync chặn giữa chừng khi lưu).
/// </summary>
public sealed class ImportService(
    IAccountingService accountingService,
    ICongNoService congNoService,
    IPeriodClosingService periodClosingService,
    IAccountingEntryRepository accountingEntryRepository,
    ILogger<ImportService> logger) : IImportService
{
    private const int MaxRows = 2000;
    private const int DuplicateWindowMinutes = 5;

    private static readonly string[] Header =
        ["Ngày", "Loại phiếu", "Tài khoản", "Số tiền", "Đối tượng", "MST", "Diễn giải", "Số chứng từ"];

    // ── Records ─────────────────────────────────────────────────────────────

    private sealed record ParsedLine(
        int RowNumber,
        DateTime Date,
        ImportVoucherType VoucherType,
        string AccountCode,
        decimal Amount,
        string? DoiTuong,
        string? Mst,
        string? Description,
        string? Reference);

    private sealed record ParseResult(
        List<ParsedLine> Lines,
        List<ImportLineError> Errors,
        string? FatalError);

    // ── Public API ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ImportPreviewResult> PreviewAsync(TenantId tenantId, Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        ParseResult parsed = await ParseAsync(stream, fileName, cancellationToken);
        if (parsed.FatalError != null)
        {
            return new ImportPreviewResult(0, 0, 0, [], parsed.FatalError);
        }

        List<ImportLineError> errors = new(parsed.Errors);
        await ValidatePeriodsAndDuplicatesAsync(tenantId, parsed.Lines, errors, cancellationToken);

        int valid = parsed.Lines.Count(l => !errors.Any(e => e.RowNumber == l.RowNumber));
        return new ImportPreviewResult(
            parsed.Lines.Count,
            valid,
            errors.Count,
            errors,
            errors.Count == 0
                ? $"Sẵn sàng lưu: {parsed.Lines.Count} dòng hợp lệ."
                : $"{errors.Count} dòng lỗi — sửa file rồi tải lại (0 lỗi mới được lưu).");
    }

    /// <inheritdoc />
    public async Task<ImportResult> ImportAsync(TenantId tenantId, Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        ParseResult parsed = await ParseAsync(stream, fileName, cancellationToken);
        if (parsed.FatalError != null)
        {
            return new ImportResult(0, 0, 0, [new ImportLineError(0, parsed.FatalError)], parsed.FatalError);
        }

        List<ImportLineError> errors = new(parsed.Errors);
        await ValidatePeriodsAndDuplicatesAsync(tenantId, parsed.Lines, errors, cancellationToken);

        if (errors.Count > 0)
        {
            return new ImportResult(
                parsed.Lines.Count, 0, 0, errors,
                $"Có {errors.Count} dòng lỗi — KHÔNG lưu gì. Sửa file rồi thử lại (Q4: 0 lỗi mới lưu).");
        }

        int imported = 0;
        List<ImportLineError> runtimeErrors = new();
        foreach (ParsedLine line in parsed.Lines)
        {
            try
            {
                await SaveLineAsync(tenantId, line, cancellationToken);
                imported++;
            }
            catch (Exception ex)
            {
                // Fail-safe: lỗi 1 dòng không ảnh hưởng các dòng khác (master plan §6.1.4)
                logger.LogWarning(ex, "Import line {Row} failed (tenant {TenantId}) — skip", line.RowNumber, tenantId.Value);
                runtimeErrors.Add(new ImportLineError(line.RowNumber, ex.Message));
            }
        }

        logger.LogInformation("Import done: {Imported}/{Total} lines cho tenant {TenantId} (errors {Errors})",
            imported, parsed.Lines.Count, tenantId.Value, runtimeErrors.Count);

        return new ImportResult(
            parsed.Lines.Count,
            imported,
            runtimeErrors.Count,
            runtimeErrors,
            $"Đã lưu {imported}/{parsed.Lines.Count} dòng."
                + (runtimeErrors.Count > 0 ? $" ({runtimeErrors.Count} dòng lỗi runtime — xem bảng)." : string.Empty));
    }

    /// <inheritdoc />
    public Task<byte[]> GenerateTemplateAsync(ImportFileFormat format, CancellationToken cancellationToken = default)
        => format == ImportFileFormat.Xlsx
            ? Task.FromResult(BuildXlsxTemplate())
            : Task.FromResult(BuildCsvTemplate());

    // ── Parse ───────────────────────────────────────────────────────────────

    private static async Task<ParseResult> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        List<string[]> rawRows;
        if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            rawRows = await ParseXlsxAsync(stream, cancellationToken);
        }
        else if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            rawRows = ParseCsv(stream);
        }
        else
        {
            return new ParseResult([], [], "Định dạng không hỗ trợ — chỉ nhận .xlsx hoặc .csv.");
        }

        if (rawRows.Count == 0)
        {
            return new ParseResult([], [], "File rỗng.");
        }

        // Bỏ header (dòng 1) + dòng trống
        List<ParsedLine> lines = new();
        List<ImportLineError> errors = new();
        for (int i = 1; i < rawRows.Count; i++)
        {
            string[] cells = rawRows[i];
            if (IsEmptyRow(cells))
            {
                continue;
            }

            int rowNumber = i + 1; // hiển thị = số dòng trong file (gồm header)
            (ParsedLine? line, string? error) = MapRow(rowNumber, cells);
            if (error != null)
            {
                errors.Add(new ImportLineError(rowNumber, error));
            }
            else if (line != null)
            {
                lines.Add(line);
            }
        }

        if (lines.Count > MaxRows)
        {
            return new ParseResult([], errors, $"Quá giới hạn {MaxRows} dòng/lần import (file có {lines.Count} dòng).");
        }

        return new ParseResult(lines, errors, null);
    }

    private static async Task<List<string[]>> ParseXlsxAsync(Stream stream, CancellationToken cancellationToken)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using ExcelPackage package = new();
        await package.LoadAsync(stream, cancellationToken);
        ExcelWorksheet? ws = package.Workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("File xlsx không có sheet dữ liệu.");

        List<string[]> rows = new();
        int endRow = ws.Dimension?.End.Row ?? 0;
        for (int r = 1; r <= endRow; r++)
        {
            string[] cells = new string[Header.Length];
            for (int c = 0; c < Header.Length; c++)
            {
                object? val = ws.Cells[r, c + 1].Value;
                cells[c] = val == null ? string.Empty : NormalizeCellValue(val);
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static List<string[]> ParseCsv(Stream stream)
    {
        // leaveOpen: true — service KHÔNG được đóng stream của caller (UI giữ lại để ImportAsync đọc lại)
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        string text = reader.ReadToEnd();
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..]; // strip BOM (Excel CSV)
        }

        List<string[]> rows = new();
        foreach (string rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            rows.Add(SplitCsvLine(rawLine));
        }

        return rows;
    }

    /// <summary>CSV split đơn giản — hỗ trợ dấu nháy kép; ưu tiên dấu phẩy, fallback dấu chấm phẩy.</summary>
    private static string[] SplitCsvLine(string line)
    {
        char delimiter = CountChar(line, ',') >= CountChar(line, ';') ? ',' : ';';
        List<string> cells = new();
        StringBuilder current = new();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        cells.Add(current.ToString().Trim());
        return cells.ToArray();
    }

    private static int CountChar(string s, char c) => s.Count(ch => ch == c);

    private static bool IsEmptyRow(string[] cells)
        => cells.All(string.IsNullOrWhiteSpace);

    private static string NormalizeCellValue(object value)
        => value switch
        {
            DateTime dt => dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.DateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            _ => value.ToString()?.Trim() ?? string.Empty,
        };

    // ── Row mapping + validation ────────────────────────────────────────────

    private static (ParsedLine? Line, string? Error) MapRow(int rowNumber, string[] cells)
    {
        string rawDate = Get(cells, 0);
        if (!TryParseDate(rawDate, out DateTime date))
        {
            return (null, $"Ngày '{rawDate}' không hợp lệ (dùng dd/MM/yyyy).");
        }

        ImportVoucherType? voucherType = ParseVoucherType(Get(cells, 1));
        if (voucherType == null)
        {
            return (null, $"Loại phiếu '{Get(cells, 1)}' không hợp lệ. Dùng: thu-doanh-thu · chi-phí · ghi-nhan-phai-thu · thu-tien-khach-tra-no · ghi-nhan-phai-tra · tra-tien-nguoi-ban.");
        }

        string accountCode = Get(cells, 2).Trim();
        if (!IsValidAccountCode(voucherType.Value, accountCode, out string? accountError))
        {
            return (null, accountError);
        }

        if (!TryParseAmount(Get(cells, 3), out decimal amount) || amount <= 0)
        {
            return (null, $"Số tiền '{Get(cells, 3)}' không hợp lệ (phải > 0).");
        }

        string doiTuong = Get(cells, 4).Trim();
        if (IsCongNoType(voucherType.Value) && string.IsNullOrWhiteSpace(doiTuong))
        {
            return (null, "Đối tượng bắt buộc cho phiếu công nợ.");
        }

        string? mst = NullIfBlank(Get(cells, 5));
        string? description = NullIfBlank(Get(cells, 6));
        string? reference = NullIfBlank(Get(cells, 7));

        return (new ParsedLine(
            rowNumber, date, voucherType.Value, accountCode, amount,
            NullIfBlank(doiTuong), mst, description, reference), null);
    }

    private static string Get(string[] cells, int index)
        => index < cells.Length ? (cells[index] ?? string.Empty).Trim() : string.Empty;

    private static string? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseDate(string raw, out DateTime date)
    {
        raw = raw.Trim();
        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy/MM/dd"];
        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateTime.TryParse(raw, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out date);
    }

    private static bool TryParseAmount(string raw, out decimal amount)
    {
        raw = raw.Trim().Replace(" ", string.Empty).Replace("₫", string.Empty).Replace("đ", string.Empty);
        if (raw.Length == 0)
        {
            amount = 0;
            return false;
        }

        bool hasComma = raw.Contains(',');
        bool hasDot = raw.Contains('.');
        if (hasComma && !hasDot)
        {
            // vi-VN: "1.234.567,89"
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out amount);
        }

        if (hasDot && !hasComma)
        {
            // "1234.56" (invariant thập phân) vs "1.234" (vi nghìn)
            string[] parts = raw.Split('.');
            bool looksLikeDecimal = parts.Length == 2 && parts[1].Length == 2 && parts[0].Length <= 3;
            if (looksLikeDecimal)
            {
                return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
            }

            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out amount);
        }

        if (hasComma && hasDot)
        {
            // ưu tiên vi-VN ("1.234.567,89"); fallback invariant ("1,234.56")
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out amount)
                || decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
        }

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private static bool IsValidAccountCode(ImportVoucherType type, string accountCode, out string? error)
    {
        error = null;
        if (!accountCode.All(char.IsDigit) || accountCode.Length is < 3 or > 4)
        {
            error = $"Tài khoản '{accountCode}' không hợp lệ (phải là số 3-4 chữ số).";
            return false;
        }

        bool ok = type switch
        {
            ImportVoucherType.Revenue => accountCode.StartsWith('5') || accountCode.StartsWith('7'),
            ImportVoucherType.Expense => accountCode.StartsWith('6'),
            ImportVoucherType.Receivable or ImportVoucherType.ReceivablePayment => accountCode == "131",
            ImportVoucherType.Payable or ImportVoucherType.PayablePayment => accountCode == "331",
            _ => false,
        };
        if (!ok)
        {
            error = $"Tài khoản '{accountCode}' không hợp lệ cho loại phiếu '{type}' (thu: 5xx/7xx · chi: 6xx · phải thu: 131 · phải trả: 331).";
        }

        return ok;
    }

    private static bool IsCongNoType(ImportVoucherType type)
        => type is ImportVoucherType.Receivable or ImportVoucherType.ReceivablePayment
            or ImportVoucherType.Payable or ImportVoucherType.PayablePayment;

    /// <summary>Chuẩn hóa token loại phiếu: lower + bỏ dấu tiếng Việt + bỏ space/hyphen → đối sánh alias.</summary>
    private static ImportVoucherType? ParseVoucherType(string raw)
    {
        string key = NormalizeVnText(raw);
        return key switch
        {
            "thudoanhthu" or "revenue" => ImportVoucherType.Revenue,
            "chiphi" or "expense" => ImportVoucherType.Expense,
            "ghinhanphaithu" or "receivable" => ImportVoucherType.Receivable,
            "thutienkhachtrano" or "receivablepayment" => ImportVoucherType.ReceivablePayment,
            "ghinhanphaitra" or "payable" => ImportVoucherType.Payable,
            "tratiennguoiban" or "payablepayment" => ImportVoucherType.PayablePayment,
            _ => null,
        };
    }

    private static string NormalizeVnText(string raw)
    {
        // Bỏ dấu tiếng Việt (FormD tách tổ hợp dấu; 'đ' không tách được → thay trước)
        string lower = raw.Trim().ToLowerInvariant().Replace("đ", "d");
        StringBuilder sb = new(lower.Length);
        foreach (char ch in lower.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(ch) && CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    // ── Period + duplicate validation (dry-run & import dùng chung) ─────────

    private async Task ValidatePeriodsAndDuplicatesAsync(
        TenantId tenantId, List<ParsedLine> lines, List<ImportLineError> errors, CancellationToken cancellationToken)
    {
        // C-1 window: load 1 lần các phiếu Revenue/Expense gần đây (CreatedAt — đúng semantics chống double-click)
        DateTime windowStart = DateTime.UtcNow.AddMinutes(-DuplicateWindowMinutes);
        DateTime windowEnd = DateTime.UtcNow.AddSeconds(1);
        List<AccountingEntry> recent = (await accountingEntryRepository.GetByTenantAndDateRangeAsync(
            tenantId, windowStart, windowEnd, cancellationToken)).ToList();

        HashSet<string> batchKeys = new();
        foreach (ParsedLine line in lines)
        {
            // Kỳ chưa đóng
            AccountingPeriod period = new(line.Date.Year, line.Date.Month);
            PeriodClosingStatus status = await periodClosingService.GetPeriodStatusAsync(period, tenantId, cancellationToken);
            if (status == PeriodClosingStatus.Closed)
            {
                errors.Add(new ImportLineError(line.RowNumber, $"Kỳ {period.Year}/{period.Month:D2} đã đóng sổ — không thêm bút toán."));
                continue;
            }

            // Duplicate C-1 chỉ áp dụng cho thu doanh thu/chi phí (CongNoService không có duplicate-check)
            if (line.VoucherType is not (ImportVoucherType.Revenue or ImportVoucherType.Expense))
            {
                continue;
            }

            AccountingEntryType entryType = line.VoucherType == ImportVoucherType.Revenue
                ? AccountingEntryType.Revenue
                : AccountingEntryType.Expense;

            string key = BuildDuplicateKey(line, entryType, line.Reference);
            bool duplicateInBatch = !batchKeys.Add(key);
            bool duplicateRecent = recent.Any(e =>
                e.EntryType == entryType
                && e.Amount == line.Amount
                && (e.AccountCode ?? string.Empty) == line.AccountCode
                && (string.IsNullOrWhiteSpace(line.Reference)
                    || e.Reference == line.Reference));
            if (duplicateInBatch || duplicateRecent)
            {
                errors.Add(new ImportLineError(line.RowNumber,
                    "Trùng lặp trong 5 phút (cùng số tiền/tài khoản" + (string.IsNullOrWhiteSpace(line.Reference) ? "" : "/số chứng từ")
                    + ") — điền Số chứng từ khác nhau hoặc kiểm tra file vừa import."));
            }
        }
    }

    private static string BuildDuplicateKey(ParsedLine line, AccountingEntryType entryType, string? reference)
        => string.IsNullOrWhiteSpace(reference)
            ? $"{entryType}|{line.Amount}|{line.AccountCode}"
            : $"{entryType}|{line.Amount}|{line.AccountCode}|{reference}";

    // ── Save ────────────────────────────────────────────────────────────────

    private async Task SaveLineAsync(TenantId tenantId, ParsedLine line, CancellationToken cancellationToken)
    {
        AccountingPeriod period = new(line.Date.Year, line.Date.Month);
        string fallbackDescription = $"Import Excel {line.Date:dd/MM/yyyy}";

        switch (line.VoucherType)
        {
            case ImportVoucherType.Revenue:
                await accountingService.CreateRevenueEntryAsync(
                    tenantId, period, line.Amount,
                    line.Description ?? fallbackDescription,
                    accountCode: line.AccountCode,
                    reference: line.Reference,
                    transactionDate: line.Date);
                break;

            case ImportVoucherType.Expense:
                await accountingService.CreateExpenseEntryAsync(
                    tenantId, period, line.Amount,
                    line.Description ?? fallbackDescription,
                    accountCode: line.AccountCode,
                    reference: line.Reference,
                    transactionDate: line.Date);
                break;

            case ImportVoucherType.Receivable:
                await congNoService.CreateReceivableAsync(
                    tenantId, line.Amount, line.DoiTuong!, line.Description, line.Date,
                    isPayment: false, mst: line.Mst, reference: line.Reference, cancellationToken: cancellationToken);
                break;

            case ImportVoucherType.ReceivablePayment:
                await congNoService.CreateReceivableAsync(
                    tenantId, line.Amount, line.DoiTuong!, line.Description, line.Date,
                    isPayment: true, mst: line.Mst, reference: line.Reference, cancellationToken: cancellationToken);
                break;

            case ImportVoucherType.Payable:
                await congNoService.CreatePayableAsync(
                    tenantId, line.Amount, line.DoiTuong!, line.Description, line.Date,
                    isPayment: false, mst: line.Mst, reference: line.Reference, cancellationToken: cancellationToken);
                break;

            case ImportVoucherType.PayablePayment:
                await congNoService.CreatePayableAsync(
                    tenantId, line.Amount, line.DoiTuong!, line.Description, line.Date,
                    isPayment: true, mst: line.Mst, reference: line.Reference, cancellationToken: cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Loại phiếu không hỗ trợ: {line.VoucherType}");
        }
    }

    // ── Template ────────────────────────────────────────────────────────────

    private static byte[] BuildXlsxTemplate()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using ExcelPackage package = new();
        ExcelWorksheet ws = package.Workbook.Worksheets.Add("Import");
        for (int c = 0; c < Header.Length; c++)
        {
            ws.Cells[1, c + 1].Value = Header[c];
            ws.Cells[1, c + 1].Style.Font.Bold = true;
        }

        string[][] samples =
        [
            ["01/10/2026", "thu-doanh-thu", "511", "1000000", "", "", "Thu tiền bán hàng", "PT-0001"],
            ["01/10/2026", "ghi-nhan-phai-thu", "131", "2000000", "Công ty ABC", "0312345678", "Bán chịu", "HD-0001"],
        ];
        for (int r = 0; r < samples.Length; r++)
        {
            for (int c = 0; c < samples[r].Length; c++)
            {
                ws.Cells[r + 2, c + 1].Value = samples[r][c];
            }
        }

        ws.Cells[1, 1, 1 + samples.Length, Header.Length].AutoFitColumns();
        return package.GetAsByteArray();
    }

    private static byte[] BuildCsvTemplate()
    {
        StringBuilder sb = new();
        _ = sb.Append('\uFEFF'); // BOM — Excel mở đúng UTF-8
        _ = sb.AppendLine(string.Join(",", Header));
        _ = sb.AppendLine("01/10/2026,thu-doanh-thu,511,1000000,,,\"Thu tiền bán hàng\",PT-0001");
        _ = sb.AppendLine("01/10/2026,ghi-nhan-phai-thu,131,2000000,\"Công ty ABC\",0312345678,\"Bán chịu\",HD-0001");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
