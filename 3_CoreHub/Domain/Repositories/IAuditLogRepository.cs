using VanAn.Shared.Domain.Audit;

namespace VanAn.CoreHub.Domain.Repositories
{
    /// <summary>
    /// Repository interface for AuditLog entity
    /// Engineering Constitution Compliance:
    /// - Append-only: Create only, no Update or Delete
    /// - Always filter by tenant for data isolation
    /// - Optimized for query patterns used in audit trail viewer
    /// </summary>
    public interface IAuditLogRepository
    {
        /// <summary>
        /// Get single audit log by ID (tenant-filtered)
        /// </summary>
        Task<AuditLog?> GetByIdAsync(Guid id);

        /// <summary>
        /// Get audit logs by query filter (tenant-filtered, paginated)
        /// </summary>
        Task<AuditLogPagedResult> GetByQueryAsync(AuditLogQuery query);

        /// <summary>
        /// Get all audit logs for a specific entity (tenant-filtered)
        /// </summary>
        Task<IReadOnlyList<AuditLog>> GetByEntityAsync(AuditableEntityType entityType, Guid entityId, int maxResults = 100);

        /// <summary>
        /// Get audit logs by correlation ID (for tracking related operations)
        /// </summary>
        Task<IReadOnlyList<AuditLog>> GetByCorrelationIdAsync(string correlationId);

        /// <summary>
        /// Add new audit log (append-only)
        /// </summary>
        Task<AuditLog> AddAsync(AuditLog auditLog);

        /// <summary>
        /// Add multiple audit logs in batch (for bulk operations)
        /// </summary>
        Task<IReadOnlyList<AuditLog>> AddRangeAsync(IEnumerable<AuditLog> auditLogs);

        /// <summary>
        /// Get recent audit logs for dashboard display
        /// </summary>
        Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count = 50);

        /// <summary>
        /// Get audit log count for statistics
        /// </summary>
        Task<int> GetCountAsync(DateTime? fromDate = null, DateTime? toDate = null);
    }
}
