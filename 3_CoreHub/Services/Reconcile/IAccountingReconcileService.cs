using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Reconcile;

/// <summary>
/// Tra soát số liệu kế toán 1 tenant + kỳ (SRS v1.1 — read-only, manual on-demand).
/// Chạy 12 nhóm check R1-R12 → ReconcileResult (Error/Warning/Info/Pass + gợi ý fix).
/// KHÔNG ghi dữ liệu (NFR-1 read-only tuyệt đối).
/// </summary>
public interface IAccountingReconcileService
{
    /// <summary>Chạy tra soát tenant + kỳ. Fail-safe từng check (1 lỗi không chặn check khác).</summary>
    Task<ReconcileResult> RunAsync(TenantId tenantId, AccountingPeriod period, CancellationToken ct = default);
}