using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Journal;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Enhanced Journal Factory with Business Rules Engine
    /// Implements Hybrid Architecture combining Generic Template and Rule Engine
    /// </summary>
    public class EnhancedJournalFactory(
        IJournalTemplateRepository templateRepository,
        ITemplateValidator validator,
        IBusinessRuleRegistry ruleRegistry,
        ILogger<EnhancedJournalFactory> logger,
        JournalEntryService journalEntryService) : IEnhancedJournalFactory
    {
        private readonly IJournalTemplateRepository _templateRepository = templateRepository;
        private readonly ITemplateValidator _validator = validator;
        private readonly IBusinessRuleRegistry _ruleRegistry = ruleRegistry;
        private readonly ILogger<EnhancedJournalFactory> _logger = logger;
        private readonly JournalEntryService _journalEntryService = journalEntryService;

        public async Task<JournalEntry> CreateFromTemplateAsync(
            TenantId tenantId,
            string templateCode,
            decimal amount,
            Dictionary<string, object> parameters)
        {
            _logger.LogInformation("Creating journal entry from template {TemplateCode} for tenant {TenantId}",
                templateCode, tenantId.Value);

            // 1. Get template
            JournalTemplate? template = await _templateRepository.GetByCodeAsync(tenantId, templateCode) ?? throw new NotFoundException($"Template not found: {templateCode}");

            // 2. Validate template
            ValidationResult validationResult = await _validator.ValidateTemplateAsync(template, parameters);
            if (!validationResult.IsValid)
            {
                throw new ValidationException($"Template validation failed: {string.Join("; ", validationResult.Errors)}");
            }

            // 3. Create context
            TemplateContext context = new(template, amount, parameters);

            // 4. Apply business rules
            foreach (string ruleName in template.BusinessRules)
            {
                IBusinessRule rule = _ruleRegistry.GetRule(ruleName);
                if (await rule.ShouldApplyAsync(context))
                {
                    await rule.ApplyAsync(context);
                    _logger.LogDebug("Applied business rule {RuleName} for template {TemplateCode}",
                        ruleName, templateCode);
                }
            }

            // 5. Create journal entry
            JournalEntry journal = new(tenantId, DateTime.UtcNow, template.Description);

            // 6. Generate lines from template
            foreach (JournalTemplateLine line in template.Lines)
            {
                decimal calculatedAmount = CalculateLineAmount(line, context);
                string description = ReplaceParameters(line.DescriptionTemplate, parameters);

                journal.AddLine(
                    line.AccountNumber,
                    line.IsDebit ? calculatedAmount : 0,
                    line.IsCredit ? calculatedAmount : 0,
                    description
                );
            }

            // 7. Validate final journal entry using domain service
            if (!JournalEntryService.IsBalanced(journal))
            {
                throw new ValidationException("Generated journal entry is not balanced");
            }

            if (!JournalEntryService.HasValidAccountNumbers(journal))
            {
                throw new ValidationException("Journal entry contains invalid account numbers");
            }

            _logger.LogInformation("Successfully created journal entry {JournalNo} from template {TemplateCode}",
                journal.JournalNo, templateCode);

            return journal;
        }

        public async Task<bool> ValidateTemplateAsync(
            JournalTemplate template,
            Dictionary<string, object> parameters)
        {
            try
            {
                ValidationResult result = await _validator.ValidateTemplateAsync(template, parameters);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating template {TemplateCode}", template.Code);
                return false;
            }
        }

        public async Task<IEnumerable<string>> GetAvailableTemplatesAsync(TenantId tenantId)
        {
            IEnumerable<JournalTemplate> templates = await _templateRepository.GetByTenantAsync(tenantId);
            return templates.Where(t => t.IsActive).Select(t => t.Code);
        }

        private decimal CalculateLineAmount(JournalTemplateLine line, TemplateContext context)
        {
            return line.AmountFormula switch
            {
                "Amount" => context.Amount,
                "NetAmount" => context.NetAmount > 0 ? context.NetAmount : context.Amount,
                "VatAmount" => context.VatAmount > 0 ? context.VatAmount : context.Amount,
                "COGS" => context.COGS > 0 ? context.COGS : context.Amount,
                "ImportTax" => context.ImportTaxAmount > 0 ? context.ImportTaxAmount : context.Amount,
                "TotalAmount" => context.Amount,
                _ when line.AmountFormula?.Contains('*') == true => CalculateFormula(line.AmountFormula, context),
                _ => context.Amount
            };
        }

        private decimal CalculateFormula(string formula, TemplateContext context)
        {
            // Simple formula evaluation for common patterns
            // In production, consider using a proper expression parser
            if (formula == "Amount*0.1")
            {
                return context.Amount * 0.1m;
            }

            if (formula == "Amount*0.05")
            {
                return context.Amount * 0.05m;
            }

            if (formula == "Amount*0.1*VatRate")
            {
                decimal vatRate = context.GetParameter("VatRate", 0.1m);
                return context.Amount * 0.1m * (vatRate / 100m);
            }

            _logger.LogWarning("Unsupported formula: {Formula}", formula);
            return context.Amount;
        }

        private static string ReplaceParameters(string? template, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(template))
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
