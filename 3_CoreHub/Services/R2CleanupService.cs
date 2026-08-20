using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Repositories;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// R2 photo cleanup service — deletes expired photos from Cloudflare R2
    /// after retention period (default 30 days post-checkout/void).
    /// Per-tenant isolation: cleanup processes one tenant at a time.
    /// </summary>
    public class R2CleanupService(
        IR2StorageService r2Storage,
        IVehicleSessionRepository sessionRepo,
        ILogger<R2CleanupService> logger) : IR2CleanupService
    {
        private readonly IR2StorageService _r2Storage = r2Storage;
        private readonly IVehicleSessionRepository _sessionRepo = sessionRepo;
        private readonly ILogger<R2CleanupService> _logger = logger;

        /// <summary>
        /// Get storage stats for a tenant (photo count + total size).
        /// Lists R2 objects under plates/{tenantId}/ and customers/{tenantId}/.
        /// </summary>
        public async Task<TenantStorageStats> GetTenantStatsAsync(Guid tenantId, CancellationToken ct = default)
        {
            try
            {
                var platePrefix = IR2StorageService.GetPlatePrefix(tenantId);
                var customerPrefix = IR2StorageService.GetCustomerPrefix(tenantId);

                var plateObjects = await _r2Storage.ListObjectsByPrefixAsync(platePrefix, ct);
                var customerObjects = await _r2Storage.ListObjectsByPrefixAsync(customerPrefix, ct);

                var allObjects = plateObjects.Concat(customerObjects).ToList();
                var totalSize = allObjects.Sum(o => o.Size);
                var oldestDate = allObjects.Count > 0 ? allObjects.Min(o => o.LastModified) : (DateTime?)null;

                return new TenantStorageStats(
                    plateObjects.Count,
                    customerObjects.Count,
                    totalSize,
                    oldestDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storage stats for tenant {TenantId}", tenantId);
                return new TenantStorageStats(0, 0, 0, null);
            }
        }

        /// <summary>
        /// Cleanup expired photos for a specific tenant.
        /// Deletes R2 objects for sessions that are CheckedOut/Voided past retention period,
        /// then clears PlatePhotoKey/CustomerPhotoKey in DB.
        /// </summary>
        public async Task<R2CleanupResult> CleanupTenantAsync(Guid tenantId, TimeSpan retentionPeriod, CancellationToken ct = default)
        {
            var errors = new List<string>();
            var cutoff = DateTime.UtcNow - retentionPeriod;

            try
            {
                var expiredSessions = await _sessionRepo.GetExpiredSessionsAsync(tenantId, cutoff, ct);
                if (expiredSessions.Count == 0)
                {
                    _logger.LogDebug("No expired sessions with photos for tenant {TenantId}", tenantId);
                    return new R2CleanupResult(0, 0, 0, errors);
                }

                // Collect R2 keys to delete (plate + customer photos)
                var keysToDelete = new List<string>();
                long bytesToFree = 0;

                // Get object sizes for stats by listing per-prefix
                var platePrefix = IR2StorageService.GetPlatePrefix(tenantId);
                var customerPrefix = IR2StorageService.GetCustomerPrefix(tenantId);
                var plateObjects = await _r2Storage.ListObjectsByPrefixAsync(platePrefix, ct);
                var customerObjects = await _r2Storage.ListObjectsByPrefixAsync(customerPrefix, ct);
                var objectSizeMap = plateObjects.Concat(customerObjects).ToDictionary(o => o.Key, o => o.Size);

                foreach (var session in expiredSessions)
                {
                    if (!string.IsNullOrEmpty(session.PlatePhotoKey))
                    {
                        keysToDelete.Add(session.PlatePhotoKey);
                        bytesToFree += objectSizeMap.GetValueOrDefault(session.PlatePhotoKey, 0);
                    }
                    if (!string.IsNullOrEmpty(session.CustomerPhotoKey))
                    {
                        keysToDelete.Add(session.CustomerPhotoKey);
                        bytesToFree += objectSizeMap.GetValueOrDefault(session.CustomerPhotoKey, 0);
                    }
                }

                // Delete R2 objects
                var photosDeleted = 0;
                if (keysToDelete.Count > 0)
                {
                    photosDeleted = await _r2Storage.DeleteObjectsAsync(keysToDelete, ct);
                    _logger.LogInformation(
                        "R2 cleanup for tenant {TenantId}: deleted {Deleted}/{Requested} photos, freed {Bytes} bytes",
                        tenantId, photosDeleted, keysToDelete.Count, bytesToFree);
                }

                // Clear DB photo keys
                var sessionIds = expiredSessions.Select(s => s.Id).ToList();
                var cleared = await _sessionRepo.ClearPhotoKeysAsync(sessionIds, ct);
                if (cleared != sessionIds.Count)
                {
                    errors.Add($"DB clear mismatch: expected {sessionIds.Count}, got {cleared}");
                }

                return new R2CleanupResult(expiredSessions.Count, photosDeleted, bytesToFree, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during R2 cleanup for tenant {TenantId}", tenantId);
                errors.Add(ex.Message);
                return new R2CleanupResult(0, 0, 0, errors);
            }
        }

        /// <summary>
        /// Cleanup expired photos for ALL tenants (background service use).
        /// Queries distinct tenant IDs with expired sessions, processes each sequentially.
        /// </summary>
        public async Task<R2CleanupResult> CleanupAllTenantsAsync(TimeSpan retentionPeriod, CancellationToken ct = default)
        {
            var errors = new List<string>();
            var cutoff = DateTime.UtcNow - retentionPeriod;

            try
            {
                var tenantIds = await _sessionRepo.GetTenantsWithExpiredSessionsAsync(cutoff, ct);
                if (tenantIds.Count == 0)
                {
                    _logger.LogDebug("No tenants with expired sessions found");
                    return new R2CleanupResult(0, 0, 0, errors);
                }

                _logger.LogInformation("R2 cleanup: processing {Count} tenants with expired sessions", tenantIds.Count);

                var totalSessions = 0;
                var totalPhotos = 0;
                long totalBytes = 0;

                foreach (var tenantId in tenantIds)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    var result = await CleanupTenantAsync(tenantId, retentionPeriod, ct);
                    totalSessions += result.SessionsProcessed;
                    totalPhotos += result.PhotosDeleted;
                    totalBytes += result.BytesFreed;
                    errors.AddRange(result.Errors);
                }

                _logger.LogInformation(
                    "R2 cleanup complete: {Tenants} tenants, {Sessions} sessions, {Photos} photos deleted, {Bytes} bytes freed",
                    tenantIds.Count, totalSessions, totalPhotos, totalBytes);

                return new R2CleanupResult(totalSessions, totalPhotos, totalBytes, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during R2 cleanup for all tenants");
                errors.Add(ex.Message);
                return new R2CleanupResult(0, 0, 0, errors);
            }
        }
    }
}
