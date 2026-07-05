using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 8 — D9 HKD→DN Conversion Service (Option B: New Tenant + Link).
///
/// Conversion flow:
/// 1. Validate: HKD tenant exists, Status=Active/Suspended (not Inactive/Converted), Type=HKD
/// 2. Create new DN Tenant via Tenant.CreateFromConversion (W2 factory)
/// 3. Migrate opening balance: query HKD closing balance (AccountingEntry aggregates),
///    map HKD accounts → DN accounts via IHkdToEnterpriseAccountMapper (W3),
///    create OpeningBalance entries for DN
/// 4. Mark HKD as converted: hkdTenant.MarkConvertedTo(newTenantId) → Status=Converted (read-only)
/// 5. Save both tenants (transactional)
/// 6. Raise TenantConvertedEvent (via aggregate domain events)
/// </summary>
public interface ITenantConversionService
{
    /// <summary>
    /// Convert an HKD tenant to an Enterprise (DN) tenant.
    /// Creates a new DN tenant linked to the HKD predecessor, migrates opening balance,
    /// and marks the HKD tenant as Converted (read-only historical).
    /// </summary>
    /// <param name="hkdTenantId">The HKD tenant to convert.</param>
    /// <param name="newType">Target Enterprise type (SME/Large/SuperSmall — not HKD).</param>
    /// <param name="standard">Target accounting standard (TT99/133/58).</param>
    /// <param name="newName">New DN tenant name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created DN tenant.</returns>
    /// <exception cref="InvalidOperationException">HKD tenant not found, not HKD type, or already converted.</exception>
    Task<Tenant> ConvertHkdToEnterpriseAsync(
        Guid hkdTenantId,
        TenantType newType,
        AccountingStandard standard,
        string newName,
        CancellationToken ct = default);

    /// <summary>Get the predecessor (HKD cũ) for a DN tenant — for read-only historical access.</summary>
    Task<Tenant?> GetPredecessorAsync(Guid enterpriseTenantId, CancellationToken ct = default);

    /// <summary>Get the successor (DN mới) for an HKD tenant — from HKD perspective.</summary>
    Task<Tenant?> GetSuccessorAsync(Guid hkdTenantId, CancellationToken ct = default);

    /// <summary>Migrate HKD closing balance to DN opening balance (best-effort account mapping via W3 mapper).</summary>
    /// <returns>Tuple of (mappings count, total debit, total credit) for verification.</returns>
    Task<(int MappingsCount, decimal TotalDebit, decimal TotalCredit)> MigrateOpeningBalanceAsync(
        Guid hkdTenantId,
        Guid newEnterpriseTenantId,
        AccountingStandard standard,
        CancellationToken ct = default);
}
