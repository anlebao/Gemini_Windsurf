using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Target Profit analysis (KU-03).
    /// Given a target profit, computes required revenue / units / daily units + feasibility vs capacity.
    /// Pure deterministic calculation — Trust Level 1 (NFR-14).
    /// </summary>
    public interface ITargetProfitService
    {
        /// <summary>
        /// Compute the required revenue / units / daily units to achieve a target profit.
        /// RequiredRevenue = (FixedCost + TargetProfit) / ContributionMarginRatio.
        /// Feasibility compares RequiredDailyUnits against BusinessProfile.DailyCapacityUnits.
        /// </summary>
        Task<TargetProfitAnalysis> AnalyzeAsync(
            TenantId tenantId,
            AccountingPeriod period,
            AccountingStandard standard,
            decimal targetProfit,
            CancellationToken ct = default);
    }
}
