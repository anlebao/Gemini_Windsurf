using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.AlertRules
{
    /// <summary>
    /// Rule 10: SHIFT_NOT_ACKNOWLEDGED — ca SUBMITTED &gt; X giờ chưa ACKNOWLEDGED (SRS §4.1).
    /// Đánh giá trong luồng đóng ca = no-op (ca vừa submit); hữu dụng khi engine chạy nền/real-time.
    /// </summary>
    public sealed class ShiftNotAcknowledgedRule : IAlertRule
    {
        public string AlertCode => "SHIFT_NOT_ACKNOWLEDGED";
        public AlertSeverity Severity => AlertSeverity.Warning;

        public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
        {
            List<ShiftAlert> alerts = [];
            if (context.Shift.Status == ShiftStatus.Submitted)
            {
                DateTime since = context.Shift.EndTime ?? context.Shift.StartTime;
                double elapsedHours = (context.Now - since).TotalHours;
                if (elapsedHours > thresholds.ShiftAckTimeoutHours)
                {
                    alerts.Add(new ShiftAlert(
                        context.Shift.TenantId, context.Shift.Id, AlertCode, Severity,
                        $"Ca {context.Shift.Id} chưa được xác nhận bàn giao sau {elapsedHours:F1}h (giới hạn {thresholds.ShiftAckTimeoutHours}h)",
                        null, null, null));
                }
            }
            return Task.FromResult<IReadOnlyList<ShiftAlert>>(alerts);
        }
    }
}
