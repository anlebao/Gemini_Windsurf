using System.Diagnostics;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Context for tracing rule execution chains with detailed logging
    /// </summary>
    public sealed class RuleExecutionContext(TenantId tenantId, string journalTemplateCode)
    {
        private readonly List<RuleExecutionStep> _executionSteps = [];
        private readonly Stopwatch _stopwatch = new();

        public TenantId TenantId { get; } = tenantId;
        public string JournalTemplateCode { get; } = journalTemplateCode;
        public IReadOnlyList<RuleExecutionStep> ExecutionSteps => _executionSteps.AsReadOnly();
        public TimeSpan TotalExecutionTime => _stopwatch.Elapsed;
        public bool HasFailures => _executionSteps.Exists(step => !step.IsSuccess);

        /// <summary>
        /// Start timing the rule execution
        /// </summary>
        public void StartExecution()
        {
            _stopwatch.Restart();
            AddStep("RuleExecutionStarted", "Starting rule chain execution", true);
        }

        /// <summary>
        /// Stop timing the rule execution
        /// </summary>
        public void StopExecution()
        {
            _stopwatch.Stop();
            AddStep("RuleExecutionCompleted", $"Rule chain execution completed in {_stopwatch.ElapsedMilliseconds}ms", true);
        }

        /// <summary>
        /// Add a rule execution step
        /// </summary>
        public void AddStep(string ruleName, string description, bool isSuccess, string? errorMessage = null)
        {
            RuleExecutionStep step = new()
            {
                RuleName = ruleName,
                Description = description,
                IsSuccess = isSuccess,
                ErrorMessage = errorMessage,
                ExecutionTime = _stopwatch.Elapsed,
                Timestamp = DateTime.UtcNow
            };

            _executionSteps.Add(step);
        }

        /// <summary>
        /// Add a successful rule execution step
        /// </summary>
        public void AddSuccessStep(string ruleName, string description)
        {
            AddStep(ruleName, description, true);
        }

        /// <summary>
        /// Add a failed rule execution step
        /// </summary>
        public void AddFailureStep(string ruleName, string description, string errorMessage)
        {
            AddStep(ruleName, description, false, errorMessage);
        }

        /// <summary>
        /// Get execution summary for logging
        /// </summary>
        public string GetExecutionSummary()
        {
            int successCount = _executionSteps.Count(step => step.IsSuccess);
            int failureCount = _executionSteps.Count(step => !step.IsSuccess);
            int totalSteps = _executionSteps.Count;

            return $"Rule execution completed for template {JournalTemplateCode} (Tenant: {TenantId.Value}): " +
                   $"{totalSteps} steps, {successCount} success, {failureCount} failures, " +
                   $"Total time: {_stopwatch.ElapsedMilliseconds}ms";
        }

        /// <summary>
        /// Get detailed execution report
        /// </summary>
        public string GetDetailedReport()
        {
            System.Text.StringBuilder report = new();
            _ = report.AppendLine($"=== Rule Execution Report ===");
            _ = report.AppendLine($"Template: {JournalTemplateCode}");
            _ = report.AppendLine($"Tenant: {TenantId.Value}");
            _ = report.AppendLine($"Total Steps: {_executionSteps.Count}");
            _ = report.AppendLine($"Success: {_executionSteps.Count(step => step.IsSuccess)}");
            _ = report.AppendLine($"Failures: {_executionSteps.Count(step => !step.IsSuccess)}");
            _ = report.AppendLine($"Total Time: {_stopwatch.ElapsedMilliseconds}ms");
            _ = report.AppendLine($"Execution Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            _ = report.AppendLine();

            foreach (RuleExecutionStep step in _executionSteps)
            {
                _ = report.AppendLine($"[{step.Timestamp:HH:mm:ss.fff}] {step.RuleName}");
                _ = report.AppendLine($"  Description: {step.Description}");
                _ = report.AppendLine($"  Success: {step.IsSuccess}");
                _ = report.AppendLine($"  Execution Time: {step.ExecutionTime.TotalMilliseconds:F2}ms");

                if (!string.IsNullOrWhiteSpace(step.ErrorMessage))
                {
                    _ = report.AppendLine($"  Error: {step.ErrorMessage}");
                }

                _ = report.AppendLine();
            }

            return report.ToString();
        }
    }

    /// <summary>
    /// Individual rule execution step for detailed tracing
    /// </summary>
    public sealed class RuleExecutionStep
    {
        public required string RuleName { get; init; }
        public required string Description { get; init; }
        public required bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public TimeSpan ExecutionTime { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
