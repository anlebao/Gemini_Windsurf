using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Formula;
using VanAn.CoreHub.Services.Data;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Template
{
    /// <summary>
    /// Template Calculation Engine - Uses FormulaEngine to calculate template values
    /// Bridges the gap between templates and the formula engine
    /// </summary>
    public class TemplateCalculationEngine(
        IFormulaEngine formulaEngine,
        IDataProvider dataProvider,
        ILogger<TemplateCalculationEngine> logger)
    {
        private readonly IFormulaEngine _formulaEngine = formulaEngine;
        private readonly IDataProvider _dataProvider = dataProvider;
        private readonly ILogger<TemplateCalculationEngine> _logger = logger;

        /// <summary>
        /// Calculate all field values for a template
        /// </summary>
        public async Task<Dictionary<string, decimal>> CalculateFieldsAsync(
            HKDBookTemplate template,
            TenantId tenantId,
            AccountingPeriod period)
        {
            Dictionary<string, decimal> results = [];
            DataProviderContext context = new(tenantId, period);

            // Create base variables with tenant and period context
            Dictionary<string, decimal> variables = CreateBaseVariables(tenantId, period);

            _logger.LogInformation("Calculating {FieldCount} fields for template {TemplateCode}",
                template.Fields.Count, template.TemplateCode);

            // Calculate each field
            foreach (TemplateField field in template.Fields)
            {
                if (string.IsNullOrEmpty(field.Formula))
                {
                    if (field.DefaultValue.HasValue)
                    {
                        results[field.FieldName] = field.DefaultValue.Value;
                        _logger.LogDebug("Field {FieldName} using default value: {Value}",
                            field.FieldName, field.DefaultValue.Value);
                    }
                    continue;
                }

                try
                {
                    decimal value = await CalculateFormulaAsync(field.Formula, variables, context);
                    results[field.FieldName] = value;

                    _logger.LogDebug("Field {FieldName} calculated: {Value}", field.FieldName, value);

                    // Add calculated value to variables for dependent calculations
                    variables[field.FieldName] = value;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating field {FieldName} with formula {Formula}",
                        field.FieldName, field.Formula);

                    if (field.IsRequired)
                    {
                        throw new InvalidOperationException($"Required field {field.FieldName} calculation failed", ex);
                    }

                    // Use default value for optional fields
                    if (field.DefaultValue.HasValue)
                    {
                        results[field.FieldName] = field.DefaultValue.Value;
                    }
                }
            }

            _logger.LogInformation("Template {TemplateCode} field calculation completed: {SuccessCount}/{TotalCount} fields",
                template.TemplateCode, results.Count, template.Fields.Count);

            return results;
        }

        /// <summary>
        /// Calculate all calculation values for a template
        /// </summary>
        public async Task<Dictionary<string, decimal>> CalculateCalculationsAsync(
            HKDBookTemplate template,
            Dictionary<string, decimal> fieldValues,
            TenantId tenantId,
            AccountingPeriod period)
        {
            Dictionary<string, decimal> results = [];
            DataProviderContext context = new(tenantId, period);

            // Start with field values as variables
            Dictionary<string, decimal> variables = new(fieldValues);

            // Add base variables
            foreach (KeyValuePair<string, decimal> kvp in CreateBaseVariables(tenantId, period))
            {
                variables[kvp.Key] = kvp.Value;
            }

            _logger.LogInformation("Calculating {CalculationCount} calculations for template {TemplateCode}",
                template.Calculations.Count, template.TemplateCode);

            // Calculate each calculation in order
            IOrderedEnumerable<TemplateCalculation> orderedCalculations = template.Calculations
                .OrderBy(c => c.Order)
                .ThenBy(c => c.CalculationName);

            foreach (TemplateCalculation? calculation in orderedCalculations)
            {
                if (string.IsNullOrEmpty(calculation.Formula))
                {
                    continue;
                }

                try
                {
                    decimal value = await CalculateFormulaAsync(calculation.Formula, variables, context);
                    results[calculation.CalculationName] = value;

                    _logger.LogDebug("Calculation {CalculationName} calculated: {Value}",
                        calculation.CalculationName, value);

                    // Add calculated value to variables for dependent calculations
                    variables[calculation.CalculationName] = value;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating calculation {CalculationName} with formula {Formula}",
                        calculation.CalculationName, calculation.Formula);
                    throw new InvalidOperationException($"Calculation {calculation.CalculationName} failed", ex);
                }
            }

            _logger.LogInformation("Template {TemplateCode} calculation completed: {SuccessCount}/{TotalCount} calculations",
                template.TemplateCode, results.Count, template.Calculations.Count);

            return results;
        }

        private async Task<decimal> CalculateFormulaAsync(
            string formula,
            Dictionary<string, decimal> variables,
            DataProviderContext context)
        {
            // Validate formula first
            if (!_formulaEngine.ValidateFormula(formula))
            {
                throw new InvalidOperationException($"Invalid formula: {formula}");
            }

            // Evaluate formula
            decimal result = _formulaEngine.Evaluate(formula, variables);

            return await Task.FromResult(result);
        }

        private static Dictionary<string, decimal> CreateBaseVariables(TenantId tenantId, AccountingPeriod period)
        {
            return new Dictionary<string, decimal>
            {
                ["_TenantId"] = decimal.Parse(tenantId.Value.ToString("N")),
                ["_PeriodYear"] = period.Year,
                ["_PeriodMonth"] = period.Month,
                ["_PeriodQuarter"] = (period.Month + 2) / 3,
                ["_PeriodDays"] = DateTime.DaysInMonth(period.Year, period.Month)
            };
        }

        /// <summary>
        /// Validate template formulas
        /// </summary>
        public async Task<List<string>> ValidateTemplateAsync(HKDBookTemplate template)
        {
            List<string> errors = [];
            Dictionary<string, decimal> variables = CreateBaseVariables(new TenantId(Guid.NewGuid()), AccountingPeriod.Create(2026, 1));

            // Validate field formulas
            foreach (TemplateField field in template.Fields)
            {
                if (!string.IsNullOrEmpty(field.Formula))
                {
                    try
                    {
                        if (!_formulaEngine.ValidateFormula(field.Formula))
                        {
                            errors.Add($"Field {field.FieldName}: Invalid formula syntax");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Field {field.FieldName}: {ex.Message}");
                    }
                }
            }

            // Validate calculation formulas
            foreach (TemplateCalculation calculation in template.Calculations)
            {
                if (!string.IsNullOrEmpty(calculation.Formula))
                {
                    try
                    {
                        if (!_formulaEngine.ValidateFormula(calculation.Formula))
                        {
                            errors.Add($"Calculation {calculation.CalculationName}: Invalid formula syntax");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Calculation {calculation.CalculationName}: {ex.Message}");
                    }
                }
            }

            return await Task.FromResult(errors);
        }
    }
}
