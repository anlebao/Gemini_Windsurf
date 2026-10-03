using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.5): gom kết quả từ 10 <see cref="IAlertRule"/> (SRS §7.4).
    /// Rule disabled per-tenant bỏ qua; kết quả không trùng (AlertCode + IngredientId).
    /// </summary>
    public sealed class AlertEngine(IEnumerable<IAlertRule> rules, ILogger<AlertEngine> logger) : IAlertEngine
    {
        private readonly IReadOnlyList<IAlertRule> _rules = rules.ToList();
        private readonly ILogger<AlertEngine> _logger = logger;

        public async Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds? thresholds = null, CancellationToken ct = default)
        {
            AlertThresholds effective = thresholds ?? new AlertThresholds();
            List<ShiftAlert> alerts = [];
            HashSet<(string Code, Guid? IngredientId)> seen = [];

            foreach (IAlertRule rule in _rules)
            {
                if (effective.DisabledRules.Contains(rule.AlertCode))
                {
                    _logger.LogDebug("Rule {AlertCode} disabled — skipped", rule.AlertCode);
                    continue;
                }

                try
                {
                    IReadOnlyList<ShiftAlert> ruleAlerts = await rule.EvaluateAsync(context, effective, ct);
                    foreach (ShiftAlert alert in ruleAlerts)
                    {
                        if (seen.Add((alert.AlertCode, alert.IngredientId)))
                        {
                            alerts.Add(alert);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 1 rule lỗi không chặn các rule khác — fail-safe (SRS §7.4).
                    _logger.LogError(ex, "Alert rule {AlertCode} failed", rule.AlertCode);
                }
            }

            _logger.LogInformation("Alert engine: {RuleCount} rules → {AlertCount} alerts for shift {ShiftId}",
                _rules.Count, alerts.Count, context.Shift.Id);
            return alerts;
        }
    }
}
