using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.AlertRules
{
    /// <summary>
    /// Rule 4: SALES_HIGH_STOCK_STABLE — doanh số cao nhưng nguyên liệu không giảm (gian lận tiềm ẩn):
    /// RevenueByIngredient &gt; SalesHighThreshold AND |StockChange| ≤ Opening × StockStableTolerance% (SRS §4.1).
    /// </summary>
    public sealed class SalesHighStockStableRule : IAlertRule
    {
        public string AlertCode => "SALES_HIGH_STOCK_STABLE";
        public AlertSeverity Severity => AlertSeverity.Critical;

        public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
        {
            List<ShiftAlert> alerts = [];
            foreach ((Guid ingredientId, decimal revenue) in context.RevenueByIngredient)
            {
                if (revenue <= thresholds.SalesHighThreshold)
                {
                    continue;
                }
                decimal opening = context.OpeningCountByIngredient.GetValueOrDefault(ingredientId);
                decimal stockChange = context.StockChangeByIngredient.GetValueOrDefault(ingredientId);
                decimal tolerance = opening * (thresholds.StockStableTolerancePercent / 100m);
                if (Math.Abs(stockChange) <= tolerance)
                {
                    string name = context.Ingredients.TryGetValue(ingredientId, out Ingredient? ingredient) ? ingredient.Name : ingredientId.ToString();
                    alerts.Add(new ShiftAlert(
                        context.Shift.TenantId, context.Shift.Id, AlertCode, Severity,
                        $"Doanh số cao ({revenue:N0}đ) nhưng {name} không giảm (stock change {stockChange:F1}) — kiểm tra gian lận",
                        ingredientId, stockChange, null));
                }
            }
            return Task.FromResult<IReadOnlyList<ShiftAlert>>(alerts);
        }
    }
}
