using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Service for BusinessProfile CRUD — tenant business profile
    /// for Financial Intelligence Layer (fixed costs + capacity + pricing model).
    /// Tenant-scoped — 1 BusinessProfile per tenant (unique index in DB).
    /// </summary>
    public interface IBusinessProfileService
    {
        /// <summary>Get the BusinessProfile for a tenant. Returns null if not yet declared.</summary>
        Task<BusinessProfile?> GetAsync(TenantId tenantId, CancellationToken ct = default);

        /// <summary>Get existing profile or create one with default values (zeros + FixedPrice + 30 days).</summary>
        Task<BusinessProfile> GetOrCreateDefaultAsync(TenantId tenantId, CancellationToken ct = default);

        /// <summary>Update (or create if missing) the BusinessProfile. Increments Version on update.</summary>
        Task<BusinessProfile> UpdateAsync(TenantId tenantId, UpdateBusinessProfileCommand cmd, CancellationToken ct = default);
    }

    /// <summary>Command for updating BusinessProfile — all 7 fixed costs + capacity + pricing.</summary>
    public record UpdateBusinessProfileCommand(
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
        string? Notes = null
    );
}
