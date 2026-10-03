using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.AlertRules
{
    /// <summary>
    /// Rule 7: CONSUMABLE_OVER_STANDARD — ly/nắp/ống hút (IngredientCategory.Consumable) dùng vượt chuẩn:
    /// ActualUsage &gt; UnitsSold × StandardRatio × (1 + Tolerance%) (SRS §4.1).
    /// </summary>
    public sealed class ConsumableOverStandardRule : IAlertRule
    {
        public string AlertCode => "CONSUMABLE_OVER_STANDARD";
        public AlertSeverity Severity => AlertSeverity.Warning;

        public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
        {
            List<ShiftAlert> alerts = [];
            decimal standardLimit = context.TotalUnitsSold * thresholds.ConsumableStandardRatio * (1m + (thresholds.ConsumableTolerancePercent / 100m));

            foreach (TheoreticalConsumption c in context.Consumptions)
            {
                if (!context.Ingredients.TryGetValue(c.IngredientId, out Ingredient? ingredient) || ingredient.Category != IngredientCategory.Consumable)
                {
                    continue;
                }
                if (c.ActualQuantity > standardLimit)
                {
                    alerts.Add(new ShiftAlert(
                        context.Shift.TenantId, context.Shift.Id, AlertCode, Severity,
                        $"{ingredient.Name} dùng {c.ActualQuantity:F1} > chuẩn {context.TotalUnitsSold} món × {thresholds.ConsumableStandardRatio:F1} × (1 + {thresholds.ConsumableTolerancePercent:F0}%)",
                        c.IngredientId, c.ActualQuantity, null));
                }
            }
            return Task.FromResult<IReadOnlyList<ShiftAlert>>(alerts);
        }
    }
}
