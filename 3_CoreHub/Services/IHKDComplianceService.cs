using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service interface for HKD Compliance Validation - Phase 2.3.9
/// Implements Vietnamese Accounting Standard (Thông tư 152/2025/TT-BTC)
/// Compliance validation rules for 7 HKD book types
/// </summary>
public interface IHKDComplianceService
{
    /// <summary>
    /// Validate HKD book compliance per Thông tư 152/2025/TT-BTC
    /// </summary>
    Task<HKDComplianceResult> ValidateBookComplianceAsync(
        TenantId tenantId,
        AccountingBookType bookType,
        DateTime period,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate HKD transaction compliance
    /// </summary>
    Task<HKDTransactionComplianceResult> ValidateTransactionComplianceAsync(
        TenantId tenantId,
        AccountingBookType bookType,
        CoreAccountingEntry entry,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get compliance rules for HKD book type
    /// </summary>
    Task<List<ComplianceRule>> GetComplianceRulesAsync(
        AccountingBookType bookType,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if HKD book requires audit
    /// </summary>
    Task<bool> RequiresAuditAsync(
        TenantId tenantId,
        AccountingBookType bookType,
        DateTime period,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate compliance report for HKD book
    /// </summary>
    Task<HKDComplianceReport> GenerateComplianceReportAsync(
        TenantId tenantId,
        AccountingBookType bookType,
        DateTime period,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate multi-tenant data isolation compliance
    /// </summary>
    Task<MultiTenantComplianceResult> ValidateMultiTenantComplianceAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// HKD Compliance Result
/// </summary>
public record HKDComplianceResult
{
    public AccountingBookType BookType { get; init; }
    public DateTime Period { get; init; }
    public bool IsCompliant { get; init; }
    public ComplianceStatus Status { get; init; }
    public List<ComplianceViolation> Violations { get; init; } = new();
    public List<ComplianceWarning> Warnings { get; init; } = new();
    public decimal ComplianceScore { get; init; }
    public DateTime ValidationDate { get; init; }
    public string Validator { get; init; }
}

/// <summary>
/// HKD Transaction Compliance Result
/// </summary>
public record HKDTransactionComplianceResult
{
    public AccountingBookType BookType { get; init; }
    public Guid TransactionId { get; init; }
    public bool IsCompliant { get; init; }
    public List<TransactionViolation> Violations { get; init; } = new();
    public List<TransactionWarning> Warnings { get; init; } = new();
    public DateTime ValidationDate { get; init; }
}

/// <summary>
/// Compliance Rule
/// </summary>
public record ComplianceRule
{
    public string RuleId { get; init; }
    public string RuleName { get; init; }
    public string Description { get; init; }
    public AccountingBookType BookType { get; init; }
    public RuleType Type { get; init; }
    public SeverityLevel Severity { get; init; }
    public string ValidationLogic { get; init; }
    public bool IsMandatory { get; init; }
    public DateTime EffectiveDate { get; init; }
}

/// <summary>
/// Compliance Violation
/// </summary>
public record ComplianceViolation
{
    public string RuleId { get; init; }
    public string RuleName { get; init; }
    public string Description { get; init; }
    public string ActualValue { get; init; }
    public string ExpectedValue { get; init; }
    public SeverityLevel Severity { get; init; }
    public DateTime DetectedDate { get; init; }
    public string Recommendation { get; init; }
}


/// <summary>
/// Transaction Violation
/// </summary>
public record TransactionViolation
{
    public string RuleId { get; init; }
    public string RuleName { get; init; }
    public string Description { get; init; }
    public SeverityLevel Severity { get; init; }
    public string Recommendation { get; init; }
}

/// <summary>
/// Transaction Warning
/// </summary>
public record TransactionWarning
{
    public string RuleId { get; init; }
    public string RuleName { get; init; }
    public string Description { get; init; }
    public string Recommendation { get; init; }
}

/// <summary>
/// HKD Compliance Report
/// </summary>
public record HKDComplianceReport
{
    public AccountingBookType BookType { get; init; }
    public DateTime Period { get; init; }
    public ComplianceStatus OverallStatus { get; init; }
    public decimal ComplianceScore { get; init; }
    public List<ComplianceSummary> Summaries { get; init; } = new();
    public List<ComplianceTrend> Trends { get; init; } = new();
    public List<Recommendation> Recommendations { get; init; } = new();
    public DateTime ReportDate { get; init; }
    public string GeneratedBy { get; init; }
}

/// <summary>
/// Compliance Summary
/// </summary>
public record ComplianceSummary
{
    public string Category { get; init; }
    public int TotalRules { get; init; }
    public int PassedRules { get; init; }
    public int FailedRules { get; init; }
    public decimal Score { get; init; }
}

/// <summary>
/// Compliance Trend
/// </summary>
public record ComplianceTrend
{
    public DateTime Period { get; init; }
    public decimal Score { get; init; }
    public int Violations { get; init; }
    public int Warnings { get; init; }
}

/// <summary>
/// Recommendation
/// </summary>
public record Recommendation
{
    public string Type { get; init; }
    public string Priority { get; init; }
    public string Description { get; init; }
    public string Action { get; init; }
    public DateTime TargetDate { get; init; }
}

/// <summary>
/// Multi-tenant Compliance Result
/// </summary>
public record MultiTenantComplianceResult
{
    public TenantId TenantId { get; init; }
    public bool IsDataIsolated { get; init; }
    public bool HasCrossTenantAccess { get; init; }
    public List<DataIsolationViolation> Violations { get; init; } = new();
    public decimal IsolationScore { get; init; }
    public DateTime ValidationDate { get; init; }
}

/// <summary>
/// Data Isolation Violation
/// </summary>
public record DataIsolationViolation
{
    public string ViolationType { get; init; }
    public string Description { get; init; }
    public string AffectedEntity { get; init; }
    public SeverityLevel Severity { get; init; }
    public string Recommendation { get; init; }
}

/// <summary>
/// Compliance Status
/// </summary>
public enum ComplianceStatus
{
    Compliant = 1,
    PartiallyCompliant = 2,
    NonCompliant = 3,
    RequiresReview = 4,
    NotAssessed = 5
}

/// <summary>
/// Rule Type
/// </summary>
public enum RuleType
{
    DataIntegrity = 1,
    TaxCompliance = 2,
    ReportingFormat = 3,
    BusinessLogic = 4,
    Security = 5,
    MultiTenant = 6
}
