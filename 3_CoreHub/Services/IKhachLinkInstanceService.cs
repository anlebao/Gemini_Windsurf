using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// KhachLink Multi-Profile R1: Service for managing KhachLink instances.
    /// Platform-level CRUD + by-domain lookup. Used by Gateway Admin API (SystemAdmin)
    /// and KhachLink runtime (by-domain public lookup).
    ///
    /// KhachLinkInstance is a platform-level entity (TenantId = Guid.Empty sentinel),
    /// excluded from multi-tenancy query filter — no IgnoreQueryFilters needed.
    /// </summary>
    public interface IKhachLinkInstanceService
    {
        Task<KhachLinkInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<KhachLinkInstance?> GetByDomainAsync(string customDomain, CancellationToken ct = default);
        Task<List<KhachLinkInstance>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Get active CustomDomain values only (lightweight — for CORS cache + routing lookups).
        /// Returns only IsActive = true instances with non-empty CustomDomain.
        /// </summary>
        Task<List<string>> GetActiveCustomDomainsAsync(CancellationToken ct = default);
        Task<KhachLinkInstance> CreateAsync(
            string label,
            KhachLinkProfile profile,
            string customDomain,
            Guid? ownerTenantId = null,
            KhachLinkNavFlags? navFlagsOverride = null,
            CancellationToken ct = default);
        Task<bool> UpdateAsync(
            Guid id,
            KhachLinkProfile profile,
            KhachLinkNavFlags navFlags,
            CancellationToken ct = default);
        Task<bool> DeactivateAsync(Guid id, CancellationToken ct = default);
        Task<bool> ActivateAsync(Guid id, CancellationToken ct = default);
    }
}
