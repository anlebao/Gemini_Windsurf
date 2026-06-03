using System.Collections.Generic;
using System.Diagnostics;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Context for tracing rule execution chains with detailed logging
    /// </summary>
    public sealed class RuleExecutionContext
    {
        private readonly List<RuleExecutionStep> _executionSteps = new();
        private readonly Stopwatch _stopwatch = new();
        private readonly TenantId _tenantId;
        private readonly string _journalTemplateCode;

        public TenantId TenantId => _tenantId;
        public string JournalTemplateCode => _journalTemplateCode;
        public IReadOnlyList<RuleExecutionStep> ExecutionSteps => _executionSteps.AsReadOnly();
        public TimeSpan TotalExecutionTime => _stopwatch.Elapsed;
        public bool HasFailures => _executionSteps.Exists(step => !step.IsSuccess);

        public RuleExecutionContext(TenantId tenantId, string journalTemplateCode)
        {
            _tenantId = tenantId;
            _journalTemplateCode = journalTemplateCode;
        }

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
            var step = new RuleExecutionStep
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
            var successCount = _executionSteps.Count(step => step.IsSuccess);
            var failureCount = _executionSteps.Count(step => !step.IsSuccess);
            var totalSteps = _executionSteps.Count;

            return $"Rule execution completed for template {_journalTemplateCode} (Tenant: {_tenantId.Value}): " +
                   $"{totalSteps} steps, {successCount} success, {failureCount} failures, " +
                   $"Total time: {_stopwatch.ElapsedMilliseconds}ms";
        }

        /// <summary>
        /// Get detailed execution report
        /// </summary>
        public string GetDetailedReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"=== Rule Execution Report ===");
            report.AppendLine($"Template: {_journalTemplateCode}");
            report.AppendLine($"Tenant: {_tenantId.Value}");
            report.AppendLine($"Total Steps: {_executionSteps.Count}");
            report.AppendLine($"Success: {_executionSteps.Count(step => step.IsSuccess)}");
            report.AppendLine($"Failures: {_executionSteps.Count(step => !step.IsSuccess)}");
            report.AppendLine($"Total Time: {_stopwatch.ElapsedMilliseconds}ms");
            report.AppendLine($"Execution Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            foreach (var step in _executionSteps)
            {
                report.AppendLine($"[{step.Timestamp:HH:mm:ss.fff}] {step.RuleName}");
                report.AppendLine($"  Description: {step.Description}");
                report.AppendLine($"  Success: {step.IsSuccess}");
                report.AppendLine($"  Execution Time: {step.ExecutionTime.TotalMilliseconds:F2}ms");
                
                if (!string.IsNullOrWhiteSpace(step.ErrorMessage))
                {
                    report.AppendLine($"  Error: {step.ErrorMessage}");
                }
                
                report.AppendLine();
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
