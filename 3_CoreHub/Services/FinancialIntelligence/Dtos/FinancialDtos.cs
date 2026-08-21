using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence.Dtos
{
    // ========================================================================
    // VA-FI-MVP2 Phase 3 (2026-08-21): DTOs for FinancialIntelligenceController endpoints.
    // camelCase JSON (default System.Text.Json policy in Gateway).
    // Records → DTOs: trim Domain value objects (TenantId, AccountingPeriod, FinancialModelVersion)
    // to primitive serializable types. Enum values serialize as strings via JsonStringEnumConverter.
    // ========================================================================

    /// <summary>BusinessProfile snapshot — Owner-facing read view (1 per tenant).</summary>
    public record BusinessProfileDto(
        Guid TenantId,
        decimal MonthlyRent,
        decimal MonthlyPayroll,
        decimal MonthlyUtilities,
        decimal MonthlyMarketing,
        decimal MonthlyLogistics,
        decimal MonthlyOtherOpEx,
        decimal MonthlyDepreciation,
        decimal TotalMonthlyFixedCost,    // computed — convenience for UI
        int DailyCapacityUnits,
        int OperatingDaysPerMonth,
        PricingModel PricingModel,
        string? Notes,
        string Version,                   // FinancialModelVersion.ToString() — "1.0"
        DateTime UpdatedAt
    );

    /// <summary>Body for PUT /api/financial/business-profile (upsert).</summary>
    public record UpdateBusinessProfileDto(
        decimal MonthlyRent,
        decimal MonthlyPayroll,
        decimal MonthlyUtilities,
        decimal MonthlyMarketing,
        decimal MonthlyLogistics,
        decimal MonthlyOtherOpEx,
        decimal MonthlyDepreciation,
        int DailyCapacityUnits,
        int OperatingDaysPerMonth,
        PricingModel PricingModel,
        string? Notes
    );

    /// <summary>Profit summary widget DTO (KU-01).</summary>
    public record ProfitSummaryDto(
        Guid TenantId,
        int Year,
        int Month,
        DateTime CalculatedAt,
        decimal Revenue,
        decimal COGS,
        decimal GrossProfit,
        decimal GrossMarginPercent,
        decimal OperatingExpenses,
        decimal OperatingProfit,
        decimal NetProfit,
        decimal NetMarginPercent,
        ProfitStatus Status,
        string? WarningMessage
    );

    /// <summary>Break-even single DTO (KU-02).</summary>
    public record BreakEvenAnalysisDto(
        Guid TenantId,
        int Year,
        int Month,
        DateTime CalculatedAt,
        string ModelVersion,
        decimal TotalFixedCost,
        decimal TotalRevenue,
        decimal TotalVariableCost,
        decimal TotalContributionMargin,
        decimal ContributionMarginRatio,
        decimal BreakEvenRevenue,
        decimal BreakEvenUnits,
        decimal MarginOfSafetyRevenue,
        decimal MarginOfSafetyPercent,
        BreakEvenStatus Status,
        string? WarningMessage,
        IReadOnlyList<string> SourceAccountCodes
    );

    public record ProductBreakEvenLineDto(
        Guid ProductId,
        string ProductName,
        decimal SellingPrice,
        decimal VariableCost,
        decimal ContributionMargin,
        decimal ContributionMarginRatio,
        decimal SalesMixPercent,
        int UnitsSoldInPeriod,
        decimal ProductBreakEvenUnits
    );

    /// <summary>Multi-product break-even DTO.</summary>
    public record MultiProductBreakEvenDto(
        Guid TenantId,
        int Year,
        int Month,
        DateTime CalculatedAt,
        string ModelVersion,
        decimal TotalFixedCost,
        decimal WeightedContributionMargin,
        decimal WeightedContributionMarginRatio,
        decimal BreakEvenRevenue,
        IReadOnlyList<ProductBreakEvenLineDto> ProductLines
    );

    public record UnitEconomicsLineDto(
        Guid ProductId,
        string ProductName,
        string Category,
        decimal SellingPrice,
        decimal VariableCost,
        decimal ContributionMargin,
        decimal ContributionMarginPercent,
        int UnitsSold,
        decimal Revenue,
        decimal ProfitContribution,
        int ProfitContributionRank,
        bool HasMissingCostPrice
    );

    public record UnitEconomicsReportDto(
        Guid TenantId,
        int Year,
        int Month,
        DateTime CalculatedAt,
        string ModelVersion,
        IReadOnlyList<UnitEconomicsLineDto> Products,
        int TotalProductsAnalyzed,
        int ProductsWithMissingCostPrice,
        decimal TotalContribution,
        decimal AverageContributionMargin
    );

    /// <summary>Target Profit analysis DTO (KU-03).</summary>
    public record TargetProfitAnalysisDto(
        Guid TenantId,
        int Year,
        int Month,
        DateTime CalculatedAt,
        string ModelVersion,
        decimal TargetProfit,
        decimal TotalFixedCost,
        decimal AverageContributionMargin,
        decimal RequiredRevenue,
        decimal RequiredUnits,
        decimal RequiredDailyUnits,
        bool Feasible,
        string? FeasibilityWarning
    );

    /// <summary>Body for POST /api/financial/target-profit.</summary>
    public record TargetProfitRequestDto(
        int Year,
        int Month,
        AccountingStandard Standard,
        decimal TargetProfit
    );
}
