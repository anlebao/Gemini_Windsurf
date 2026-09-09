using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service for audit trail operations.
    /// Engineering Constitution Compliance:
    /// - Append-only: Create only, no Update or Delete
    /// - All operations automatically capture user context
    /// - Tenant isolation enforced at service layer
    /// </summary>
    public interface IAuditTrailService
    {
        /// <summary>
        /// Log entity creation. Returns null when audit toggle is OFF (Sprint 3 EXPANDED).
        /// </summary>
        Task<AuditLog?> LogCreateAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string newValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log entity update. Returns null when audit toggle is OFF (Sprint 3 EXPANDED).
        /// </summary>
        Task<AuditLog?> LogUpdateAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string oldValues,
            string newValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log entity deletion (soft delete). Returns null when audit toggle is OFF (Sprint 3 EXPANDED).
        /// </summary>
        Task<AuditLog?> LogDeleteAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string oldValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log period closing with reason. Returns null when audit toggle is OFF (Sprint 3 EXPANDED).
        /// </summary>
        Task<AuditLog?> LogPeriodCloseAsync(
            AccountingPeriod period,
            string reason,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log period reopening with reason. Returns null when audit toggle is OFF (Sprint 3 EXPANDED).
        /// </summary>
        Task<AuditLog?> LogPeriodReopenAsync(
            AccountingPeriod period,
            string reason,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log correction entry. Returns null when audit toggle is OFF (Sprint 3 EXPANDED).
        /// </summary>
        Task<AuditLog?> LogCorrectionAsync(
            Guid originalEntryId,
            string correctionReason,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log reversal entry. Returns null when audit toggle is OFF (Sprint 3 EXPANDED).
        /// </summary>
        Task<AuditLog?> LogReversalAsync(
            Guid originalEntryId,
            Guid reversalEntryId,
            string reversalReason,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sprint 3 P3.3: Log a security event (failed login, rate limit hit, suspicious activity).
        /// Uses AuditActionType.SecurityAlert / FailedLogin / SuspiciousActivity / RateLimitHit.
        /// EntityType = SecurityEvent, EntityId = Guid.Empty.
        /// EXPANDED: persisted ASYNC via AuditLogQueue (fire-and-forget) — không chặn login/429.
        /// </summary>
        Task<AuditLog?> LogSecurityEventAsync(
            AuditActionType actionType,
            string description,
            string? correlationId = null,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Query audit logs with filters
        /// </summary>
        Task<AuditLogPagedResult> QueryAsync(
            AuditLogQuery query,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get audit history for a specific entity
        /// </summary>
        Task<IReadOnlyList<AuditLog>> GetEntityHistoryAsync(
            AuditableEntityType entityType,
            Guid entityId,
            int maxResults = 100,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get recent audit logs for dashboard
        /// </summary>
        Task<IReadOnlyList<AuditLog>> GetRecentAsync(
            int count = 50,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get audit logs by correlation ID (for tracking related operations)
        /// </summary>
        Task<IReadOnlyList<AuditLog>> GetByCorrelationIdAsync(
            string correlationId,
            CancellationToken cancellationToken = default);
    }
}
