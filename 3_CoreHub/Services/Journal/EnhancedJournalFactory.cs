using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Enhanced Journal Factory - Implementation moved from Shared Domain
    /// Combines Generic Template + Business Rules Engine
    /// </summary>
    public sealed class EnhancedJournalFactory(
        IJournalTemplateRepository templateRepository,
        IBusinessRuleRegistry ruleRegistry,
        ITemplateValidator validator,
        JournalEntryService journalEntryService)
    {
        private readonly IJournalTemplateRepository _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        private readonly IBusinessRuleRegistry _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
        private readonly ITemplateValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        private readonly JournalEntryService _journalEntryService = journalEntryService ?? throw new ArgumentNullException(nameof(journalEntryService));

        public async Task<JournalEntry> CreateFromTemplateAsync(
            TenantId tenantId,
            string templateCode,
            decimal amount,
            Dictionary<string, object> parameters)
        {
            // 1. Get template
            JournalTemplate? template = await _templateRepository.GetByCodeAsync(tenantId, templateCode) ?? throw new Shared.Domain.NotFoundException($"Template not found: {templateCode}");

            // 2. Validate template
            ValidationResult validationResult = await _validator.ValidateTemplateAsync(template, parameters);
            if (!validationResult.IsValid)
            {
                throw new Shared.Domain.ValidationException(string.Join("; ", validationResult.Errors));
            }

            // 3. Create context
            TemplateContext context = new(template, amount, parameters);

            // 4. Apply business rules
            foreach (string ruleName in template.BusinessRules)
            {
                try
                {
                    IBusinessRule rule = _ruleRegistry.GetRule(ruleName);
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
            JournalEntry journalEntry = new(
                tenantId,
                DateTime.UtcNow,
                template.Description,
                parameters.GetValueOrDefault("ReferenceType")?.ToString(),
                parameters.TryGetValue("ReferenceId", out object? refId) && refId is Guid guid ? guid : null
            );

            // 6. Validate account numbers in template lines
            foreach (JournalTemplateLine line in template.Lines)
            {
                if (!JournalEntryService.IsValidAccountNumber(line.AccountNumber))
                {
                    throw new Shared.Domain.ValidationException($"Invalid account number in template: {line.AccountNumber}");
                }
            }

            // 7. Generate lines from template
            foreach (JournalTemplateLine line in template.Lines)
            {
                decimal calculatedAmount = CalculateLineAmount(line, context);
                string description = ReplaceParameters(line.DescriptionTemplate, context.Parameters);

                journalEntry.AddLine(
                    line.AccountNumber,
                    line.IsDebit ? calculatedAmount : 0,
                    line.IsCredit ? calculatedAmount : 0,
                    description
                );
            }

            // 8. Validate journal balance when all lines use Amount-based formulas
            // Skip check for rule-modified formulas (NetAmount, VatAmount, COGS, etc.)
            HashSet<string> ruleModifiedFormulas = new(StringComparer.OrdinalIgnoreCase) { "NetAmount", "VatAmount", "COGS", "ImportTax" };
            bool allAmountBased = template.Lines.All(l =>
                string.IsNullOrEmpty(l.AmountFormula) ||
                l.AmountFormula == "Amount" ||
                l.AmountFormula == "TotalAmount" ||
                (l.AmountFormula.StartsWith("Amount*") && !l.AmountFormula.Contains("VatRate") &&
                !ruleModifiedFormulas.Contains(l.AmountFormula)));
            bool hasDebitLines = template.Lines.Any(l => l.IsDebit);
            bool hasCreditLines = template.Lines.Any(l => l.IsCredit);
            if (hasDebitLines && hasCreditLines && allAmountBased)
            {
                decimal totalDebit = journalEntry.Lines.Sum(l => l.DebitAmount);
                decimal totalCredit = journalEntry.Lines.Sum(l => l.CreditAmount);
                if (Math.Abs(totalDebit - totalCredit) >= 0.01m)
                {
                    throw new Shared.Domain.ValidationException($"Journal entry is not balanced: Debit={totalDebit}, Credit={totalCredit}");
                }
            }

            return journalEntry;
        }

        private static decimal CalculateLineAmount(JournalTemplateLine line, TemplateContext context)
        {
            return line.AmountFormula switch
            {
                "Amount" => context.Amount,
                "NetAmount" => context.NetAmount,
                "VatAmount" => context.VatAmount,
                "COGS" => context.COGS,
                "ImportTax" => context.ImportTaxAmount,
                "TotalAmount" => context.Amount,
                _ when line.AmountFormula?.Contains('*') == true => EvaluateFormula(line.AmountFormula, context),
                _ => context.Amount
            };
        }

        private static decimal EvaluateFormula(string formula, TemplateContext context)
        {
            // Handle Amount*X*VatRate pattern
            if (formula.StartsWith("Amount*") && formula.Contains("*VatRate"))
            {
                string middle = formula[7..formula.IndexOf("*VatRate")];
                if (decimal.TryParse(middle, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal m))
                {
                    decimal vatRate = context.Parameters.TryGetValue("VatRate", out object? vr) ? Convert.ToDecimal(vr) / 100m : 0m;
                    return context.Amount * m * vatRate;
                }
            }

            if (formula.StartsWith("Amount*"))
            {
                string rest = formula[7..];
                if (decimal.TryParse(rest, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal multiplier))
                {
                    return context.Amount * multiplier;
                }
            }

            if (formula.StartsWith("NetAmount*"))
            {
                string rest = formula[10..];
                if (decimal.TryParse(rest, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal multiplier))
                {
                    return context.NetAmount * multiplier;
                }
            }

            return context.Amount; // Fallback
        }

        private static string ReplaceParameters(string? template, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            string result = template;
            foreach (KeyValuePair<string, object> param in parameters)
            {
                result = result.Replace($"{{{param.Key}}}", param.Value?.ToString() ?? string.Empty);
            }
            return result;
        }
    }
}
