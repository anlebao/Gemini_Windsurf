namespace VanAn.CoreHub.Services.Import;

using VanAn.Shared.Domain;

public enum ImportFileFormat
{
    Xlsx,
    Csv,
}

/// <summary>
/// 6 loại phiếu — khớp UI phiếu thu/chi (THU CHI & CÔNG NỢ P2: RevenueEntry/ExpenseEntry).
/// </summary>
public enum ImportVoucherType
{
    /// <summary>thu-doanh-thu — thu doanh thu (TK 5xx/7xx) → CreateRevenueEntryAsync (tự sinh JE — P2).</summary>
    Revenue,

    /// <summary>chi-phí — chi phí (TK 6xx) → CreateExpenseEntryAsync (tự sinh JE — P2).</summary>
    Expense,

    /// <summary>ghi-nhan-phai-thu — bán chịu (TK 131) → CreateReceivableAsync(isPayment: false) — KHÔNG JE [G6].</summary>
    Receivable,

    /// <summary>thu-tien-khach-tra-no — thu nợ (TK 131) → CreateReceivableAsync(isPayment: true).</summary>
    ReceivablePayment,

    /// <summary>ghi-nhan-phai-tra — mua chịu (TK 331) → CreatePayableAsync(isPayment: false) — KHÔNG JE [G6].</summary>
    Payable,

    /// <summary>tra-tien-nguoi-ban — trả nợ (TK 331) → CreatePayableAsync(isPayment: true).</summary>
    PayablePayment,
}

public sealed record ImportLineError(int RowNumber, string Error);

public sealed record ImportPreviewResult(
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    IReadOnlyList<ImportLineError> Errors,
    string Message);

public sealed record ImportResult(
    int TotalRows,
    int ImportedRows,
    int SkippedRows,
    IReadOnlyList<ImportLineError> Errors,
    string Message);

/// <summary>
/// NHẬP LIỆU & SỔ SÁCH P4 (#1 Import Excel, master plan §5.4 — Q3 xlsx+CSV · Q4 dry-run 0 lỗi mới lưu).
/// Mẫu: Ngày | Loại phiếu | Tài khoản | Số tiền | Đối tượng | MST | Diễn giải | Số chứng từ.
/// Lưu qua IAccountingService/ICongNoService — giữ immutable + period guard + dấu +/− + JE tự sinh (P2).
/// </summary>
public interface IImportService
{
    /// <summary>Đọc + validate TOÀN BỘ dòng (dry-run — KHÔNG ghi gì). Trả về lỗi theo dòng (Q4).</summary>
    Task<ImportPreviewResult> PreviewAsync(TenantId tenantId, Stream stream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Import: chỉ lưu khi 0 lỗi (re-validate nội bộ — Q4). Lỗi 1 dòng không ảnh hưởng dòng khác.</summary>
    Task<ImportResult> ImportAsync(TenantId tenantId, Stream stream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Tạo mẫu file (xlsx/csv) — cùng bộ cột + 2 dòng ví dụ.</summary>
    Task<byte[]> GenerateTemplateAsync(ImportFileFormat format, CancellationToken cancellationToken = default);
}
