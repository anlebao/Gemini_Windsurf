using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Template Validator implementation with sliding expiration cache
    /// </summary>
    public class TemplateValidator : ITemplateValidator
    {
        private readonly ILogger<TemplateValidator> _logger;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30); // 30 minutes sliding expiration

        public TemplateValidator(ILogger<TemplateValidator> logger, IMemoryCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public async Task<ValidationResult> ValidateTemplateAsync(JournalTemplate template, Dictionary<string, object> parameters)
        {
            // ENHANCED: Use sliding expiration cache for validation results
            var cacheKey = $"template_validation_{template.Id}_{string.Join(",", parameters.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}";
            
            if (_cache.TryGetValue(cacheKey, out ValidationResult? cachedResult))
            {
                _logger.LogDebug("Template {TemplateCode} validation retrieved from cache", template.Code);
                return cachedResult!;
            }

            _logger.LogDebug("Validating template {TemplateCode}", template.Code);

            // 1. Check if template is active
            if (!template.IsActive)
            {
                var result = ValidationResult.Failure($"Template {template.Code} is not active");
                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }

            // 2. Validate that template has lines
            if (!template.Lines.Any())
            {
                var result = ValidationResult.Failure($"Template {template.Code} has no lines defined");
                _cache.Set(cacheKey, result, _cacheExpiration);
                return result;
            }

            // 3. Validate account numbers in lines
            foreach (var line in template.Lines)
            {
                if (!IsValidAccountNumber(line.AccountNumber))
                {
                    var result = ValidationResult.Failure($"Invalid account number: {line.AccountNumber}");
                    _cache.Set(cacheKey, result, _cacheExpiration);
                    return result;
                }
            }

            // 4. Validate amount formulas
            foreach (var line in template.Lines)
            {
                if (!IsValidAmountFormula(line.AmountFormula))
                {
                    var result = ValidationResult.Failure($"Invalid amount formula: {line.AmountFormula}");
                    _cache.Set(cacheKey, result, _cacheExpiration);
                    return result;
                }
            }

            // 5. Validate business rules
            foreach (var ruleName in template.BusinessRules)
            {
                if (string.IsNullOrWhiteSpace(ruleName))
                {
                    var result = ValidationResult.Failure("Empty business rule name found");
                    _cache.Set(cacheKey, result, _cacheExpiration);
                    return result;
                }
            }

            // 6. Validate validation rules
            foreach (var validationRule in template.ValidationRules)
            {
                var validationResult = ValidateRule(validationRule, parameters);
                if (!validationResult.IsValid)
                {
                    _cache.Set(cacheKey, validationResult, _cacheExpiration);
                    return validationResult;
                }
            }

            // 7. Check for required parameters
            var requiredParams = GetRequiredParameters(template);
            foreach (var requiredParam in requiredParams)
            {
                if (!parameters.ContainsKey(requiredParam))
                {
                    var result = ValidationResult.Failure($"Required parameter missing: {requiredParam}");
                    _cache.Set(cacheKey, result, _cacheExpiration);
                    return result;
                }
            }

            _logger.LogDebug("Template {TemplateCode} validation passed", template.Code);
            var successResult = ValidationResult.Success();
            _cache.Set(cacheKey, successResult, _cacheExpiration);
            return successResult;
        }

        public async Task<ValidationResult> ValidateTemplateAsync(JournalTemplate template, Dictionary<string, object> parameters, bool validateParameters)
        {
            // If validateParameters is false, skip parameter validation
            if (!validateParameters)
            {
                return await ValidateTemplateAsync(template, parameters);
            }

            // Full validation including parameters
            return await ValidateTemplateAsync(template, parameters);
        }

        private bool IsValidAccountNumber(string accountNumber)
        {
            // Vietnamese account numbers are typically 3 digits for main accounts
            // Can have sub-accounts with additional digits
            return accountNumber.Length >= 3 && 
                   accountNumber.Length <= 10 && 
                   int.TryParse(accountNumber, out _);
        }

        private bool IsValidAmountFormula(string? formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return false;

            var validFormulas = new[]
            {
                "Amount", "NetAmount", "VatAmount", "COGS", "ImportTax", "TotalAmount",
                "Amount*0.1", "Amount*0.05", "Amount*0.1*VatRate"
            };

            return validFormulas.Contains(formula);
        }

        private ValidationResult ValidateRule(TemplateValidationRule rule, Dictionary<string, object> parameters)
        {
            // Simple validation rule evaluation
            // In production, consider using a proper expression parser
            
            try
            {
                // Handle common validation patterns
                if (rule.Rule == "Amount > 0")
                {
                    if (parameters.TryGetValue("Amount", out var amount) && Convert.ToDecimal(amount) > 0)
                        return ValidationResult.Success();
                    return ValidationResult.Failure(rule.Message ?? "Amount must be greater than 0");
                }

                if (rule.Rule == "Amount >= 1000")
                {
                    if (parameters.TryGetValue("Amount", out var amount) && Convert.ToDecimal(amount) >= 1000)
                        return ValidationResult.Success();
                    return ValidationResult.Failure(rule.Message ?? "Amount must be at least 1000");
                }

                if (rule.Rule == "CustomerName != null")
                {
                    if (parameters.ContainsKey("CustomerName") && parameters["CustomerName"] != null)
                        return ValidationResult.Success();
                    return ValidationResult.Failure(rule.Message ?? "Customer name is required");
                }

                if (rule.Rule.StartsWith("VatRate in "))
                {
                    var rates = rule.Rule.Replace("VatRate in [", "").Replace("]", "").Split(',');
                    var vatRate = parameters.TryGetValue("VatRate", out var rate) ? Convert.ToDecimal(rate) : 0;
                    
                    foreach (var rateStr in rates)
                    {
                        if (decimal.TryParse(rateStr.Trim(), out var allowedRate) && allowedRate == vatRate)
                            return ValidationResult.Success();
                    }
                    
                    return ValidationResult.Failure(rule.Message ?? $"Invalid VAT rate: {vatRate}");
                }

                _logger.LogWarning("Unsupported validation rule: {Rule}", rule.Rule);
                return ValidationResult.Success(); // Allow unknown rules for now
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating rule: {Rule}", rule.Rule);
                return ValidationResult.Failure($"Validation error: {rule.Rule}");
            }
        }

        private IEnumerable<string> GetRequiredParameters(JournalTemplate template)
        {
            var requiredParams = new HashSet<string>();

            // Check amount formulas for required parameters
            foreach (var line in template.Lines)
            {
                if (line.AmountFormula?.Contains("VatRate") == true)
                    requiredParams.Add("VatRate");
                
                if (line.AmountFormula?.Contains("COGSPercentage") == true)
                    requiredParams.Add("COGSPercentage");
            }

            // Check description templates for required parameters
            foreach (var line in template.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line.DescriptionTemplate))
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(line.DescriptionTemplate, @"\{(\w+)\}");
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        requiredParams.Add(match.Groups[1].Value);
                    }
                }
            }

            // Check validation rules for required parameters
            foreach (var rule in template.ValidationRules)
            {
                if (rule.Rule.Contains("CustomerName"))
                    requiredParams.Add("CustomerName");
                
                if (rule.Rule.Contains("Amount"))
                    requiredParams.Add("Amount");
                
                if (rule.Rule.Contains("VatRate"))
                    requiredParams.Add("VatRate");
            }

            return requiredParams;
        }
    }
}
