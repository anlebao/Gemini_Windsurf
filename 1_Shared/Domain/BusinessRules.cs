namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Business Rule Interfaces for Hybrid Architecture
    /// Source of Truth: VanAn.Shared.Domain
    /// Only pure interfaces - no implementation logic
    /// </summary>

    public interface IBusinessRule
    {
        string Name { get; }
        Task<bool> ShouldApplyAsync(TemplateContext context);
        Task ApplyAsync(TemplateContext context);
    }

    public interface IBusinessRuleRegistry
    {
        void RegisterRule(string ruleName, IBusinessRule rule);
        IBusinessRule GetRule(string ruleName);
        IEnumerable<IBusinessRule> GetAllRules();
        IEnumerable<string> GetRegisteredRules();
        bool HasRule(string ruleName);
    }

    public interface ITemplateValidator
    {
        Task<ValidationResult> ValidateTemplateAsync(JournalTemplate template, Dictionary<string, object> parameters);
        Task<ValidationResult> ValidateTemplateAsync(JournalTemplate template, Dictionary<string, object> parameters, bool validateParameters);
    }

    public interface IJournalService
    {
        Task<JournalEntry> CreateJournalEntryAsync(TenantId tenantId, DateTime date, string description, string? referenceType = null, Guid? referenceId = null);
        Task<JournalEntry> CreateReversalEntryAsync(JournalEntry originalEntry, string? reason = null);
        Task<JournalEntry> AddLineAsync(JournalEntry entry, string accountNumber, decimal debitAmount, decimal creditAmount, string? description = null);
        Task<JournalEntry> ValidateAndSaveAsync(JournalEntry entry);
    }

    public interface IJournalTemplateRepository
    {
        Task<JournalTemplate?> GetByCodeAsync(TenantId tenantId, string code);
        Task<IEnumerable<JournalTemplate>> GetByTenantAsync(TenantId tenantId);
        Task<JournalTemplate> CreateAsync(JournalTemplate template);
        Task<JournalTemplate> UpdateAsync(JournalTemplate template);
        Task DeleteAsync(TenantId tenantId, string code);
    }

    // Forward declaration for TemplateContext - implementation in CoreHub
    public class TemplateContext(JournalTemplate template, decimal amount, Dictionary<string, object> parameters)
    {
        public JournalTemplate Template { get; } = template ?? throw new ArgumentNullException(nameof(template));
        public decimal Amount { get; } = amount;
        public Dictionary<string, object> Parameters { get; } = parameters ?? [];
        public decimal NetAmount { get; set; } = amount; // Default to Amount; rules can override
        public decimal DiscountAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal COGS { get; set; }
        public decimal ImportTaxAmount { get; set; }
        public List<string> AppliedRules { get; } = [];

        public T? GetParameter<T>(string key, T? defaultValue = default)
        {
            return Parameters.TryGetValue(key, out object? value) && value is T typedValue ? typedValue : defaultValue;
        }

        public void SetParameter<T>(string key, T? value)
        {
            Parameters[key] = value!;
        }

        public bool HasParameter(string key)
        {
            return Parameters.ContainsKey(key);
        }
    }

    /// <summary>
    /// Immutable ValidationResult record for thread-safe validation results
    /// </summary>
    public sealed record ValidationResult
    {
        public bool IsValid { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = null!;
        public IReadOnlyList<string> Warnings { get; init; } = null!;

        private ValidationResult(bool isValid, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            IsValid = isValid;
            Errors = errors;
            Warnings = warnings;
        }

        public static ValidationResult Success()
        {
            return new(true, Array.Empty<string>(), Array.Empty<string>());
        }

        public static ValidationResult Failure(params string[] errors)
        {
            return new(false, errors.ToList(), Array.Empty<string>());
        }

        public static ValidationResult WithWarnings(params string[] warnings)
        {
            return new(true, Array.Empty<string>(), warnings.ToList());
        }

        public static ValidationResult WithErrorsAndWarnings(string[] errors, string[] warnings)
        {
            return new(false, errors.ToList(), warnings.ToList());
        }
    }

    public sealed class ValidationException(string message) : Exception(message)
    {
    }

    public sealed class NotFoundException(string message) : Exception(message)
    {
    }
}
