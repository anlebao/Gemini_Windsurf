using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Break-even analysis service (KU-02).
    /// Single-product (period aggregate) + Multi-product weighted average contribution margin.
    /// Pure deterministic calculation — Trust Level 1 (NFR-14).
    /// </summary>
    public interface IBreakEvenAnalysisService
    {
        /// <summary>
        /// Single-period break-even at the tenant aggregate level.
        /// Returns <see cref="BreakEvenStatus.InsufficientData"/> when no BusinessProfile
        /// or no P&L movement for the period.
        /// </summary>
        Task<BreakEvenAnalysis> AnalyzeAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);

        /// <summary>
        /// Multi-product weighted break-even. Returns empty <see cref="MultiProductBreakEven.ProductLines"/>
        /// when no OrderItem movement exists for the period.
        /// Per-product VariableCost = Product.CostPrice (fallback 70% UnitPrice — match OrderService.CalculateCogsAmount).
        /// </summary>
        Task<MultiProductBreakEven> AnalyzeMultiProductAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
    }
}
