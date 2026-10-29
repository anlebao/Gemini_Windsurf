namespace VanAn.CoreHub.Services.Reconcile;

/// <summary>Mức độ kết quả tra soát (SRS FR-2).</summary>
public enum ReconcileLevel
{
    Pass = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

/// <summary>1 nhóm check (R1..R12) — SRS FR-2.1.</summary>
public sealed record ReconcileCheck(
    string Code,        // "R1".."R12"
    string Name,
    ReconcileLevel Level,
    string Summary,     // số liệu chính (chuỗi)
    string Details);    // chi tiết/bảng (trace)

public sealed record ReconcileSummary(
    int Pass, int Info, int Warning, int Error, int Total);

/// <summary>Kết quả tra soát 1 tenant + kỳ — SRS FR-2/FR-3.3 (runtime, không lưu).</summary>
public sealed record ReconcileResult(
    string Period,           // "YYYY-MM"
    Guid TenantId,
    string TenantName,
    DateTime RanAt,
    ReconcileSummary Summary,
    IReadOnlyList<ReconcileCheck> Checks,
    IReadOnlyList<string> Suggestions);

/// <summary>
/// Order mirror từ ShopERP SQLite (R11 cross-store — G8). Đọc từ ShopERPDbContext
/// (mirror orders + tenants), KHÔNG phải nguồn kế toán — chỉ để đối chiếu sync.
/// </summary>
public sealed record MirrorOrderDto(
    Guid OrderId,
    string? SourceType,
    string? Status,
    string? PaymentStatus,
    decimal DepositPaidAmount,
    decimal TotalAmount);

/// <summary>
/// Cung cấp order mirror (SQLite) cho R11. Gateway implement bằng HTTP gọi ShopERP
/// `GET /api/shoperp/accounting/mirror-orders?period=` (P2). Fail-open: lỗi/không cấu
/// hình → trả null → R11 skip với Info (không chặn các check khác).
/// </summary>
public interface IMirrorOrderProvider
{
    Task<IReadOnlyList<MirrorOrderDto>?> GetMirrorOrdersAsync(string period, CancellationToken ct = default);
}