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
        /// Log entity creation
        /// </summary>
        Task<AuditLog> LogCreateAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string newValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log entity update
        /// </summary>
        Task<AuditLog> LogUpdateAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string oldValues,
            string newValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log entity deletion (soft delete)
        /// </summary>
        Task<AuditLog> LogDeleteAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string oldValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log period closing with reason
        /// </summary>
        Task<AuditLog> LogPeriodCloseAsync(
            AccountingPeriod period,
            string reason,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log period reopening with reason
        /// </summary>
        Task<AuditLog> LogPeriodReopenAsync(
            AccountingPeriod period,
            string reason,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log correction entry
        /// </summary>
        Task<AuditLog> LogCorrectionAsync(
            Guid originalEntryId,
            string correctionReason,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Log reversal entry
        /// </summary>
        Task<AuditLog> LogReversalAsync(
            Guid originalEntryId,
            Guid reversalEntryId,
            string reversalReason,
            string? correlationId = null,
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
