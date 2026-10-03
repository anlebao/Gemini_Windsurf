using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.5): Alert Engine — chạy toàn bộ rule (trừ rule disabled), gom ShiftAlert.
    /// Evaluate khi đóng ca (SRS §7.4).
    /// </summary>
    public interface IAlertEngine
    {
        /// <summary>Đánh giá tất cả rule trên context — trả về ShiftAlert (CHƯA persist — caller quyết định).</summary>
        Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds? thresholds = null, CancellationToken ct = default);
    }
}
