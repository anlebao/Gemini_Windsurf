namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Base template for HKD Book generation
    /// </summary>
    public abstract record HKDBookTemplate
    {
        public string TemplateCode { get; init; } = null!;
        public string TemplateName { get; init; } = null!;
        public string TemplateVersion { get; init; } = null!;
        public HKDGroup TargetGroup { get; init; }
        public List<TemplateField> Fields { get; init; } = [];
        public List<TemplateCalculation> Calculations { get; init; } = [];
        public List<TemplateValidationRule> ValidationRules { get; init; } = [];

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
        public string FieldName { get; init; } = null!;
        public string DisplayName { get; init; } = null!;
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
        public string CalculationName { get; init; } = null!;
        public string Formula { get; init; } = null!;
        public List<string> Dependencies { get; init; } = [];
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
