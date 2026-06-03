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
        protected IFormulaEngine FormulaEngine { get; private set; }
        protected IDataProvider DataProvider { get; private set; }
        protected ILogger<BaseHKDBookTemplate> Logger { get; private set; }

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

            GenericHKDBook book = new()
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
            TemplateCalculationEngine calculationEngine = new(FormulaEngine, DataProvider, null!); // Logger will be created internally

            try
            {
                // Calculate field values
                Dictionary<string, decimal> fieldValues = await calculationEngine.CalculateFieldsAsync(this, book.TenantId, book.Period);

                // Add field values to book
                foreach (KeyValuePair<string, decimal> kvp in fieldValues)
                {
                    book.NumericValues[kvp.Key] = kvp.Value;
                }

                // Calculate calculation values
                Dictionary<string, decimal> calculationValues = await calculationEngine.CalculateCalculationsAsync(
                    this, fieldValues, book.TenantId, book.Period);

                // Add calculation values to book
                foreach (KeyValuePair<string, decimal> kvp in calculationValues)
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
            List<string> errors = [];

            // Check required fields
            foreach (TemplateField? field in Fields.Where(f => f.IsRequired))
            {
                if (!book.NumericValues.ContainsKey(field.FieldName))
                {
                    errors.Add($"Required field {field.FieldName} is missing");
                }
            }

            // Check validation rules
            foreach (TemplateValidationRule rule in ValidationRules)
            {
                try
                {
                    bool isValid = await ValidateRuleAsync(rule, book);
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
                string errorMessage = $"HKD book {TemplateCode} validation failed: {string.Join(", ", errors)}";
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
                    Dictionary<string, decimal> variables = new(book.NumericValues);
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
            TemplateCalculationEngine calculationEngine = new(FormulaEngine, DataProvider, null!); // Logger will be created internally
            return await calculationEngine.ValidateTemplateAsync(this);
        }
    }
}
