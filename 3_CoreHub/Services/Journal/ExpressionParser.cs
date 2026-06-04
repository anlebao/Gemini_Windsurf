using System.Globalization;
using System.Text.RegularExpressions;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Expression Parser interface for formula evaluation
    /// </summary>
    public interface IExpressionParser
    {
        decimal Evaluate(string expression, TemplateContext context, CultureInfo? cultureInfo = null);
        bool CanParse(string expression);
        IEnumerable<string> GetRequiredParameters(string expression);
    }

    /// <summary>
    /// Concrete Expression Parser implementation with Vietnamese accounting formulas
    /// </summary>
    public partial class ExpressionParser : IExpressionParser
    {
        private readonly Dictionary<string, Func<TemplateContext, decimal>> _functions;
        private readonly Regex _parameterRegex;
        private readonly Regex _functionRegex;

        public ExpressionParser()
        {
            _functions = new Dictionary<string, Func<TemplateContext, decimal>>(StringComparer.OrdinalIgnoreCase)
            {
                // Basic arithmetic functions
                ["Amount"] = ctx => ctx.Amount,
                ["NetAmount"] = ctx => ctx.NetAmount,
                ["VatAmount"] = ctx => ctx.VatAmount,
                ["COGS"] = ctx => ctx.COGS,
                ["ImportTaxAmount"] = ctx => ctx.ImportTaxAmount,
                ["DiscountAmount"] = ctx => ctx.DiscountAmount,

                // Vietnamese accounting specific formulas
                ["GrossRevenue"] = ctx => ctx.Amount,
                ["NetRevenue"] = ctx => ctx.NetAmount,
                ["TaxableAmount"] = ctx => ctx.NetAmount > 0 ? ctx.NetAmount : ctx.Amount,

                // Helper functions
                ["Max"] = ctx => Math.Max(ctx.Amount, ctx.NetAmount),
                ["Min"] = ctx => Math.Min(ctx.Amount, ctx.NetAmount),
                ["Abs"] = ctx => Math.Abs(ctx.Amount),
            };

            _parameterRegex = MyRegex();
            _functionRegex = MyRegex1();
        }

        public decimal Evaluate(string expression, TemplateContext context, CultureInfo? cultureInfo = null)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return 0;
            }

            cultureInfo ??= CultureInfo.InvariantCulture;

            try
            {
                // Step 1: Replace parameters {ParamName} with actual values
                string processedExpression = ReplaceParameters(expression, context, cultureInfo);

                // Step 2: Replace function calls with actual values
                processedExpression = ReplaceFunctions(processedExpression, context, cultureInfo);

                // Step 3: Evaluate the resulting arithmetic expression
                return EvaluateArithmeticExpression(processedExpression, cultureInfo);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to evaluate expression '{expression}': {ex.Message}", ex);
            }
        }

        public bool CanParse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            try
            {
                // Check if expression contains valid parameters or functions
                bool hasValidTokens = _parameterRegex.IsMatch(expression) ||
                                  _functions.Keys.Any(func => expression.Contains(func, StringComparison.OrdinalIgnoreCase));

                if (!hasValidTokens)
                {
                    return false;
                }

                // Basic syntax validation
                return !expression.Contains("++") && !expression.Contains("--") &&
                       expression.Count(c => c == '(') == expression.Count(c => c == ')');
            }
            catch
            {
                return false;
            }
        }

        public IEnumerable<string> GetRequiredParameters(string expression)
        {
            HashSet<string> parameters = [];

            // Find parameter references {ParamName}
            MatchCollection parameterMatches = _parameterRegex.Matches(expression);
            foreach (Match match in parameterMatches.Cast<Match>())
            {
                _ = parameters.Add(match.Groups[1].Value);
            }

            // Find function calls that might require specific context values
            MatchCollection functionMatches = _functionRegex.Matches(expression);
            foreach (Match match in functionMatches.Cast<Match>())
            {
                string functionName = match.Groups[1].Value;
                if (_functions.ContainsKey(functionName))
                {
                    // Add context parameters that might be needed
                    _ = parameters.Add("Amount");
                    _ = parameters.Add("NetAmount");
                    _ = parameters.Add("VatAmount");
                }
            }

            return parameters;
        }

        private string ReplaceParameters(string expression, TemplateContext context, CultureInfo cultureInfo)
        {
            return _parameterRegex.Replace(expression, match =>
            {
                string parameterName = match.Groups[1].Value;
                return GetParameterValue(parameterName, context, cultureInfo).ToString(cultureInfo);
            });
        }

        private string ReplaceFunctions(string expression, TemplateContext context, CultureInfo cultureInfo)
        {
            return _functionRegex.Replace(expression, match =>
            {
                string functionName = match.Groups[1].Value;
                if (_functions.TryGetValue(functionName, out Func<TemplateContext, decimal>? function))
                {
                    try
                    {
                        decimal result = function(context);
                        return result.ToString(cultureInfo);
                    }
                    catch
                    {
                        return "0";
                    }
                }
                return match.Value;
            });
        }

        private static decimal GetParameterValue(string parameterName, TemplateContext context, CultureInfo cultureInfo)
        {
            return parameterName.ToUpperInvariant() switch
            {
                "AMOUNT" => context.Amount,
                "NETAMOUNT" => context.NetAmount,
                "VATAMOUNT" => context.VatAmount,
                "COGS" => context.COGS,
                "IMPORTTAXAMOUNT" => context.ImportTaxAmount,
                "DISCOUNTAMOUNT" => context.DiscountAmount,

                // Vietnamese specific
                "GROSSREVENUE" => context.Amount,
                "NETREVENUE" => context.NetAmount,
                "TAXABLEAMOUNT" => context.NetAmount > 0 ? context.NetAmount : context.Amount,

                // Try to get from context parameters
                _ => context.GetParameter<decimal>(parameterName, 0)
            };
        }

        private decimal EvaluateArithmeticExpression(string expression, CultureInfo cultureInfo)
        {
            // Simple arithmetic evaluator for basic operations
            // In production, consider using a more robust expression parser library

            expression = expression.Trim();
            if (string.IsNullOrEmpty(expression) || expression == "0")
            {
                return 0;
            }

            // Handle parentheses recursively
            while (expression.Contains('('))
            {
                int start = expression.LastIndexOf('(');
                int end = expression.IndexOf(')', start);
                if (end == -1)
                {
                    throw new InvalidOperationException("Mismatched parentheses");
                }

                string innerExpression = expression.Substring(start + 1, end - start - 1);
                decimal innerResult = EvaluateArithmeticExpression(innerExpression, cultureInfo);
                expression = expression[..start] + innerResult.ToString(cultureInfo) + expression[(end + 1)..];
            }

            // Handle multiplication and division
            char[] operators = ['*', '/'];
            foreach (char op in operators)
            {
                string[] parts = expression.Split(op, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    decimal result = decimal.Parse(parts[0], cultureInfo);
                    for (int i = 1; i < parts.Length; i++)
                    {
                        decimal value = decimal.Parse(parts[i], cultureInfo);
                        result = op == '*' ? result * value : result / value;
                    }
                    return result;
                }
            }

            // Handle addition and subtraction
            operators = ['+', '-'];
            foreach (char op in operators)
            {
                string[] parts = expression.Split(op, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    decimal result = decimal.Parse(parts[0], cultureInfo);
                    for (int i = 1; i < parts.Length; i++)
                    {
                        decimal value = decimal.Parse(parts[i], cultureInfo);
                        result = op == '+' ? result + value : result - value;
                    }
                    return result;
                }
            }

            // Return the parsed value if no operators found
            return decimal.Parse(expression, cultureInfo);
        }

        [GeneratedRegex(@"\{(\w+)\}", RegexOptions.Compiled)]
        private static partial Regex MyRegex();
        [GeneratedRegex(@"(\w+)\s*\(\s*\)", RegexOptions.Compiled)]
        private static partial Regex MyRegex1();
    }
}
