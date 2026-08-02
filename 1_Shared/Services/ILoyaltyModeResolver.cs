using VanAn.Shared.Domain;

namespace VanAn.Shared.Services;

/// <summary>
/// Resolves the effective loyalty operating mode for a tenant.
/// Tenant override takes precedence; falls back to global config when tenant value is null.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0 (Q2: full opt-out via IsAllianceMember=false).
/// </summary>
public interface ILoyaltyModeResolver
{
    /// <summary>
    /// Returns the effective loyalty mode for the tenant.
    /// Tenant override (non-null) wins; otherwise global Mode is returned.
    /// Note: a tenant with IsAllianceMember=false is forced to Silo regardless of Mode.
    /// </summary>
    Task<LoyaltyMode> GetEffectiveModeAsync(Guid tenantId);

    /// <summary>
    /// Returns the effective MaxWalletPoints cap for the tenant.
    /// Tenant override (non-null) wins; otherwise global MaxWalletPoints is returned.
    /// </summary>
    Task<int> GetEffectiveMaxWalletPointsAsync(Guid tenantId);

    /// <summary>
    /// Returns true when the tenant is an active alliance member.
    /// A tenant with no LoyaltyTenantConfig row, or IsAllianceMember=false, returns false.
    /// </summary>
    Task<bool> IsAllianceMemberAsync(Guid tenantId);
}
