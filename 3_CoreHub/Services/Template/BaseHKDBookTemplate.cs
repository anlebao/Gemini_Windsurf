using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Formula;
using VanAn.CoreHub.Services.Data;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Template
{
    /// <summary>
    /// Base implementation for HKD Book templates with actual calculation logic
    /// </summary>
    public abstract record BaseHKDBookTemplate : HKDBookTemplate
    {
        protected readonly IFormulaEngine FormulaEngine;
        protected readonly IDataProvider DataProvider;
        protected readonly ILogger<BaseHKDBookTemplate> Logger;
        
        protected BaseHKDBookTemplate(
            IFormulaEngine formulaEngine,
            IDataProvider dataProvider,
            ILogger<BaseHKDBookTemplate> logger)
        {
            FormulaEngine = formulaEngine;
            DataProvider = dataProvider;
            Logger = logger;
        }
        
        public override async Task<GenericHKDBook> CreateBookAsync(
            TenantId tenantId, 
            AccountingPeriod period, 
            List<JournalEntry> entries)
        {
            Logger.LogInformation("Creating HKD book {TemplateCode} for tenant {TenantId}, period {Period}", 
                TemplateCode, tenantId.Value, period);
            
            var book = new GenericHKDBook
            {
                TenantId = tenantId,
                Period = period,
                BookTypeCode = TemplateCode,
                Template = this,
                Entries = entries
            };
            
            // Calculate all values using the template calculation engine
            await CalculateAsync(book);
            
            // Validate the calculated values
            await ValidateAsync(book);
            
            Logger.LogInformation("HKD book {TemplateCode} created successfully with {ValueCount} calculated values", 
                TemplateCode, book.NumericValues.Count);
            
            return book;
        }
        
        public override async Task CalculateAsync(GenericHKDBook book)
        {
            var calculationEngine = new TemplateCalculationEngine(FormulaEngine, DataProvider, null!); // Logger will be created internally
            
            try
            {
                // Calculate field values
                var fieldValues = await calculationEngine.CalculateFieldsAsync(this, book.TenantId, book.Period);
                
                // Add field values to book
                foreach (var kvp in fieldValues)
                {
                    book.NumericValues[kvp.Key] = kvp.Value;
                }
                
                // Calculate calculation values
                var calculationValues = await calculationEngine.CalculateCalculationsAsync(
                    this, fieldValues, book.TenantId, book.Period);
                
                // Add calculation values to book
                foreach (var kvp in calculationValues)
                {
                    book.NumericValues[kvp.Key] = kvp.Value;
                }
                
                Logger.LogDebug("HKD book {TemplateCode} calculation completed: {FieldCount} fields, {CalculationCount} calculations", 
                    TemplateCode, fieldValues.Count, calculationValues.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error calculating HKD book {TemplateCode}", TemplateCode);
                throw;
            }
        }
        
        public override async Task ValidateAsync(GenericHKDBook book)
        {
            var errors = new List<string>();
            
            // Check required fields
            foreach (var field in Fields.Where(f => f.IsRequired))
            {
                if (!book.NumericValues.ContainsKey(field.FieldName))
                {
                    errors.Add($"Required field {field.FieldName} is missing");
                }
            }
            
            // Check validation rules
            foreach (var rule in ValidationRules)
            {
                try
                {
                    var isValid = await ValidateRuleAsync(rule, book);
                    if (!isValid)
                    {
                        errors.Add($"Validation rule failed: {rule.Rule}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Error validating rule {Rule}", rule.Rule);
                    errors.Add($"Validation rule error: {rule.Rule} - {ex.Message}");
                }
            }
            
            if (errors.Count > 0)
            {
                var errorMessage = $"HKD book {TemplateCode} validation failed: {string.Join(", ", errors)}";
                Logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            
            Logger.LogInformation("HKD book {TemplateCode} validation passed", TemplateCode);
            await Task.CompletedTask;
        }
        
        private async Task<bool> ValidateRuleAsync(TemplateValidationRule rule, GenericHKDBook book)
        {
            // Simple validation implementation - can be extended
            // For now, just check if the rule is a valid formula
            if (!string.IsNullOrEmpty(rule.Rule))
            {
                try
                {
                    var variables = new Dictionary<string, decimal>(book.NumericValues);
                    return FormulaEngine.ValidateFormula(rule.Rule);
                }
                catch
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Validate template formulas
        /// </summary>
        public async Task<List<string>> ValidateTemplateAsync()
        {
            var calculationEngine = new TemplateCalculationEngine(FormulaEngine, DataProvider, null!); // Logger will be created internally
            return await calculationEngine.ValidateTemplateAsync(this);
        }
    }
}
