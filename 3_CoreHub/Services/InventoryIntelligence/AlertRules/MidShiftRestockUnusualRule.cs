using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.AlertRules
{
    /// <summary>
    /// Rule 6: MIDSHIFT_RESTOCK_UNUSUAL — nhập thêm bất thường trong ca:
    /// Restock &gt; Opening × RestockUnusualThreshold% (outlier — SRS §4.1).
    /// </summary>
    public sealed class MidShiftRestockUnusualRule : IAlertRule
    {
        public string AlertCode => "MIDSHIFT_RESTOCK_UNUSUAL";
        public AlertSeverity Severity => AlertSeverity.Warning;

        public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
        {
            List<ShiftAlert> alerts = [];
            foreach ((Guid ingredientId, decimal restock) in context.RestockByIngredient)
            {
                if (restock <= 0m)
                {
                    continue;
                }
                decimal opening = context.OpeningCountByIngredient.GetValueOrDefault(ingredientId);
                decimal limit = opening * (thresholds.RestockUnusualThresholdPercent / 100m);
                if (restock > limit)
                {
                    string name = context.Ingredients.TryGetValue(ingredientId, out Ingredient? ingredient) ? ingredient.Name : ingredientId.ToString();
                    alerts.Add(new ShiftAlert(
                        context.Shift.TenantId, context.Shift.Id, AlertCode, Severity,
                        $"Nhập thêm bất thường {name}: {restock:F1} > {thresholds.RestockUnusualThresholdPercent:F0}% của đầu ca ({opening:F1})",
                        ingredientId, restock, null));
                }
            }
            return Task.FromResult<IReadOnlyList<ShiftAlert>>(alerts);
        }
    }
}
