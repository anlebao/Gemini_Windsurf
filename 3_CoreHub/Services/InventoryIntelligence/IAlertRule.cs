using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.5): 1 rule = 1 class implement interface (SRS §7.4 — rule-based, declarative).
    /// </summary>
    public interface IAlertRule
    {
        /// <summary>Mã cảnh báo duy nhất (vd ING_VARIANCE_HIGH).</summary>
        string AlertCode { get; }

        /// <summary>Severity mặc định của rule.</summary>
        AlertSeverity Severity { get; }

        /// <summary>Đánh giá rule — trả về danh sách ShiftAlert (rỗng nếu không vi phạm).</summary>
        Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct);
    }
}
