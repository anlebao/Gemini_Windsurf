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
        /// Get all CustomDomain values (lightweight — for CORS cache + routing lookups).
        /// #134-fix: Returns ALL instances (including disabled) so CORS allows KhachLink
        /// WASM to fetch by-domain config. The WASM then checks IsActive and shows a
        /// "disabled" page. Previously filtered IsActive=true → disabled domains removed
        /// from CORS snapshot → CORS blocked fetch → WASM fell back to FullCommerce.
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
            string? theme = null,
            string? logoUrl = null,
            string? navColor = null,
            string? headerColor = null,
            string? footerColor = null,
            CancellationToken ct = default);
        Task<bool> DeactivateAsync(Guid id, CancellationToken ct = default);
        Task<bool> ActivateAsync(Guid id, CancellationToken ct = default);
    }
}
