using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): Detects + resolves duplicate tenants (same MST).
    /// Correction H5: first canonical tenant kept, rest marked duplicate of canonical.
    /// NO data merge — SysAdmin picks one to Verify, other → Inactive.
    /// </summary>
    public class DuplicateDetectionService(
        IVanAnDbContext dbContext,
        ILogger<DuplicateDetectionService> logger) : IDuplicateDetectionService
    {
        public async Task<bool> MarkDuplicateIfTaxCodeExistsAsync(
            TenantId newTenantId,
            string? taxCode,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(taxCode))
                return false;

            // Find existing tenant with same TaxCode (Active OR Pending — both are canonical candidates).
            // Correction H5: first canonical = oldest existing tenant (OrderBy CreatedAt).
            var existing = await dbContext.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Settings.TaxCode == taxCode && t.Id != newTenantId)
                .OrderBy(t => t.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (existing is null)
                return false;

            // Mark the NEW tenant as duplicate of the existing canonical tenant
            var newTenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == newTenantId, ct);
            if (newTenant is null)
                return false;

            newTenant.MarkPotentialDuplicateOf(existing.Id.Value);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Tenant {NewTenantId} marked as potential duplicate of {CanonicalTenantId} (same MST {TaxCode})",
                newTenantId.Value, existing.Id.Value, taxCode);

            return true;
        }

        public async Task<IReadOnlyList<DuplicateTenantDto>> ListPotentialDuplicatesAsync(CancellationToken ct = default)
        {
            var duplicates = await dbContext.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.PotentialDuplicateOf != null)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync(ct);

            // Load canonical tenant names for display
            var canonicalIds = duplicates
                .Where(t => t.PotentialDuplicateOf.HasValue)
                .Select(t => t.PotentialDuplicateOf!.Value)
                .Distinct()
                .ToList();

            var canonicalTenants = await dbContext.Tenants
                .IgnoreQueryFilters()
                .Where(t => canonicalIds.Contains(t.Id.Value))
                .ToDictionaryAsync(t => t.Id.Value, t => t.Name, ct);

            return duplicates.Select(t => new DuplicateTenantDto(
                TenantId: t.Id.Value,
                TenantName: t.Name,
                TaxCode: t.Settings.TaxCode,
                PotentialDuplicateOf: t.PotentialDuplicateOf,
                CanonicalTenantName: t.PotentialDuplicateOf.HasValue
                    ? canonicalTenants.GetValueOrDefault(t.PotentialDuplicateOf.Value)
                    : null,
                Status: t.Status.ToString(),
                CreatedAt: t.CreatedAt)).ToList();
        }

        public async Task ResolveDuplicateAsync(
            Guid keepTenantId,
            Guid deactivateTenantId,
            string reason,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            if (keepTenantId == deactivateTenantId)
                throw new ArgumentException("Cannot resolve duplicate: keep and deactivate are the same tenant.");

            var deactivateTenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == new TenantId(deactivateTenantId), ct)
                ?? throw new KeyNotFoundException($"Tenant {deactivateTenantId} not found.");

            // Deactivate the duplicate tenant (lifecycle transition, NO data merge)
            deactivateTenant.Deactivate(reason);

            // Clear PotentialDuplicateOf flag on the deactivated tenant
            // (it's now Inactive, no longer a "potential" duplicate — it's resolved)
            // Note: We don't clear the flag in domain — it stays for audit trail.
            // The Inactive status itself indicates resolution.

            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Duplicate resolved: kept tenant {KeepTenantId}, deactivated tenant {DeactivateTenantId} — reason: {Reason}",
                keepTenantId, deactivateTenantId, reason);
        }
    }
}
