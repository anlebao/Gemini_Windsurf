using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Domain Service for Journal Template processing and validation
    /// Moved from Shared Domain to maintain Clean Architecture
    /// </summary>
    public partial class JournalTemplateService(ILogger<JournalTemplateService> logger)
    {
        private readonly ILogger<JournalTemplateService> _logger = logger;

        /// <summary>
        /// Replace parameters in template strings
        /// Implementation moved from Shared Domain
        /// </summary>
        public string ReplaceParameters(string? template, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            try
            {
                string result = template;
                foreach (KeyValuePair<string, object> param in parameters)
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
        public static bool IsValidAccountNumber(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                return false;
            }

            // Vietnamese account numbers are typically 3 digits for main accounts
            // Can have sub-accounts with additional digits
            return accountNumber.Length >= 3 &&
                   accountNumber.Length <= 10 &&
                   int.TryParse(accountNumber, out _);
        }

        /// <summary>
        /// Validate template line data
        /// </summary>
        public static bool ValidateTemplateLine(string accountNumber, bool isDebit, string? amountFormula)
        {
            return !string.IsNullOrWhiteSpace(accountNumber)
&& IsValidAccountNumber(accountNumber) && (!isDebit || !string.IsNullOrWhiteSpace(amountFormula));
        }

        /// <summary>
        /// Process template description with parameter replacement
        /// </summary>
        public string ProcessDescriptionTemplate(JournalTemplate template, Dictionary<string, object> parameters)
        {
            List<string> descriptions = [];

            foreach (JournalTemplateLine line in template.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line.DescriptionTemplate))
                {
                    string processedDescription = ReplaceParameters(line.DescriptionTemplate, parameters);
                    descriptions.Add(processedDescription);
                }
            }

            return string.Join("; ", descriptions);
        }

        /// <summary>
        /// Get required parameters from template
        /// </summary>
        public static IEnumerable<string> GetRequiredParameters(JournalTemplate template)
        {
            HashSet<string> parameters = [];

            // Extract from description templates
            foreach (JournalTemplateLine line in template.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line.DescriptionTemplate))
                {
                    MatchCollection matches = MyRegex().Matches(line.DescriptionTemplate);
                    foreach (Match match in matches.Cast<Match>())
                    {
                        _ = parameters.Add(match.Groups[1].Value);
                    }
                }
            }

            // Extract from amount formulas
            foreach (JournalTemplateLine line in template.Lines)
            {
                if (!string.IsNullOrWhiteSpace(line.AmountFormula))
                {
                    // Simple parameter extraction from formulas
                    if (line.AmountFormula.Contains("Amount"))
                    {
                        _ = parameters.Add("Amount");
                    }

                    if (line.AmountFormula.Contains("VatRate"))
                    {
                        _ = parameters.Add("VatRate");
                    }

                    if (line.AmountFormula.Contains("COGSPercentage"))
                    {
                        _ = parameters.Add("COGSPercentage");
                    }
                }
            }

            return parameters;
        }

        [GeneratedRegex(@"\{(\w+)\}")]
        private static partial Regex MyRegex();
    }
}
