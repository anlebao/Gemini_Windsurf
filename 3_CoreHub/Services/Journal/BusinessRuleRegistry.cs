using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Business Rule Registry Implementation
    /// Manages registration and retrieval of business rules
    /// </summary>
    public sealed class BusinessRuleRegistry : IBusinessRuleRegistry
    {
        private readonly Dictionary<string, IBusinessRule> _rules;
        private readonly ILogger<BusinessRuleRegistry> _logger;

        public BusinessRuleRegistry(ILogger<BusinessRuleRegistry> logger,
                                   ILogger<VIPDiscountRule> vipLogger,
                                   ILogger<VietnameseVATRule> vatLogger,
                                   ILogger<CorporateIncomeTaxRule> citLogger,
                                   ILogger<PersonalIncomeTaxRule> pitLogger,
                                   ILogger<PeriodClosingValidationRule> periodLogger,
                                   ILogger<AccountingStandardComplianceRule> complianceLogger)
        {
            _logger = logger;
            _rules = [];

            // Register Vietnamese accounting rules
            RegisterRule("VietnameseVAT", new VietnameseVATRule(vatLogger));
            RegisterRule("CorporateIncomeTax", new CorporateIncomeTaxRule(citLogger));
            RegisterRule("PersonalIncomeTax", new PersonalIncomeTaxRule(pitLogger));
            RegisterRule("PeriodClosingValidation", new PeriodClosingValidationRule(periodLogger));
            RegisterRule("AccountingStandardCompliance", new AccountingStandardComplianceRule(complianceLogger));

            // Keep legacy rules for compatibility
            RegisterRule("VIPDiscount", new VIPDiscountRule(vipLogger));
        }

        public void RegisterRule(string ruleName, IBusinessRule rule)
        {
            if (string.IsNullOrEmpty(ruleName))
            {
                throw new ArgumentException("Rule name cannot be null or empty", nameof(ruleName));
            }

            if (_rules.ContainsKey(ruleName))
            {
                throw new InvalidOperationException($"Rule '{ruleName}' is already registered");
            }

            _rules[ruleName] = rule ?? throw new ArgumentNullException(nameof(rule));
        }

        public IBusinessRule GetRule(string ruleName)
        {
            return string.IsNullOrEmpty(ruleName)
                ? throw new ArgumentException("Rule name cannot be null or empty", nameof(ruleName))
                : _rules.TryGetValue(ruleName, out IBusinessRule? rule) ? rule : throw new KeyNotFoundException($"Rule '{ruleName}' not found");
        }

        public IEnumerable<IBusinessRule> GetAllRules()
        {
            return _rules.Values.ToList();
        }

        public IEnumerable<string> GetRegisteredRules()
        {
            return _rules.Keys.ToList();
        }

        public bool HasRule(string ruleName)
        {
            return !string.IsNullOrEmpty(ruleName) && _rules.ContainsKey(ruleName);
        }
    }
}
