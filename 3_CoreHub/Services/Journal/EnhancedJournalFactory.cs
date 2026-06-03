using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Enhanced Journal Factory - Implementation moved from Shared Domain
    /// Combines Generic Template + Business Rules Engine
    /// </summary>
    public sealed class EnhancedJournalFactory
    {
        private readonly IJournalTemplateRepository _templateRepository;
        private readonly IBusinessRuleRegistry _ruleRegistry;
        private readonly ITemplateValidator _validator;
        private readonly JournalEntryService _journalEntryService;

        public EnhancedJournalFactory(
            IJournalTemplateRepository templateRepository,
            IBusinessRuleRegistry ruleRegistry,
            ITemplateValidator validator,
            JournalEntryService journalEntryService)
        {
            _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
            _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _journalEntryService = journalEntryService ?? throw new ArgumentNullException(nameof(journalEntryService));
        }

        public async Task<JournalEntry> CreateFromTemplateAsync(
            TenantId tenantId,
            string templateCode,
            decimal amount,
            Dictionary<string, object> parameters)
        {
            // 1. Get template
            var template = await _templateRepository.GetByCodeAsync(tenantId, templateCode);
            if (template == null)
                throw new VanAn.Shared.Domain.NotFoundException($"Template not found: {templateCode}");

            // 2. Validate template
            var validationResult = await _validator.ValidateTemplateAsync(template, parameters);
            if (!validationResult.IsValid)
                throw new VanAn.Shared.Domain.ValidationException(string.Join("; ", validationResult.Errors));

            // 3. Create context
            var context = new TemplateContext(template, amount, parameters);

            // 4. Apply business rules
            foreach (var ruleName in template.BusinessRules)
            {
                try
                {
                    var rule = _ruleRegistry.GetRule(ruleName);
                    if (await rule.ShouldApplyAsync(context))
                    {
                        await rule.ApplyAsync(context);
                        context.AppliedRules.Add(ruleName);
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Rule not registered - skip
                }
            }

            // 5. Create journal entry
            var journalEntry = new JournalEntry(
                tenantId,
                DateTime.UtcNow,
                template.Description,
                parameters.GetValueOrDefault("ReferenceType")?.ToString(),
                parameters.TryGetValue("ReferenceId", out var refId) && refId is Guid guid ? guid : null
            );

            // 6. Validate account numbers in template lines
            foreach (var line in template.Lines)
            {
                if (!_journalEntryService.IsValidAccountNumber(line.AccountNumber))
                    throw new VanAn.Shared.Domain.ValidationException($"Invalid account number in template: {line.AccountNumber}");
            }

            // 7. Generate lines from template
            foreach (var line in template.Lines)
            {
                var calculatedAmount = CalculateLineAmount(line, context);
                var description = ReplaceParameters(line.DescriptionTemplate, context.Parameters);
                
                journalEntry.AddLine(
                    line.AccountNumber,
                    line.IsDebit ? calculatedAmount : 0,
                    line.IsCredit ? calculatedAmount : 0,
                    description
                );
            }

            // 8. Validate journal balance when all lines use Amount-based formulas
            // Skip check for rule-modified formulas (NetAmount, VatAmount, COGS, etc.)
            var ruleModifiedFormulas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NetAmount", "VatAmount", "COGS", "ImportTax" };
            var allAmountBased = template.Lines.All(l =>
                string.IsNullOrEmpty(l.AmountFormula) ||
                l.AmountFormula == "Amount" ||
                l.AmountFormula == "TotalAmount" ||
                (l.AmountFormula.StartsWith("Amount*") && !l.AmountFormula.Contains("VatRate")) &&
                !ruleModifiedFormulas.Contains(l.AmountFormula));
            var hasDebitLines = template.Lines.Any(l => l.IsDebit);
            var hasCreditLines = template.Lines.Any(l => l.IsCredit);
            if (hasDebitLines && hasCreditLines && allAmountBased)
            {
                var totalDebit = journalEntry.Lines.Sum(l => l.DebitAmount);
                var totalCredit = journalEntry.Lines.Sum(l => l.CreditAmount);
                if (Math.Abs(totalDebit - totalCredit) >= 0.01m)
                    throw new VanAn.Shared.Domain.ValidationException($"Journal entry is not balanced: Debit={totalDebit}, Credit={totalCredit}");
            }

            return journalEntry;
        }

        private decimal CalculateLineAmount(JournalTemplateLine line, TemplateContext context)
        {
            return line.AmountFormula switch
            {
                "Amount" => context.Amount,
                "NetAmount" => context.NetAmount,
                "VatAmount" => context.VatAmount,
                "COGS" => context.COGS,
                "ImportTax" => context.ImportTaxAmount,
                "TotalAmount" => context.Amount,
                _ when line.AmountFormula?.Contains("*") == true => EvaluateFormula(line.AmountFormula, context),
                _ => context.Amount
            };
        }

        private decimal EvaluateFormula(string formula, TemplateContext context)
        {
            // Handle Amount*X*VatRate pattern
            if (formula.StartsWith("Amount*") && formula.Contains("*VatRate"))
            {
                var middle = formula.Substring(7, formula.IndexOf("*VatRate") - 7);
                if (decimal.TryParse(middle, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var m))
                {
                    var vatRate = context.Parameters.TryGetValue("VatRate", out var vr) ? Convert.ToDecimal(vr) / 100m : 0m;
                    return context.Amount * m * vatRate;
                }
            }

            if (formula.StartsWith("Amount*"))
            {
                var rest = formula.Substring(7);
                if (decimal.TryParse(rest, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var multiplier))
                    return context.Amount * multiplier;
            }
            
            if (formula.StartsWith("NetAmount*"))
            {
                var rest = formula.Substring(10);
                if (decimal.TryParse(rest, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var multiplier))
                    return context.NetAmount * multiplier;
            }

            return context.Amount; // Fallback
        }

        private string ReplaceParameters(string? template, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            
            var result = template;
            foreach (var param in parameters)
            {
                result = result.Replace($"{{{param.Key}}}", param.Value?.ToString() ?? string.Empty);
            }
            return result;
        }
    }
}
