using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain
{
    // ========================================================================
    // VA-FI-MVP2 (approved 2026-08-21) — Financial Intelligence result records.
    // All records are immutable + NOT persisted (pure calculation, recompute on demand).
    // Trust Level 1 (deterministic) per parent spec §27.
    // ========================================================================

    /// <summary>Profit status classification for ProfitSummary widget.</summary>
    public enum ProfitStatus
    {
        Profitable = 0,
        AtBreakEven = 1,
        Loss = 2,
        InsufficientData = 3
    }

    /// <summary>Break-even status classification.</summary>
    public enum BreakEvenStatus
    {
        AboveBreakEven = 0,
        AtBreakEven = 1,
        BelowBreakEven = 2,
        InsufficientData = 3
    }

    /// <summary>
    /// Profit summary for dashboard — quick "tháng này có lời không?" (KU-01).
    /// Derived from extended IncomeStatement (Revenue, COGS, OpEx, NetProfit).
    /// </summary>
    public record ProfitSummary(
        TenantId TenantId,
        AccountingPeriod Period,
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

    /// <summary>
    /// Break-even analysis result — single period (KU-02).
    /// Formula: BreakEvenRevenue = TotalFixedCost / ContributionMarginRatio.
    /// </summary>
    public record BreakEvenAnalysis(
        TenantId TenantId,
        AccountingPeriod Period,
        DateTime CalculatedAt,
        FinancialModelVersion ModelVersion,
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

    /// <summary>One product line in multi-product break-even analysis.</summary>
    public record ProductBreakEvenLine(
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

    /// <summary>
    /// Multi-product break-even — weighted average contribution margin.
    /// Σ ProductBreakEvenUnits ≈ total break-even units (tolerance 5% due to rounding).
    /// </summary>
    public record MultiProductBreakEven(
        TenantId TenantId,
        AccountingPeriod Period,
        DateTime CalculatedAt,
        FinancialModelVersion ModelVersion,
        decimal TotalFixedCost,
        decimal WeightedContributionMargin,
        decimal WeightedContributionMarginRatio,
        decimal BreakEvenRevenue,
        IReadOnlyList<ProductBreakEvenLine> ProductLines
    );

    /// <summary>One product line in unit economics ranking.</summary>
    public record UnitEconomicsLine(
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

    /// <summary>
    /// Unit Economics — per product ranking (KU-05: "Món nào đang làm giảm lợi nhuận?").
    /// Products sorted by ProfitContribution DESC. Loss makers (negative contribution) at bottom.
    /// </summary>
    public record UnitEconomicsReport(
        TenantId TenantId,
        AccountingPeriod Period,
        DateTime CalculatedAt,
        FinancialModelVersion ModelVersion,
        IReadOnlyList<UnitEconomicsLine> Products,
        int TotalProductsAnalyzed,
        int ProductsWithMissingCostPrice,
        decimal TotalContribution,
        decimal AverageContributionMargin
    );

    /// <summary>
    /// Target Profit analysis (KU-03: "Muốn lời 50 triệu thì phải bán bao nhiêu?").
    /// Formula: RequiredRevenue = (FixedCost + TargetProfit) / CMRatio.
    /// Feasibility check vs DailyCapacityUnits × OperatingDaysPerMonth.
    /// </summary>
    public record TargetProfitAnalysis(
        TenantId TenantId,
        AccountingPeriod Period,
        DateTime CalculatedAt,
        FinancialModelVersion ModelVersion,
        decimal TargetProfit,
        decimal TotalFixedCost,
        decimal AverageContributionMargin,
        decimal RequiredRevenue,
        decimal RequiredUnits,
        decimal RequiredDailyUnits,
        bool Feasible,
        string? FeasibilityWarning
    );
}
