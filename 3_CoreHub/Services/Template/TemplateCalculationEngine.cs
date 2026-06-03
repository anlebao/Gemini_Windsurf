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
    public class TemplateCalculationEngine
    {
        private readonly IFormulaEngine _formulaEngine;
        private readonly IDataProvider _dataProvider;
        private readonly ILogger<TemplateCalculationEngine> _logger;
        
        public TemplateCalculationEngine(
            IFormulaEngine formulaEngine,
            IDataProvider dataProvider,
            ILogger<TemplateCalculationEngine> logger)
        {
            _formulaEngine = formulaEngine;
            _dataProvider = dataProvider;
            _logger = logger;
        }
        
        /// <summary>
        /// Calculate all field values for a template
        /// </summary>
        public async Task<Dictionary<string, decimal>> CalculateFieldsAsync(
            HKDBookTemplate template,
            TenantId tenantId,
            AccountingPeriod period)
        {
            var results = new Dictionary<string, decimal>();
            var context = new DataProviderContext(tenantId, period);
            
            // Create base variables with tenant and period context
            var variables = CreateBaseVariables(tenantId, period);
            
            _logger.LogInformation("Calculating {FieldCount} fields for template {TemplateCode}", 
                template.Fields.Count, template.TemplateCode);
            
            // Calculate each field
            foreach (var field in template.Fields)
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
                    var value = await CalculateFormulaAsync(field.Formula, variables, context);
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
            var results = new Dictionary<string, decimal>();
            var context = new DataProviderContext(tenantId, period);
            
            // Start with field values as variables
            var variables = new Dictionary<string, decimal>(fieldValues);
            
            // Add base variables
            foreach (var kvp in CreateBaseVariables(tenantId, period))
            {
                variables[kvp.Key] = kvp.Value;
            }
            
            _logger.LogInformation("Calculating {CalculationCount} calculations for template {TemplateCode}", 
                template.Calculations.Count, template.TemplateCode);
            
            // Calculate each calculation in order
            var orderedCalculations = template.Calculations
                .OrderBy(c => c.Order)
                .ThenBy(c => c.CalculationName);
            
            foreach (var calculation in orderedCalculations)
            {
                if (string.IsNullOrEmpty(calculation.Formula))
                {
                    continue;
                }
                
                try
                {
                    var value = await CalculateFormulaAsync(calculation.Formula, variables, context);
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
            var result = _formulaEngine.Evaluate(formula, variables);
            
            return await Task.FromResult(result);
        }
        
        private Dictionary<string, decimal> CreateBaseVariables(TenantId tenantId, AccountingPeriod period)
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
            var errors = new List<string>();
            var variables = CreateBaseVariables(new TenantId(Guid.NewGuid()), AccountingPeriod.Create(2026, 1));
            
            // Validate field formulas
            foreach (var field in template.Fields)
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
            foreach (var calculation in template.Calculations)
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
