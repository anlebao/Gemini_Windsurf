using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Phase 2 (Multi-VPS Checkout): Service for managing ShopERP hosting instances.
    /// Platform-level CRUD + health check. Used by Gateway Admin API (Phase 2) and
    /// Gateway router (Phase 3) to resolve tenant → ShopInstance → BaseUrl.
    /// </summary>
    public interface IShopInstanceService
    {
        /// <summary>Creates a new ShopInstance. Validates unique BaseUrl + URL format.</summary>
        Task<ShopInstance> CreateAsync(
            string baseUrl,
            string label,
            int maxTenants = 50,
            string? healthCheckUrl = null,
            CancellationToken ct = default);

        /// <summary>Gets a ShopInstance by Id. Returns null if not found.</summary>
        Task<ShopInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>Lists all ShopInstances (including inactive).</summary>
        Task<List<ShopInstance>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Lists only active ShopInstances.</summary>
        Task<List<ShopInstance>> GetActiveAsync(CancellationToken ct = default);

        /// <summary>Updates label + maxTenants for an existing ShopInstance.</summary>
        /// <returns>True if updated, false if not found.</returns>
        Task<bool> UpdateAsync(
            Guid id,
            string label,
            int maxTenants,
            CancellationToken ct = default);

        /// <summary>Activates or deactivates a ShopInstance.</summary>
        /// <returns>True if toggled, false if not found.</returns>
        Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);

        /// <summary>
        /// Pings the ShopInstance health endpoint (HealthCheckUrl or BaseUrl/health).
        /// Updates HealthStatus + LastHealthCheck on the entity. Saves changes.
        /// </summary>
        Task<ShopInstanceHealthResult> CheckHealthAsync(Guid id, CancellationToken ct = default);

        /// <summary>Counts tenants assigned to a ShopInstance (for capacity display).</summary>
        Task<int> CountTenantsAsync(Guid shopInstanceId, CancellationToken ct = default);
    }
}
