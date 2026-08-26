using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): Detects + resolves duplicate tenants (same MST).
    /// Correction H5: first canonical tenant kept, rest marked duplicate of canonical (not chain).
    /// NO data merge — SysAdmin picks one to Verify, other → Inactive.
    /// </summary>
    public interface IDuplicateDetectionService
    {
        /// <summary>
        /// Checks if an existing tenant has the same TaxCode.
        /// If found, marks the NEW tenant as PotentialDuplicateOf the existing one.
        /// Correction H5: first canonical tenant is the existing one (oldest Active/Pending),
        /// new tenant gets PotentialDuplicateOf = canonical.Id.
        /// </summary>
        /// <param name="newTenantId">The newly created Pending tenant to check.</param>
        /// <param name="taxCode">Tax code to search for (null/empty = skip).</param>
        /// <returns>True if duplicate detected + marked; false if no duplicate.</returns>
        Task<bool> MarkDuplicateIfTaxCodeExistsAsync(TenantId newTenantId, string? taxCode, CancellationToken ct = default);

        /// <summary>
        /// Lists all tenants with PotentialDuplicateOf != null (for SysAdmin Duplicates tab).
        /// </summary>
        Task<IReadOnlyList<DuplicateTenantDto>> ListPotentialDuplicatesAsync(CancellationToken ct = default);

        /// <summary>
        /// Resolves a duplicate: verify the "keep" tenant, deactivate the "other" tenant.
        /// NO data merge — just lifecycle transitions.
        /// </summary>
        Task ResolveDuplicateAsync(Guid keepTenantId, Guid deactivateTenantId, string reason, CancellationToken ct = default);
    }

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25): DTO for duplicate tenant display in SysAdmin UI.
    /// </summary>
    public record DuplicateTenantDto(
        Guid TenantId,
        string TenantName,
        string? TaxCode,
        Guid? PotentialDuplicateOf,
        string? CanonicalTenantName,
        string Status,
        DateTime CreatedAt);
}
