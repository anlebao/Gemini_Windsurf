using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Repository interface for BusinessProfile entities.
    /// Tenant-scoped (1 row per tenant — unique index on TenantId).
    /// </summary>
    public interface IBusinessProfileRepository
    {
        /// <summary>Get the BusinessProfile for a tenant. Returns null if not yet declared.</summary>
        Task<BusinessProfile?> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);

        /// <summary>Add a new BusinessProfile.</summary>
        Task<BusinessProfile> AddAsync(BusinessProfile profile, CancellationToken cancellationToken = default);

        /// <summary>Update an existing BusinessProfile.</summary>
        Task<BusinessProfile> UpdateAsync(BusinessProfile profile, CancellationToken cancellationToken = default);

        /// <summary>Persist changes.</summary>
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
