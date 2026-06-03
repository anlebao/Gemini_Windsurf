using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Domain Service for Journal Template processing and validation
    /// Moved from Shared Domain to maintain Clean Architecture
    /// </summary>
    public class JournalTemplateService
    {
        private readonly ILogger<JournalTemplateService> _logger;

        public JournalTemplateService(ILogger<JournalTemplateService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Replace parameters in template strings
        /// Implementation moved from Shared Domain
        /// </summary>
        public string ReplaceParameters(string? template, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(template))
                return string.Empty;

            try
            {
                var result = template;
                foreach (var param in parameters)
                {
                    result = result.Replace($"{{{param.Key}}}", param.Value?.ToString() ?? string.Empty);
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing parameters in template: {Template}", template);
                return template ?? string.Empty;
            }
        }

        /// <summary>
        /// Validate Vietnamese account number format
        /// Implementation moved from Shared Domain
        /// </summary>
        public bool IsValidAccountNumber(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return false;

            // Vietnamese account numbers are typically 3 digits for main accounts
            // Can have sub-accounts with additional digits
            return accountNumber.Length >= 3 && 
                   accountNumber.Length <= 10 && 
                   int.TryParse(accountNumber, out _);
        }

        /// <summary>
        /// Validate template line data
        /// </summary>
        public bool ValidateTemplateLine(string accountNumber, bool isDebit, string? amountFormula)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return false;

            if (!IsValidAccountNumber(accountNumber))
                return false;

            if (isDebit && string.IsNullOrWhiteSpace(amountFormula))
                return false;

            return true;
        }

        /// <summary>
        /// Process template description with parameter replacement
        /// </summary>
        public string ProcessDescriptionTemplate(JournalTemplate template, Dictionary<string, object> parameters)
        {
            var descriptions = new List<string>();

            foreach (var line in template.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line.DescriptionTemplate))
                {
                    var processedDescription = ReplaceParameters(line.DescriptionTemplate, parameters);
                    descriptions.Add(processedDescription);
                }
            }

            return string.Join("; ", descriptions);
        }

        /// <summary>
        /// Get required parameters from template
        /// </summary>
        public IEnumerable<string> GetRequiredParameters(JournalTemplate template)
        {
            var parameters = new HashSet<string>();

            // Extract from description templates
            foreach (var line in template.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line.DescriptionTemplate))
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(line.DescriptionTemplate, @"\{(\w+)\}");
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        parameters.Add(match.Groups[1].Value);
                    }
                }
            }

            // Extract from amount formulas
            foreach (var line in template.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line.AmountFormula))
                {
                    // Simple parameter extraction from formulas
                    if (line.AmountFormula.Contains("Amount"))
                        parameters.Add("Amount");
                    if (line.AmountFormula.Contains("VatRate"))
                        parameters.Add("VatRate");
                    if (line.AmountFormula.Contains("COGSPercentage"))
                        parameters.Add("COGSPercentage");
                }
            }

            return parameters;
        }
    }
}
