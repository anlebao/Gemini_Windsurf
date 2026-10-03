using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.AlertRules
{
    /// <summary>
    /// Rule 2: STOCK_LOW — tồn kho thấp: ClosingCount &lt; ReorderPoint (MinStockThreshold — SRS §4.2).
    /// </summary>
    public sealed class StockLowRule : IAlertRule
    {
        public string AlertCode => "STOCK_LOW";
        public AlertSeverity Severity => AlertSeverity.Warning;

        public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
        {
            List<ShiftAlert> alerts = [];
            foreach ((Guid ingredientId, decimal closing) in context.ClosingCountByIngredient)
            {
                if (!context.Ingredients.TryGetValue(ingredientId, out Ingredient? ingredient))
                {
                    continue;
                }
                if (closing < ingredient.MinStockThreshold)
                {
                    alerts.Add(new ShiftAlert(
                        context.Shift.TenantId, context.Shift.Id, AlertCode, Severity,
                        $"Tồn kho thấp {ingredient.Name}: còn {closing:F1} {ingredient.Unit} < reorder point {ingredient.MinStockThreshold:F1} {ingredient.Unit}",
                        ingredientId, closing, null));
                }
            }
            return Task.FromResult<IReadOnlyList<ShiftAlert>>(alerts);
        }
    }
}
