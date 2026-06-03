namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Base template for HKD Book generation
    /// </summary>
    public abstract record HKDBookTemplate
    {
        public string TemplateCode { get; init; }
        public string TemplateName { get; init; }
        public string TemplateVersion { get; init; }
        public HKDGroup TargetGroup { get; init; }
        public List<TemplateField> Fields { get; init; } = new();
        public List<TemplateCalculation> Calculations { get; init; } = new();
        public List<TemplateValidationRule> ValidationRules { get; init; } = new();
        
        public abstract Task<GenericHKDBook> CreateBookAsync(
            TenantId tenantId, 
            AccountingPeriod period, 
            List<JournalEntry> entries);
        
        public abstract Task CalculateAsync(GenericHKDBook book);
        public abstract Task ValidateAsync(GenericHKDBook book);
        public abstract Task<string> GenerateReportAsync(GenericHKDBook book);
    }
    
    /// <summary>
    /// Template field definition
    /// </summary>
    public record TemplateField
    {
        public string FieldName { get; init; }
        public string DisplayName { get; init; }
        public FieldType Type { get; init; }
        public bool IsRequired { get; init; }
        public decimal? DefaultValue { get; init; }
        public string Formula { get; init; } = string.Empty;
        public string ValidationRule { get; init; } = string.Empty;
    }
    
    /// <summary>
    /// Template calculation definition
    /// </summary>
    public record TemplateCalculation
    {
        public string CalculationName { get; init; }
        public string Formula { get; init; }
        public List<string> Dependencies { get; init; } = new();
        public CalculationOrder Order { get; init; }
    }
    
        
    /// <summary>
    /// Field types for templates
    /// </summary>
    public enum FieldType
    {
        String,
        Decimal,
        Date,
        Boolean
    }
    
    /// <summary>
    /// Calculation order
    /// </summary>
    public enum CalculationOrder
    {
        BeforeData,
        AfterData,
        Final
    }
    
    }
