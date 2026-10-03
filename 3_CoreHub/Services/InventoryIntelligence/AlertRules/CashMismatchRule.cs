using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.AlertRules
{
    /// <summary>
    /// Rule 8: CASH_MISMATCH — tiền mặt chênh lệch: |CashCount − POS Cash Total| &gt; CashTolerance (SRS §4.1).
    /// </summary>
    public sealed class CashMismatchRule : IAlertRule
    {
        public string AlertCode => "CASH_MISMATCH";
        public AlertSeverity Severity => AlertSeverity.Critical;

        public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
        {
            List<ShiftAlert> alerts = [];
            if (context.Shift.CashCount is decimal cash && context.Shift.PosCashTotal is decimal pos)
            {
                decimal diff = cash - pos;
                if (Math.Abs(diff) > thresholds.CashTolerance)
                {
                    alerts.Add(new ShiftAlert(
                        context.Shift.TenantId, context.Shift.Id, AlertCode, Severity,
                        $"Tiền mặt chênh lệch {diff:N0}đ (đếm {cash:N0}đ / POS {pos:N0}đ) > dung sai {thresholds.CashTolerance:N0}đ",
                        null, diff, null));
                }
            }
            return Task.FromResult<IReadOnlyList<ShiftAlert>>(alerts);
        }
    }
}
