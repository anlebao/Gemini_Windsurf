using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service interface for HKD Tax Classification Logic - Phase 2.3.9
    /// Implements Vietnamese Accounting Standard (Thông tư 152/2025/TT-BTC)
    /// Tax classification for 7 HKD book types
    /// </summary>
    public interface IHKDTaxClassificationService
    {
        /// <summary>
        /// Classify tax obligations for HKD based on book type and transaction
        /// </summary>
        Task<HKDTaxClassification> ClassifyTaxAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            CoreAccountingEntry entry,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get applicable tax rates for HKD book type
        /// </summary>
        Task<List<VatRate>> GetApplicableVatRatesAsync(
            AccountingBookType bookType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculate tax obligations for HKD book type
        /// </summary>
        Task<HKDTaxCalculation> CalculateTaxAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            decimal revenueAmount,
            decimal expenseAmount = 0,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate tax compliance for HKD book type
        /// </summary>
        Task<HKDTaxComplianceResult> ValidateTaxComplianceAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            CoreAccountingEntry entry,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get tax reporting requirements for HKD book type
        /// </summary>
        Task<HKDTaxReportingRequirements> GetReportingRequirementsAsync(
            AccountingBookType bookType,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// HKD Tax Classification Result
    /// </summary>
    public record HKDTaxClassification
    {
        public AccountingBookType BookType { get; init; }
        public HKDGroup Group { get; init; }
        public VatRate VatRate { get; init; }
        public bool RequiresVatDeclaration { get; init; }
        public bool RequiresPersonalIncomeTax { get; init; }
        public bool RequiresSpecialTax { get; init; }
        public decimal TaxRate { get; init; }
        public string TaxClassification { get; init; }
        public List<string> ApplicableTaxes { get; init; } = [];
    }

    /// <summary>
    /// HKD Tax Calculation Result
    /// </summary>
    public record HKDTaxCalculation
    {
        public AccountingBookType BookType { get; init; }
        public decimal RevenueAmount { get; init; }
        public decimal ExpenseAmount { get; init; }
        public decimal VatAmount { get; init; }
        public decimal PersonalIncomeTaxAmount { get; init; }
        public decimal SpecialTaxAmount { get; init; }
        public decimal TotalTaxAmount { get; init; }
        public decimal NetAmount { get; init; }
        public List<TaxBreakdown> TaxBreakdowns { get; init; } = [];
    }

    /// <summary>
    /// Tax Breakdown Detail
    /// </summary>
    public record TaxBreakdown
    {
        public string TaxType { get; init; }
        public decimal TaxableAmount { get; init; }
        public decimal TaxRate { get; init; }
        public decimal TaxAmount { get; init; }
        public string Description { get; init; }
    }

    /// <summary>
    /// HKD Tax Compliance Result
    /// </summary>
    public record HKDTaxComplianceResult
    {
        public AccountingBookType BookType { get; init; }
        public bool IsCompliant { get; init; }
        public List<ComplianceIssue> Issues { get; init; } = [];
        public List<ComplianceWarning> Warnings { get; init; } = [];
        public string ComplianceStatus { get; init; }
        public DateTime ValidationDate { get; init; }
    }

    /// <summary>
    /// Compliance Issue
    /// </summary>
    public record ComplianceIssue
    {
        public string IssueType { get; init; }
        public string Description { get; init; }
        public string Recommendation { get; init; }
        public SeverityLevel Severity { get; init; }
    }

    /// <summary>
    /// Compliance Warning
    /// </summary>
    public record ComplianceWarning
    {
        public string WarningType { get; init; }
        public string Description { get; init; }
        public string Recommendation { get; init; }
    }

    /// <summary>
    /// HKD Tax Reporting Requirements
    /// </summary>
    public record HKDTaxReportingRequirements
    {
        public AccountingBookType BookType { get; init; }
        public List<ReportingRequirement> Requirements { get; init; } = [];
        public List<string> RequiredDocuments { get; init; } = [];
        public List<string> ReportFormats { get; init; } = [];
        public DateTime ReportingDeadline { get; init; }
        public string ReportingFrequency { get; init; }
    }

    /// <summary>
    /// Reporting Requirement
    /// </summary>
    public record ReportingRequirement
    {
        public string RequirementType { get; init; }
        public string Description { get; init; }
        public string Format { get; init; }
        public DateTime Deadline { get; init; }
        public bool IsMandatory { get; init; }
    }

    /// <summary>
    /// Severity Level for Compliance Issues
    /// </summary>
    public enum SeverityLevel
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}
