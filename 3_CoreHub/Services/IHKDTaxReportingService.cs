using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service interface for HKD Tax Reporting Formats - Phase 2.3.9
    /// Implements Vietnamese Accounting Standard (Thông tư 152/2025/TT-BTC)
    /// Tax reporting formats for 7 HKD book types
    /// </summary>
    public interface IHKDTaxReportingService
    {
        /// <summary>
        /// Generate tax report for HKD book type
        /// </summary>
        Task<HKDTaxReport> GenerateTaxReportAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            DateTime period,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get available report formats for HKD book type
        /// </summary>
        Task<List<ReportFormat>> GetReportFormatsAsync(
            AccountingBookType bookType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Export tax report to specified format
        /// </summary>
        Task<byte[]> ExportTaxReportAsync(
            HKDTaxReport report,
            ReportFormat format,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate report format compliance
        /// </summary>
        Task<ReportFormatValidationResult> ValidateReportFormatAsync(
            HKDTaxReport report,
            ReportFormat format,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get report template for HKD book type
        /// </summary>
        Task<ReportTemplate> GetReportTemplateAsync(
            AccountingBookType bookType,
            ReportFormat format,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate quarterly tax summary
        /// </summary>
        Task<HKDQuarterlyTaxSummary> GenerateQuarterlyTaxSummaryAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            int year,
            int quarter,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate annual tax summary
        /// </summary>
        Task<HKDAnnualTaxSummary> GenerateAnnualTaxSummaryAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            int year,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// HKD Tax Report
    /// </summary>
    public record HKDTaxReport
    {
        public AccountingBookType BookType { get; init; }
        public TenantId TenantId { get; init; }
        public DateTime Period { get; init; }
        public string ReportType { get; init; }
        public string ReportTitle { get; init; }
        public bool IsTaxExempt { get; init; }
        public decimal VATAmount { get; init; }
        public decimal PersonalIncomeTaxAmount { get; init; }
        public decimal VATRate { get; init; }
        public decimal TotalRevenue { get; init; }
        public decimal TotalExpenses { get; init; }
        public decimal NetIncome { get; init; }
        public decimal CashInflows { get; init; }
        public decimal CashOutflows { get; init; }
        public decimal NetCashFlow { get; init; }
        public decimal MaterialCosts { get; init; }
        public decimal SpecialTaxAmount { get; init; }
        public List<string> SpecialTaxCategories { get; init; } = [];
        public List<string> TaxAuthorityReferences { get; init; } = [];
        public List<string> RevenueCategories { get; init; } = [];
        public List<string> ProductCategories { get; init; } = [];
        public List<TransactionRecord> RevenueBreakdown { get; init; } = [];
        public List<TransactionRecord> ExpenseBreakdown { get; init; } = [];
        public List<TransactionRecord> InventoryBreakdown { get; init; } = [];
        public List<TransactionRecord> PaymentMethodBreakdown { get; init; } = [];
        public ReportData Data { get; init; }
        public ReportMetadata Metadata { get; init; }
        public DateTime GeneratedDate { get; init; }
        public string GeneratedBy { get; init; }
    }

    /// <summary>
    /// Report Data
    /// </summary>
    public record ReportData
    {
        public decimal TotalRevenue { get; init; }
        public decimal TotalExpense { get; init; }
        public decimal VatAmount { get; init; }
        public decimal PersonalIncomeTaxAmount { get; init; }
        public decimal SpecialTaxAmount { get; init; }
        public decimal NetAmount { get; init; }
        public List<TransactionRecord> Transactions { get; init; } = [];
        public List<TaxBreakdownRecord> TaxBreakdowns { get; init; } = [];
        public Dictionary<string, decimal> AccountSummaries { get; init; } = [];
    }

    /// <summary>
    /// Transaction Record
    /// </summary>
    public record TransactionRecord
    {
        public DateTime TransactionDate { get; init; }
        public string Description { get; init; }
        public decimal Amount { get; init; }
        public string AccountNumber { get; init; }
        public string TransactionType { get; init; }
        public string Reference { get; init; }
    }

    /// <summary>
    /// Tax Breakdown Record
    /// </summary>
    public record TaxBreakdownRecord
    {
        public string TaxType { get; init; }
        public decimal TaxableAmount { get; init; }
        public decimal TaxRate { get; init; }
        public decimal TaxAmount { get; init; }
        public string Description { get; init; }
    }

    /// <summary>
    /// Report Metadata
    /// </summary>
    public record ReportMetadata
    {
        public string ReportVersion { get; init; }
        public string TemplateVersion { get; init; }
        public List<string> RequiredFields { get; init; } = [];
        public List<ValidationRule> ValidationRules { get; init; } = [];
        public Dictionary<string, string> Formulas { get; init; } = [];
    }

    /// <summary>
    /// Validation Rule
    /// </summary>
    public record ValidationRule
    {
        public string RuleName { get; init; }
        public string Expression { get; init; }
        public string ErrorMessage { get; init; }
        public SeverityLevel Severity { get; init; }
    }

    /// <summary>
    /// Report Format
    /// </summary>
    public record ReportFormat
    {
        public string FormatId { get; init; }
        public string FormatName { get; init; }
        public string FormatType { get; init; }
        public string Extension { get; init; }
        public string MimeType { get; init; }
        public bool IsStandard { get; init; }
        public List<string> SupportedBookTypes { get; init; } = [];
        public Dictionary<string, string> FormatOptions { get; init; } = [];
    }

    /// <summary>
    /// Report Format Validation Result
    /// </summary>
    public record ReportFormatValidationResult
    {
        public ReportFormat Format { get; init; }
        public bool IsValid { get; init; }
        public List<FormatValidationError> Errors { get; init; } = [];
        public List<FormatValidationError> ValidationErrors { get; init; } = [];
        public List<FormatValidationWarning> Warnings { get; init; } = [];
        public DateTime ValidationDate { get; init; }
    }

    /// <summary>
    /// Format Validation Error
    /// </summary>
    public record FormatValidationError
    {
        public string FieldName { get; init; }
        public string ErrorType { get; init; }
        public string Description { get; init; }
        public string ExpectedValue { get; init; }
        public string ActualValue { get; init; }
    }

    /// <summary>
    /// Format Validation Warning
    /// </summary>
    public record FormatValidationWarning
    {
        public string FieldName { get; init; }
        public string WarningType { get; init; }
        public string Description { get; init; }
        public string Recommendation { get; init; }
    }

    /// <summary>
    /// Report Template
    /// </summary>
    public record ReportTemplate
    {
        public AccountingBookType BookType { get; init; }
        public ReportFormat Format { get; init; }
        public string TemplateName { get; init; }
        public string TemplateContent { get; init; }
        public string TemplateStructure { get; init; }
        public List<TemplateSection> TemplateSections { get; init; } = [];
        public List<TaxReportField> Fields { get; init; } = [];
        public List<TemplateFormula> Formulas { get; init; } = [];
        public Dictionary<string, object> DefaultValues { get; init; } = [];
    }

    /// <summary>
    /// Tax Report Field
    /// </summary>
    public record TaxReportField
    {
        public string FieldName { get; init; }
        public string DisplayName { get; init; }
        public string DataType { get; init; }
        public bool IsRequired { get; init; }
        public string Format { get; init; }
        public string ValidationRule { get; init; }
        public object DefaultValue { get; init; }
    }

    /// <summary>
    /// Template Formula
    /// </summary>
    public record TemplateFormula
    {
        public string FormulaName { get; init; }
        public string Expression { get; init; }
        public string Description { get; init; }
        public List<string> Dependencies { get; init; } = [];
    }

    /// <summary>
    /// HKD Quarterly Tax Summary
    /// </summary>
    public record HKDQuarterlyTaxSummary
    {
        public AccountingBookType BookType { get; init; }
        public TenantId TenantId { get; init; }
        public int Year { get; init; }
        public int Quarter { get; init; }
        public decimal QuarterlyRevenue { get; init; }
        public decimal QuarterlyExpense { get; init; }
        public decimal QuarterlyVat { get; init; }
        public decimal QuarterlyPersonalIncomeTax { get; init; }
        public decimal QuarterlySpecialTax { get; init; }
        public List<MonthlySummary> MonthlySummaries { get; init; } = [];
        public DateTime GeneratedDate { get; init; }
    }

    /// <summary>
    /// Monthly Summary
    /// </summary>
    public record MonthlySummary
    {
        public int Month { get; init; }
        public decimal Revenue { get; init; }
        public decimal Expense { get; init; }
        public decimal Vat { get; init; }
        public decimal PersonalIncomeTax { get; init; }
        public decimal SpecialTax { get; init; }
    }

    /// <summary>
    /// HKD Annual Tax Summary
    /// </summary>
    public record HKDAnnualTaxSummary
    {
        public AccountingBookType BookType { get; init; }
        public TenantId TenantId { get; init; }
        public int Year { get; init; }
        public decimal AnnualRevenue { get; init; }
        public decimal AnnualExpense { get; init; }
        public decimal AnnualVat { get; init; }
        public decimal AnnualPersonalIncomeTax { get; init; }
        public decimal AnnualSpecialTax { get; init; }
        public List<QuarterlySummary> QuarterlySummaries { get; init; } = [];
        public TaxYearComparison YearComparison { get; init; }
        public DateTime GeneratedDate { get; init; }
    }

    /// <summary>
    /// Quarterly Summary
    /// </summary>
    public record QuarterlySummary
    {
        public int Quarter { get; init; }
        public decimal Revenue { get; init; }
        public decimal Expense { get; init; }
        public decimal Vat { get; init; }
        public decimal PersonalIncomeTax { get; init; }
        public decimal SpecialTax { get; init; }
    }

    /// <summary>
    /// Tax Year Comparison
    /// </summary>
    public record TaxYearComparison
    {
        public decimal PreviousYearRevenue { get; init; }
        public decimal PreviousYearExpense { get; init; }
        public decimal PreviousYearVat { get; init; }
        public decimal PreviousYearPersonalIncomeTax { get; init; }
        public decimal PreviousYearSpecialTax { get; init; }
        public decimal RevenueGrowthRate { get; init; }
        public decimal ExpenseGrowthRate { get; init; }
        public decimal TaxEfficiency { get; init; }
    }

    /// <summary>
    /// Template Section
    /// </summary>
    public record TemplateSection
    {
        public string SectionName { get; init; }
        public string SectionType { get; init; }
        public List<string> Fields { get; init; } = [];
        public Dictionary<string, object> Properties { get; init; } = [];
    }
}
