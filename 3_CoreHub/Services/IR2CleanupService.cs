namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// R2 photo cleanup service — deletes expired photos from Cloudflare R2
    /// after retention period (default 30 days post-checkout/void).
    /// Per-tenant isolation: cleanup processes one tenant at a time.
    /// </summary>
    public interface IR2CleanupService
    {
        /// <summary>
        /// Get storage stats for a tenant (photo count + total size).
        /// Lists R2 objects under plates/{tenantId}/ and customers/{tenantId}/.
        /// </summary>
        Task<TenantStorageStats> GetTenantStatsAsync(Guid tenantId, CancellationToken ct = default);

        /// <summary>
        /// Cleanup expired photos for a specific tenant.
        /// Deletes R2 objects for sessions that are CheckedOut/Voided past retention period,
        /// then clears PlatePhotoKey/CustomerPhotoKey in DB.
        /// </summary>
        Task<R2CleanupResult> CleanupTenantAsync(Guid tenantId, TimeSpan retentionPeriod, CancellationToken ct = default);

        /// <summary>
        /// Cleanup expired photos for ALL tenants (background service use).
        /// Queries distinct tenant IDs with expired sessions, processes each sequentially.
        /// </summary>
        Task<R2CleanupResult> CleanupAllTenantsAsync(TimeSpan retentionPeriod, CancellationToken ct = default);
    }

    /// <summary>
    /// Storage stats for a single tenant.
    /// </summary>
    public record TenantStorageStats(
        int PlatePhotoCount,
        int CustomerPhotoCount,
        long TotalSizeBytes,
        DateTime? OldestPhotoDate);

    /// <summary>
    /// Result of a cleanup operation (single tenant or all tenants).
    /// Named R2CleanupResult to avoid collision with VanAn.Shared.Omnichannel.CleanupResult
    /// and VanAn.CoreHub.Infrastructure.ProjectMemory.CleanupResult.
    /// </summary>
    public record R2CleanupResult(
        int SessionsProcessed,
        int PhotosDeleted,
        long BytesFreed,
        List<string> Errors);
}
