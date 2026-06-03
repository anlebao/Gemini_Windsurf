using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Journal Template for Hybrid Architecture - Data-driven journal creation
    /// Source of Truth: VanAn.Shared.Domain
    /// </summary>
    public sealed class JournalTemplate : BaseEntity, IMustHaveTenant
    {
        public TenantId TenantId { get; } = null!;
        public string Code { get; } = null!;
        public string Description { get; } = null!;
        public bool IsActive { get; }
        public DateTime CreatedAt { get; }
        public DateTime? UpdatedAt { get; }

        private readonly List<JournalTemplateLine> _lines = [];
        public IReadOnlyCollection<JournalTemplateLine> Lines => _lines.AsReadOnly();

        private readonly List<string> _businessRules = [];
        public IReadOnlyCollection<string> BusinessRules => _businessRules.AsReadOnly();

        private readonly List<TemplateValidationRule> _validationRules = [];
        public IReadOnlyCollection<TemplateValidationRule> ValidationRules => _validationRules.AsReadOnly();

        // EF Core constructor
#pragma warning disable CS8618
        private JournalTemplate() { }
#pragma warning restore CS8618

        public JournalTemplate(
            TenantId tenantId,
            string code,
            string description,
            bool isActive = true)
            : base(tenantId)
        {
            TenantId = tenantId;
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentNullException(nameof(code));
            }

            Code = code;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            IsActive = isActive;
            CreatedAt = DateTime.UtcNow;
        }

        public void AddLine(string accountNumber, bool isDebit, string? amountFormula, string? descriptionTemplate = null)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                throw new ArgumentException("Account number is required", nameof(accountNumber));
            }

            if (isDebit && string.IsNullOrWhiteSpace(amountFormula))
            {
                throw new ArgumentException("Amount formula is required for debit lines", nameof(amountFormula));
            }

            _lines.Add(new JournalTemplateLine(accountNumber, isDebit, amountFormula, descriptionTemplate));
        }

        public void AddBusinessRule(string ruleName)
        {
            if (string.IsNullOrWhiteSpace(ruleName))
            {
                throw new ArgumentException("Rule name is required", nameof(ruleName));
            }

            if (!_businessRules.Contains(ruleName))
            {
                _businessRules.Add(ruleName);
            }
        }

        public void AddValidationRule(string rule, string? message = null)
        {
            if (string.IsNullOrWhiteSpace(rule))
            {
                throw new ArgumentException("Rule is required", nameof(rule));
            }

            _validationRules.Add(new TemplateValidationRule(rule, message));
        }
    }

    public sealed class JournalTemplateLine
    {
        public string AccountNumber { get; private set; } = string.Empty;
        public bool IsDebit { get; private set; }
        public bool IsCredit => !IsDebit;
        public string? AmountFormula { get; private set; }
        public string? DescriptionTemplate { get; private set; }

        // EF Core constructor for materialization
        protected JournalTemplateLine() { }

        public JournalTemplateLine(string accountNumber, bool isDebit, string? amountFormula, string? descriptionTemplate)
        {
            AccountNumber = accountNumber;
            IsDebit = isDebit;
            AmountFormula = amountFormula;
            DescriptionTemplate = descriptionTemplate;
        }
    }

    public sealed class TemplateValidationRule
    {
        public string Rule { get; private set; } = string.Empty;
        public string? Message { get; private set; }

        // EF Core constructor for materialization
        protected TemplateValidationRule() { }

        public TemplateValidationRule(string rule, string? message)
        {
            Rule = rule;
            Message = message;
        }
    }
}
