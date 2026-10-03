using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.AlertRules
{
    /// <summary>
    /// Rule 9: RECIPE_MISSING — product có bán hàng nhưng chưa định nghĩa recipe (SRS §4.1).
    /// </summary>
    public sealed class RecipeMissingRule : IAlertRule
    {
        public string AlertCode => "RECIPE_MISSING";
        public AlertSeverity Severity => AlertSeverity.Critical;

        public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
        {
            List<ShiftAlert> alerts = [];
            foreach ((Guid productId, decimal revenue) in context.RevenueByProduct)
            {
                if (!context.RecipesByProduct.ContainsKey(productId) && revenue > 0m)
                {
                    alerts.Add(new ShiftAlert(
                        context.Shift.TenantId, context.Shift.Id, AlertCode, Severity,
                        $"Product {productId} đã bán ({revenue:N0}đ) nhưng chưa có recipe — không tính được tiêu hao lý thuyết",
                        null, null, null));
                }
            }
            return Task.FromResult<IReadOnlyList<ShiftAlert>>(alerts);
        }
    }
}
