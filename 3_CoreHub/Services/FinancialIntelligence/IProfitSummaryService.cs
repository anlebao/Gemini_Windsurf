using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Profit summary for dashboard widget (KU-01).
    /// Wraps <see cref="IIncomeStatementService"/> — extracts Revenue / COGS / OpEx / NetProfit
    /// from the extended <see cref="IncomeStatement"/> record (Option 2 additive fields).
    /// Pure deterministic calculation — Trust Level 1 (NFR-14).
    /// </summary>
    public interface IProfitSummaryService
    {
        /// <summary>
        /// Build a ProfitSummary for a tenant + period. Returns InsufficientData status
        /// when no accounting movement exists for the period.
        /// </summary>
        Task<ProfitSummary> GetAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
    }
}
