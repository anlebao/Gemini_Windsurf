using System.Globalization;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Enhanced Formula Engine with Expression Parser foundation
    /// Supports Vietnamese accounting formulas and calculations
    /// </summary>
    public class FormulaEngine(ILogger<FormulaEngine> logger, IExpressionParser expressionParser)
    {
        private readonly ILogger<FormulaEngine> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IExpressionParser _expressionParser = expressionParser ?? throw new ArgumentNullException(nameof(expressionParser));

        /// <summary>
        /// Evaluate a formula expression with the given context
        /// </summary>
        public decimal EvaluateFormula(string formula, TemplateContext context, CultureInfo? cultureInfo = null)
        {
            if (string.IsNullOrWhiteSpace(formula))
            {
                _logger.LogWarning("Empty formula provided");
                return 0;
            }

            if (context == null)
            {
                _logger.LogError("TemplateContext is null for formula evaluation");
                throw new ArgumentNullException(nameof(context));
            }

            try
            {
                cultureInfo ??= CultureInfo.InvariantCulture;

                _logger.LogDebug("Evaluating formula: {Formula}", formula);

                // Check if the expression can be parsed
                if (!_expressionParser.CanParse(formula))
                {
                    _logger.LogWarning("Formula cannot be parsed: {Formula}", formula);
                    return 0;
                }

                // Evaluate the formula
                decimal result = _expressionParser.Evaluate(formula, context, cultureInfo);

                _logger.LogDebug("Formula evaluation result: {Formula} = {Result}", formula, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating formula: {Formula}", formula);
                throw new InvalidOperationException($"Failed to evaluate formula '{formula}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get required parameters for a formula
        /// </summary>
        public IEnumerable<string> GetRequiredParameters(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
            {
                return Array.Empty<string>();
            }

            try
            {
                return _expressionParser.GetRequiredParameters(formula);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting required parameters for formula: {Formula}", formula);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Validate if a formula is syntactically correct
        /// </summary>
        public bool ValidateFormula(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
            {
                return false;
            }

            try
            {
                return _expressionParser.CanParse(formula);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating formula: {Formula}", formula);
                return false;
            }
        }

        /// <summary>
        /// Predefined Vietnamese accounting formulas
        /// </summary>
        public static class VietnameseAccountingFormulas
        {
            // Revenue calculations
            public const string GrossRevenue = "Amount";
            public const string NetRevenue = "NetAmount";
            public const string TaxableRevenue = "TaxableAmount";

            // Tax calculations
            public const string VATAmount = "VatAmount";
            public const string ImportTaxAmount = "ImportTaxAmount";

            // Cost calculations
            public const string COGS = "COGS";
            public const string GrossProfit = "NetAmount - COGS";
            public const string GrossMargin = "(NetAmount - COGS) / NetAmount * 100";

            // Discount calculations
            public const string DiscountAmount = "DiscountAmount";
            public const string DiscountedRevenue = "Amount - DiscountAmount";

            // Common Vietnamese accounting formulas
            public const string DoanhThuGop = "Amount"; // Gross Revenue
            public const string DoanhThuThuan = "NetAmount"; // Net Revenue
            public const string GiaVonHangBan = "COGS"; // Cost of Goods Sold
            public const string LoiNhuanGop = "NetAmount - COGS"; // Gross Profit
            public const string TySuatLoiNhuanGop = "(NetAmount - COGS) / NetAmount * 100"; // Gross Profit Margin

            // Tax formulas for Vietnamese accounting
            public const string TienThueGTGT = "VatAmount"; // VAT Amount
            public const string TienThueNhapKhau = "ImportTaxAmount"; // Import Tax Amount
        }

        /// <summary>
        /// Get all available predefined formulas
        /// </summary>
        public static Dictionary<string, string> GetPredefinedFormulas()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Revenue formulas
                ["GrossRevenue"] = VietnameseAccountingFormulas.GrossRevenue,
                ["NetRevenue"] = VietnameseAccountingFormulas.NetRevenue,
                ["TaxableRevenue"] = VietnameseAccountingFormulas.TaxableRevenue,
                ["DoanhThuGop"] = VietnameseAccountingFormulas.DoanhThuGop,
                ["DoanhThuThuan"] = VietnameseAccountingFormulas.DoanhThuThuan,

                // Cost formulas
                ["COGS"] = VietnameseAccountingFormulas.COGS,
                ["GiaVonHangBan"] = VietnameseAccountingFormulas.GiaVonHangBan,
                ["GrossProfit"] = VietnameseAccountingFormulas.GrossProfit,
                ["LoiNhuanGop"] = VietnameseAccountingFormulas.LoiNhuanGop,

                // Margin formulas
                ["GrossMargin"] = VietnameseAccountingFormulas.GrossMargin,
                ["TySuatLoiNhuanGop"] = VietnameseAccountingFormulas.TySuatLoiNhuanGop,

                // Tax formulas
                ["VATAmount"] = VietnameseAccountingFormulas.VATAmount,
                ["ImportTaxAmount"] = VietnameseAccountingFormulas.ImportTaxAmount,
                ["TienThueGTGT"] = VietnameseAccountingFormulas.TienThueGTGT,
                ["TienThueNhapKhau"] = VietnameseAccountingFormulas.TienThueNhapKhau,

                // Discount formulas
                ["DiscountAmount"] = VietnameseAccountingFormulas.DiscountAmount,
                ["DiscountedRevenue"] = VietnameseAccountingFormulas.DiscountedRevenue,
            };
        }
    }
}
