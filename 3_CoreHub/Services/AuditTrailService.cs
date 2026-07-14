using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Audit trail service implementation.
    /// Engineering Constitution Compliance:
    /// - Append-only: Create only, no Update or Delete
    /// - All operations automatically capture user context from parameters
    /// - Tenant isolation enforced via repository layer
    /// - Correlation ID support for tracking related operations
    /// </summary>
    public class AuditTrailService : IAuditTrailService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<AuditTrailService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditTrailService(
            IAuditLogRepository auditLogRepository,
            ITenantProvider tenantProvider,
            ILogger<AuditTrailService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _auditLogRepository = auditLogRepository;
            _tenantProvider = tenantProvider;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<AuditLog> LogCreateAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string newValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            var tenantId = new TenantId(_tenantProvider.TenantId);
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();

            _logger.LogDebug(
                "Logging CREATE for {EntityType} {EntityId} by user {UserId}",
                entityType, entityId, userId);

            var auditLog = AuditLog.ForCreate(
                tenantId,
                entityType,
                entityId,
                newValues,
                userId,
                userName,
                correlationId);

            return await _auditLogRepository.AddAsync(auditLog);
        }

        public async Task<AuditLog> LogUpdateAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string oldValues,
            string newValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            var tenantId = new TenantId(_tenantProvider.TenantId);
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();

            _logger.LogDebug(
                "Logging UPDATE for {EntityType} {EntityId} by user {UserId}",
                entityType, entityId, userId);

            var auditLog = AuditLog.ForUpdate(
                tenantId,
                entityType,
                entityId,
                oldValues,
                newValues,
                userId,
                userName,
                correlationId);

            return await _auditLogRepository.AddAsync(auditLog);
        }

        public async Task<AuditLog> LogDeleteAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string oldValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            var tenantId = new TenantId(_tenantProvider.TenantId);
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();

            _logger.LogDebug(
                "Logging DELETE for {EntityType} {EntityId} by user {UserId}",
                entityType, entityId, userId);

            var auditLog = AuditLog.ForDelete(
                tenantId,
                entityType,
                entityId,
                oldValues,
                userId,
                userName,
                correlationId);

            return await _auditLogRepository.AddAsync(auditLog);
        }

        public async Task<AuditLog> LogPeriodCloseAsync(
            AccountingPeriod period,
            string reason,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            var tenantId = new TenantId(_tenantProvider.TenantId);
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();

            _logger.LogInformation(
                "Logging PERIOD CLOSE for {Period} by user {UserId}. Reason: {Reason}",
                period, userId, reason);

            var auditLog = AuditLog.ForPeriodClose(
                tenantId,
                period.Year,
                period.Month,
                reason,
                userId,
                userName,
                correlationId);

            return await _auditLogRepository.AddAsync(auditLog);
        }

        public async Task<AuditLog> LogPeriodReopenAsync(
            AccountingPeriod period,
            string reason,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            var tenantId = new TenantId(_tenantProvider.TenantId);
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();

            _logger.LogInformation(
                "Logging PERIOD REOPEN for {Period} by user {UserId}. Reason: {Reason}",
                period, userId, reason);

            var auditLog = AuditLog.ForPeriodReopen(
                tenantId,
                period.Year,
                period.Month,
                reason,
                userId,
                userName,
                correlationId);

            return await _auditLogRepository.AddAsync(auditLog);
        }

        public async Task<AuditLog> LogCorrectionAsync(
            Guid originalEntryId,
            string correctionReason,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            var tenantId = new TenantId(_tenantProvider.TenantId);
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();

            _logger.LogInformation(
                "Logging CORRECTION for entry {EntryId} by user {UserId}. Reason: {Reason}",
                originalEntryId, userId, correctionReason);

            var auditLog = AuditLog.ForCorrection(
                tenantId,
                originalEntryId,
                correctionReason,
                userId,
                userName,
                correlationId);

            return await _auditLogRepository.AddAsync(auditLog);
        }

        public async Task<AuditLog> LogReversalAsync(
            Guid originalEntryId,
            Guid reversalEntryId,
            string reversalReason,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            var tenantId = new TenantId(_tenantProvider.TenantId);
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();

            _logger.LogInformation(
                "Logging REVERSAL for entry {OriginalEntryId} -> {ReversalEntryId} by user {UserId}. Reason: {Reason}",
                originalEntryId, reversalEntryId, userId, reversalReason);

            var auditLog = AuditLog.ForReversal(
                tenantId,
                originalEntryId,
                reversalEntryId,
                reversalReason,
                userId,
                userName,
                correlationId);

            return await _auditLogRepository.AddAsync(auditLog);
        }

        public async Task<AuditLogPagedResult> QueryAsync(
            AuditLogQuery query,
            CancellationToken cancellationToken = default)
        {
            // ISSUE #2 FIX: SystemAdmin without tenant_id (not impersonating) sees cross-tenant audit logs.
            // When impersonating, _tenantProvider.HasTenant is true → normal tenant-filtered query.
            if (IsSystemAdminWithoutTenant())
            {
                _logger.LogDebug(
                    "Querying audit logs cross-tenant (SystemAdmin platform mode)");

                return await _auditLogRepository.GetByQueryCrossTenantAsync(query);
            }

            // Normal tenant-filtered query
            var tenantId = new TenantId(_tenantProvider.TenantId);
            var queryWithTenant = query with { TenantId = tenantId };

            _logger.LogDebug(
                "Querying audit logs for tenant {TenantId} with filters",
                tenantId.Value);

            return await _auditLogRepository.GetByQueryAsync(queryWithTenant);
        }

        public async Task<IReadOnlyList<AuditLog>> GetEntityHistoryAsync(
            AuditableEntityType entityType,
            Guid entityId,
            int maxResults = 100,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug(
                "Getting audit history for {EntityType} {EntityId}",
                entityType, entityId);

            return await _auditLogRepository.GetByEntityAsync(entityType, entityId, maxResults);
        }

        public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(
            int count = 50,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Getting {Count} recent audit logs", count);

            return await _auditLogRepository.GetRecentAsync(count);
        }

        public async Task<IReadOnlyList<AuditLog>> GetByCorrelationIdAsync(
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug(
                "Getting audit logs by correlation ID {CorrelationId}",
                correlationId);

            return await _auditLogRepository.GetByCorrelationIdAsync(correlationId);
        }

        #region Helper Methods - User Context

        /// <summary>
        /// Gets the current user ID from HTTP context claims.
        /// Falls back to "system" when no HTTP context is available (e.g. background services).
        /// </summary>
        private string GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return "system";

            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? "system";
        }

        /// <summary>
        /// Gets the current user name from HTTP context claims.
        /// Falls back to "System" when no HTTP context is available.
        /// </summary>
        private string GetCurrentUserName()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return "System";

            return user.FindFirst(ClaimTypes.Name)?.Value
                ?? user.FindFirst("DisplayName")?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? "System";
        }

        /// <summary>
        /// Checks if the current user is a SystemAdmin without a valid tenant_id (platform mode).
        /// In this mode, audit queries should be cross-tenant.
        /// </summary>
        private bool IsSystemAdminWithoutTenant()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return false;

            var isSystemAdmin = user.IsInRole("SystemAdmin");
            if (!isSystemAdmin)
                return false;

            // SystemAdmin with valid tenant_id (impersonating) → tenant-filtered query
            return !_tenantProvider.HasTenant;
        }

        #endregion
    }
}
