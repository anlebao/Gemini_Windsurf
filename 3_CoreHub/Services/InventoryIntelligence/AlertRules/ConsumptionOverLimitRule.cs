using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.AlertRules
{
    /// <summary>
    /// Rule 3: CONSUMPTION_OVER_LIMIT — tiêu hao vượt định mức: Actual &gt; Theoretical × (1 + MaxVariance%) (SRS §4.1).
    /// </summary>
    public sealed class ConsumptionOverLimitRule : IAlertRule
    {
        public string AlertCode => "CONSUMPTION_OVER_LIMIT";
        public AlertSeverity Severity => AlertSeverity.Critical;

        public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
        {
            List<ShiftAlert> alerts = [];
            decimal limitFactor = 1m + (thresholds.MaxVariancePercent / 100m);
            foreach (TheoreticalConsumption c in context.Consumptions)
            {
                if (c.TheoreticalQuantity <= 0m)
                {
                    continue;
                }
                if (c.ActualQuantity > c.TheoreticalQuantity * limitFactor)
                {
                    string name = context.Ingredients.TryGetValue(c.IngredientId, out Ingredient? ingredient) ? ingredient.Name : c.IngredientId.ToString();
                    alerts.Add(new ShiftAlert(
                        context.Shift.TenantId, context.Shift.Id, AlertCode, Severity,
                        $"Tiêu hao vượt định mức {name}: thực {c.ActualQuantity:F1} > lý thuyết {c.TheoreticalQuantity:F1} × (1 + {thresholds.MaxVariancePercent:F0}%)",
                        c.IngredientId, c.Variance, c.VariancePercent));
                }
            }
            return Task.FromResult<IReadOnlyList<ShiftAlert>>(alerts);
        }
    }
}
