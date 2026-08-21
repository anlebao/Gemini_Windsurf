using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Unit Economics — per product ranking (KU-05).
    /// Identifies loss-makers + products with missing CostPrice.
    /// Pure deterministic calculation — Trust Level 1 (NFR-14).
    /// </summary>
    public interface IUnitEconomicsService
    {
        /// <summary>
        /// Build a per-product ranking sorted by ProfitContribution DESC.
        /// Uses AccountingStandard-independent OrderItem aggregation (no chart lookup needed).
        /// </summary>
        Task<UnitEconomicsReport> AnalyzeAsync(TenantId tenantId, AccountingPeriod period, CancellationToken ct = default);
    }
}
