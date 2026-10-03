using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.AlertRules
{
    /// <summary>
    /// Rule 1: ING_VARIANCE_HIGH — hao hụt nguyên liệu: Variance &gt; ngưỡng % (mặc định 5%).
    /// Override per-ingredient qua Ingredient.VarianceThresholdPercent (SRS §4.2).
    /// </summary>
    public sealed class IngVarianceHighRule : IAlertRule
    {
        public string AlertCode => "ING_VARIANCE_HIGH";
        public AlertSeverity Severity => AlertSeverity.Warning;

        public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
        {
            List<ShiftAlert> alerts = [];
            foreach (TheoreticalConsumption c in context.Consumptions)
            {
                if (c.TheoreticalQuantity <= 0m)
                {
                    continue; // không có định mức → không tính variance %
                }
                decimal thresholdPercent = context.Ingredients.TryGetValue(c.IngredientId, out Ingredient? ingredient)
                    ? ingredient.VarianceThresholdPercent ?? thresholds.VarianceThresholdPercent
                    : thresholds.VarianceThresholdPercent;

                if (c.VariancePercent > thresholdPercent)
                {
                    string name = context.Ingredients.TryGetValue(c.IngredientId, out Ingredient? ing) ? ing.Name : c.IngredientId.ToString();
                    alerts.Add(new ShiftAlert(
                        context.Shift.TenantId, context.Shift.Id, AlertCode, Severity,
                        $"Hao hụt {name}: variance {c.VariancePercent:F1}% > ngưỡng {thresholdPercent:F1}% (thực {c.ActualQuantity:F1} / lý thuyết {c.TheoreticalQuantity:F1})",
                        c.IngredientId, c.Variance, c.VariancePercent));
                }
            }
            return Task.FromResult<IReadOnlyList<ShiftAlert>>(alerts);
        }
    }
}
