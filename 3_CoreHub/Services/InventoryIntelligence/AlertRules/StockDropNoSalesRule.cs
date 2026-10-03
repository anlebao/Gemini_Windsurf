using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.AlertRules
{
    /// <summary>
    /// Rule 5: STOCK_DROP_NO_SALES — nguyên liệu giảm nhưng doanh số không tăng (thất thoát):
    /// StockChange &lt; 0 AND RevenueByIngredient ≈ 0 (SRS §4.1).
    /// </summary>
    public sealed class StockDropNoSalesRule : IAlertRule
    {
        public string AlertCode => "STOCK_DROP_NO_SALES";
        public AlertSeverity Severity => AlertSeverity.Critical;

        public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
        {
            List<ShiftAlert> alerts = [];
            foreach ((Guid ingredientId, decimal stockChange) in context.StockChangeByIngredient)
            {
                decimal revenue = context.RevenueByIngredient.GetValueOrDefault(ingredientId);
                if (stockChange < 0m && revenue <= 0m)
                {
                    string name = context.Ingredients.TryGetValue(ingredientId, out Ingredient? ingredient) ? ingredient.Name : ingredientId.ToString();
                    alerts.Add(new ShiftAlert(
                        context.Shift.TenantId, context.Shift.Id, AlertCode, Severity,
                        $"{name} giảm {Math.Abs(stockChange):F1} nhưng doanh số 0 — kiểm tra thất thoát",
                        ingredientId, stockChange, null));
                }
            }
            return Task.FromResult<IReadOnlyList<ShiftAlert>>(alerts);
        }
    }
}
