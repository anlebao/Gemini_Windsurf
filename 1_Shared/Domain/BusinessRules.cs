using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
    public class TemplateContext
    {
        public JournalTemplate Template { get; }
        public decimal Amount { get; }
        public Dictionary<string, object> Parameters { get; }
        public decimal NetAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal COGS { get; set; }
        public decimal ImportTaxAmount { get; set; }
        public List<string> AppliedRules { get; } = new();

        public TemplateContext(JournalTemplate template, decimal amount, Dictionary<string, object> parameters)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Amount = amount;
            NetAmount = amount; // Default to Amount; rules can override
            Parameters = parameters ?? new Dictionary<string, object>();
        }

        public T? GetParameter<T>(string key, T? defaultValue = default)
        {
            if (Parameters.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }

        public void SetParameter<T>(string key, T? value)
        {
            Parameters[key] = value!;
        }

        public bool HasParameter(string key) => Parameters.ContainsKey(key);
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

        public static ValidationResult Success() => 
            new(true, Array.Empty<string>(), Array.Empty<string>());
        
        public static ValidationResult Failure(params string[] errors) => 
            new(false, errors.ToList(), Array.Empty<string>());
        
        public static ValidationResult WithWarnings(params string[] warnings) => 
            new(true, Array.Empty<string>(), warnings.ToList());
        
        public static ValidationResult WithErrorsAndWarnings(string[] errors, string[] warnings) => 
            new(false, errors.ToList(), warnings.ToList());
    }

    public sealed class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }

    public sealed class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }
}
