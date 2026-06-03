using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Journal;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Enhanced Journal Factory with Business Rules Engine
    /// Implements Hybrid Architecture combining Generic Template and Rule Engine
    /// </summary>
    public class EnhancedJournalFactory : IEnhancedJournalFactory
    {
        private readonly IJournalTemplateRepository _templateRepository;
        private readonly ITemplateValidator _validator;
        private readonly IBusinessRuleRegistry _ruleRegistry;
        private readonly ILogger<EnhancedJournalFactory> _logger;
        private readonly JournalEntryService _journalEntryService;

        public EnhancedJournalFactory(
            IJournalTemplateRepository templateRepository,
            ITemplateValidator validator,
            IBusinessRuleRegistry ruleRegistry,
            ILogger<EnhancedJournalFactory> logger,
            JournalEntryService journalEntryService)
        {
            _templateRepository = templateRepository;
            _validator = validator;
            _ruleRegistry = ruleRegistry;
            _logger = logger;
            _journalEntryService = journalEntryService;
        }

        public async Task<JournalEntry> CreateFromTemplateAsync(
            TenantId tenantId, 
            string templateCode, 
            decimal amount, 
            Dictionary<string, object> parameters)
        {
            _logger.LogInformation("Creating journal entry from template {TemplateCode} for tenant {TenantId}", 
                templateCode, tenantId.Value);

            // 1. Get template
            var template = await _templateRepository.GetByCodeAsync(tenantId, templateCode);
            if (template == null)
                throw new NotFoundException($"Template not found: {templateCode}");

            // 2. Validate template
            var validationResult = await _validator.ValidateTemplateAsync(template, parameters);
            if (!validationResult.IsValid)
                throw new ValidationException($"Template validation failed: {string.Join("; ", validationResult.Errors)}");

            // 3. Create context
            var context = new TemplateContext(template, amount, parameters);

            // 4. Apply business rules
            foreach (var ruleName in template.BusinessRules)
            {
                var rule = _ruleRegistry.GetRule(ruleName);
                if (await rule.ShouldApplyAsync(context))
                {
                    await rule.ApplyAsync(context);
                    _logger.LogDebug("Applied business rule {RuleName} for template {TemplateCode}", 
                        ruleName, templateCode);
                }
            }

            // 5. Create journal entry
            var journal = new JournalEntry(tenantId, DateTime.UtcNow, template.Description);

            // 6. Generate lines from template
            foreach (var line in template.Lines)
            {
                var calculatedAmount = CalculateLineAmount(line, context);
                var description = ReplaceParameters(line.DescriptionTemplate, parameters);
                
                journal.AddLine(
                    line.AccountNumber,
                    line.IsDebit ? calculatedAmount : 0,
                    line.IsCredit ? calculatedAmount : 0,
                    description
                );
            }

            // 7. Validate final journal entry using domain service
            if (!_journalEntryService.IsBalanced(journal))
                throw new ValidationException("Generated journal entry is not balanced");

            if (!_journalEntryService.HasValidAccountNumbers(journal))
                throw new ValidationException("Journal entry contains invalid account numbers");

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
                var result = await _validator.ValidateTemplateAsync(template, parameters);
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
            var templates = await _templateRepository.GetByTenantAsync(tenantId);
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
                _ when line.AmountFormula?.Contains("*") == true => CalculateFormula(line.AmountFormula, context),
                _ => context.Amount
            };
        }

        private decimal CalculateFormula(string formula, TemplateContext context)
        {
            // Simple formula evaluation for common patterns
            // In production, consider using a proper expression parser
            if (formula == "Amount*0.1")
                return context.Amount * 0.1m;
            if (formula == "Amount*0.05")
                return context.Amount * 0.05m;
            if (formula == "Amount*0.1*VatRate")
            {
                var vatRate = context.GetParameter<decimal>("VatRate", 0.1m);
                return context.Amount * 0.1m * (vatRate / 100m);
            }
            
            _logger.LogWarning("Unsupported formula: {Formula}", formula);
            return context.Amount;
        }

        private string ReplaceParameters(string? template, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(template))
                return string.Empty;

            var result = template;
            foreach (var param in parameters)
            {
                result = result.Replace($"{{{param.Key}}}", param.Value?.ToString() ?? string.Empty);
            }
            return result;
        }
    }
}
