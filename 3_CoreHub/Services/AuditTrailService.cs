using Microsoft.AspNetCore.Components.Authorization;
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
        private readonly IFeatureFlagService _featureFlag;   // NEW — Sprint 3 toggle
        private readonly AuditLogQueue _auditQueue;          // NEW — Sprint 3 async queue
        // W4 (P1.1, 2026-10-18): AuthenticationStateProvider fallback — Blazor Server circuit
        // có HttpContext = null; claims principal (gồm claims "client_ip"/"client_ua" gắn lúc
        // sign-in) vẫn resolve được qua circuit → IP/UserAgent ghi được cho thao tác UI.
        private readonly AuthenticationStateProvider? _authStateProvider;

        public AuditTrailService(
            IAuditLogRepository auditLogRepository,
            ITenantProvider tenantProvider,
            ILogger<AuditTrailService> logger,
            IHttpContextAccessor httpContextAccessor,
            IFeatureFlagService featureFlag,
            AuditLogQueue auditQueue,
            AuthenticationStateProvider? authStateProvider = null)
        {
            _auditLogRepository = auditLogRepository;
            _tenantProvider = tenantProvider;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _featureFlag = featureFlag;
            _auditQueue = auditQueue;
            _authStateProvider = authStateProvider;
        }

        public async Task<AuditLog?> LogCreateAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string newValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            // Sprint 3 EXPANDED: toggle gate — trả null khi OFF (không build, không ghi)
            if (!await IsAuditEnabledAsync(entityType, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            RequestContext ctx = await GetRequestContextAsync();

            _logger.LogDebug(
                "Logging CREATE for {EntityType} {EntityId} by user {UserId}",
                entityType, entityId, ctx.UserId);

            var auditLog = AuditLog.ForCreate(
                tenantId,
                entityType,
                entityId,
                newValues,
                ctx.UserId,
                ctx.UserName,
                correlationId,
                ctx.IpAddress,
                ctx.UserAgent);

            // Sprint 3 EXPANDED: hybrid persist (AccountingEntry → sync; khác → async queue)
            return await PersistAsync(auditLog, entityType, cancellationToken);
        }

        public async Task<AuditLog?> LogUpdateAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string oldValues,
            string newValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            // Sprint 3 EXPANDED: toggle gate — trả null khi OFF
            if (!await IsAuditEnabledAsync(entityType, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            RequestContext ctx = await GetRequestContextAsync();

            _logger.LogDebug(
                "Logging UPDATE for {EntityType} {EntityId} by user {UserId}",
                entityType, entityId, ctx.UserId);

            var auditLog = AuditLog.ForUpdate(
                tenantId,
                entityType,
                entityId,
                oldValues,
                newValues,
                ctx.UserId,
                ctx.UserName,
                correlationId,
                ctx.IpAddress,
                ctx.UserAgent);

            return await PersistAsync(auditLog, entityType, cancellationToken);
        }

        public async Task<AuditLog?> LogDeleteAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string oldValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            // Sprint 3 EXPANDED: toggle gate — trả null khi OFF
            if (!await IsAuditEnabledAsync(entityType, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            RequestContext ctx = await GetRequestContextAsync();

            _logger.LogDebug(
                "Logging DELETE for {EntityType} {EntityId} by user {UserId}",
                entityType, entityId, ctx.UserId);

            var auditLog = AuditLog.ForDelete(
                tenantId,
                entityType,
                entityId,
                oldValues,
                ctx.UserId,
                ctx.UserName,
                correlationId,
                ctx.IpAddress,
                ctx.UserAgent);

            return await PersistAsync(auditLog, entityType, cancellationToken);
        }

        public async Task<AuditLog?> LogPeriodCloseAsync(
            AccountingPeriod period,
            string reason,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            // Sprint 3 EXPANDED: toggle gate — PeriodClosing → Audit_Accounting group
            if (!await IsAuditEnabledAsync(AuditableEntityType.PeriodClosing, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            RequestContext ctx = await GetRequestContextAsync();

            _logger.LogInformation(
                "Logging PERIOD CLOSE for {Period} by user {UserId}. Reason: {Reason}",
                period, ctx.UserId, reason);

            var auditLog = AuditLog.ForPeriodClose(
                tenantId,
                period.Year,
                period.Month,
                reason,
                ctx.UserId,
                ctx.UserName,
                correlationId,
                ctx.IpAddress,
                ctx.UserAgent);

            return await PersistAsync(auditLog, AuditableEntityType.PeriodClosing, cancellationToken);
        }

        public async Task<AuditLog?> LogPeriodReopenAsync(
            AccountingPeriod period,
            string reason,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            // Sprint 3 EXPANDED: toggle gate — PeriodClosing → Audit_Accounting group
            if (!await IsAuditEnabledAsync(AuditableEntityType.PeriodClosing, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            RequestContext ctx = await GetRequestContextAsync();

            _logger.LogInformation(
                "Logging PERIOD REOPEN for {Period} by user {UserId}. Reason: {Reason}",
                period, ctx.UserId, reason);

            var auditLog = AuditLog.ForPeriodReopen(
                tenantId,
                period.Year,
                period.Month,
                reason,
                ctx.UserId,
                ctx.UserName,
                correlationId,
                ctx.IpAddress,
                ctx.UserAgent);

            return await PersistAsync(auditLog, AuditableEntityType.PeriodClosing, cancellationToken);
        }

        public async Task<AuditLog?> LogCorrectionAsync(
            Guid originalEntryId,
            string correctionReason,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            // Sprint 3 EXPANDED: toggle gate — AccountingEntry → Audit_Accounting group
            if (!await IsAuditEnabledAsync(AuditableEntityType.AccountingEntry, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            RequestContext ctx = await GetRequestContextAsync();

            _logger.LogInformation(
                "Logging CORRECTION for entry {EntryId} by user {UserId}. Reason: {Reason}",
                originalEntryId, ctx.UserId, correctionReason);

            var auditLog = AuditLog.ForCorrection(
                tenantId,
                originalEntryId,
                correctionReason,
                ctx.UserId,
                ctx.UserName,
                correlationId,
                ctx.IpAddress,
                ctx.UserAgent);

            return await PersistAsync(auditLog, AuditableEntityType.AccountingEntry, cancellationToken);
        }

        /// <summary>
        /// W3 (P0.3): audit hóa đơn điện tử điều chỉnh/thay thế (Điều 10 TT 91/2026).
        /// Toggle gate: ElectronicInvoice — nhóm audit mặc định (không phải Accounting).
        /// </summary>
        public async Task<AuditLog?> LogInvoiceCorrectionAsync(
            Guid originalInvoiceId,
            Guid newInvoiceId,
            string adjustmentType,
            string reason,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            if (!await IsAuditEnabledAsync(AuditableEntityType.ElectronicInvoice, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            RequestContext ctx = await GetRequestContextAsync();

            _logger.LogInformation(
                "Logging INVOICE CORRECTION {Type} for invoice {Original} -> {New} by user {UserId}. Reason: {Reason}",
                adjustmentType, originalInvoiceId, newInvoiceId, ctx.UserId, reason);

            var auditLog = AuditLog.ForInvoiceCorrection(
                tenantId,
                originalInvoiceId,
                newInvoiceId,
                adjustmentType,
                reason,
                ctx.UserId,
                ctx.UserName,
                correlationId,
                ctx.IpAddress,
                ctx.UserAgent);

            return await PersistAsync(auditLog, AuditableEntityType.ElectronicInvoice, cancellationToken);
        }

        public async Task<AuditLog?> LogReversalAsync(
            Guid originalEntryId,
            Guid reversalEntryId,
            string reversalReason,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            // Sprint 3 EXPANDED: toggle gate — AccountingEntry → Audit_Accounting group
            if (!await IsAuditEnabledAsync(AuditableEntityType.AccountingEntry, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            RequestContext ctx = await GetRequestContextAsync();

            _logger.LogInformation(
                "Logging REVERSAL for entry {OriginalEntryId} -> {ReversalEntryId} by user {UserId}. Reason: {Reason}",
                originalEntryId, reversalEntryId, ctx.UserId, reversalReason);

            var auditLog = AuditLog.ForReversal(
                tenantId,
                originalEntryId,
                reversalEntryId,
                reversalReason,
                ctx.UserId,
                ctx.UserName,
                correlationId,
                ctx.IpAddress,
                ctx.UserAgent);

            return await PersistAsync(auditLog, AuditableEntityType.AccountingEntry, cancellationToken);
        }

        public async Task<AuditLogPagedResult> QueryAsync(
            AuditLogQuery query,
            CancellationToken cancellationToken = default)
        {
            // ISSUE #2 FIX: SystemAdmin without tenant_id (not impersonating) sees cross-tenant audit logs.
            // When impersonating, _tenantProvider.HasTenant is true → normal tenant-filtered query.
            if (await IsSystemAdminWithoutTenantAsync())
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

        /// <summary>
        /// Sprint 3 P3.3: Log a security event (failed login, rate limit hit, suspicious activity).
        /// EXPANDED: persisted ASYNC via AuditLogQueue (fire-and-forget) — không chặn login/429.
        /// </summary>
        public async Task<AuditLog?> LogSecurityEventAsync(
            AuditActionType actionType,
            string description,
            string? correlationId = null,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            // Sprint 3 EXPANDED: toggle gate — SecurityEvent → master + Audit_Security
            if (!await IsAuditEnabledAsync(AuditableEntityType.SecurityEvent, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            RequestContext ctx = await GetRequestContextAsync();

            _logger.LogWarning(
                "SECURITY EVENT: {ActionType} — {Description} by user {UserId}",
                actionType, description, ctx.UserId);

            var auditLog = AuditLog.ForSecurityEvent(
                tenantId,
                actionType,
                description,
                ctx.UserId,
                ctx.UserName,
                correlationId,
                ipAddress ?? ctx.IpAddress,
                userAgent ?? ctx.UserAgent);

            // Security → ASYNC fire-and-forget (PersistAsync: SecurityEvent không phải accounting → enqueue)
            return await PersistAsync(auditLog, AuditableEntityType.SecurityEvent, cancellationToken);
        }

        #region Sprint 3 EXPANDED — Toggle Gating + Hybrid Persist

        /// <summary>
        /// Sprint 3 EXPANDED: Audit toggle gate — master switch + group switch theo EntityType.
        /// Audit_Enabled (master, default ON) → Audit_Accounting / Audit_Security / Audit_KhachLink (default ON).
        /// EntityType ngoài 3 nhóm → chỉ phụ thuộc master.
        /// </summary>
        private async Task<bool> IsAuditEnabledAsync(AuditableEntityType entityType, CancellationToken ct)
        {
            // Fix (2026-09-18): fail-safe in background scopes (NATS subscribers / hosted services).
            // FeatureFlagApiClient resolves auth via HttpContext (Razor DI scope) and throws
            // "Do not call GetAuthenticationStateAsync outside of the DI scope for a Razor component"
            // when invoked from PaymentConfirmedSubscriber → aborted GenerateAccountingEntriesAsync
            // after the first entry was persisted. Audit defaults to ENABLED (accounting integrity)
            // with a warning when the flag cannot be resolved.
            try
            {
                if (!await _featureFlag.IsEnabledAsync("Audit_Enabled", defaultWhenMissing: true, ct))
                    return false;

                var groupFlag = entityType switch
                {
                    AuditableEntityType.AccountingEntry or AuditableEntityType.PeriodClosing => "Audit_Accounting",
                    AuditableEntityType.KhachLinkInstance => "Audit_KhachLink",
                    AuditableEntityType.SecurityEvent => "Audit_Security",
                    _ => null
                };
                if (groupFlag == null)
                    return true;
                return await _featureFlag.IsEnabledAsync(groupFlag, defaultWhenMissing: true, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "IsAuditEnabledAsync: feature flag check failed (background scope?) — audit defaults to ENABLED");
                return true;
            }
        }

        /// <summary>
        /// Sprint 3 EXPANDED: Hybrid persist — accounting = SYNC (await DB, toàn vẹn kế toán);
        /// security/khachlink/khác = ASYNC (enqueue, không chặn luồng chính).
        /// </summary>
        private async Task<AuditLog?> PersistAsync(AuditLog auditLog, AuditableEntityType entityType, CancellationToken ct)
        {
            var isAccounting = entityType is AuditableEntityType.AccountingEntry
                or AuditableEntityType.PeriodClosing;
            if (isAccounting)
            {
                return await _auditLogRepository.AddAsync(auditLog);  // SYNC — accounting integrity
            }
            _auditQueue.TryEnqueue(auditLog);  // ASYNC fire-and-forget
            return auditLog;
        }

        #endregion

        #region Helper Methods - Request/User Context (W4 P1.1: IP/UserAgent capture)

        /// <summary>
        /// W4 (P1.1 — 2026-10-18): resolved request context (user + IP + UserAgent).
        /// IP/UserAgent nguồn: HttpContext headers (X-Forwarded-For qua Gateway/nginx → RemoteIpAddress;
        /// User-Agent header) → fallback claims "client_ip"/"client_ua" trên auth cookie (gắn lúc
        /// sign-in ShopERP) → null khi background scope (NATS/hosted — không HttpContext, không claims).
        /// </summary>
        private sealed record RequestContext(
            string UserId,
            string UserName,
            string? IpAddress,
            string? UserAgent);

        /// <summary>
        /// Resolve claims principal: HttpContext.User khi có request thật (API/SSR) —
        /// fallback AuthenticationStateProvider trong Blazor Server circuit
        /// (claims từ auth cookie vẫn resolve được; HttpContext null sau khi circuit thiết lập).
        /// </summary>
        private async Task<ClaimsPrincipal?> GetPrincipalAsync()
        {
            ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                return principal;
            }

            if (_authStateProvider == null)
            {
                return null;
            }

            try
            {
                AuthenticationState state = await _authStateProvider.GetAuthenticationStateAsync();
                return state.User.Identity?.IsAuthenticated == true ? state.User : null;
            }
            catch (InvalidOperationException)
            {
                // Ngoài DI scope Razor (background service) — không có auth state.
                return null;
            }
        }

        private async Task<RequestContext> GetRequestContextAsync()
        {
            HttpContext? http = _httpContextAccessor.HttpContext;
            ClaimsPrincipal? principal = await GetPrincipalAsync();

            string? ip = ExtractIpAddress(http) ?? principal?.FindFirst("client_ip")?.Value;
            string? ua = ExtractUserAgent(http) ?? principal?.FindFirst("client_ua")?.Value;

            return new RequestContext(
                GetUserId(principal),
                GetUserName(principal),
                ip,
                ua);
        }

        /// <summary>IP client: X-Forwarded-For (hop đầu tiên — Gateway/nginx forward) → RemoteIpAddress.</summary>
        private static string? ExtractIpAddress(HttpContext? http)
        {
            if (http == null) return null;
            string? forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }
            return http.Connection.RemoteIpAddress?.ToString();
        }

        private static string? ExtractUserAgent(HttpContext? http)
        {
            if (http == null) return null;
            string? ua = http.Request.Headers.UserAgent;
            return string.IsNullOrWhiteSpace(ua) ? null : ua;
        }

        /// <summary>
        /// User ID từ claims principal.
        /// Falls back to "system" khi không có principal (background services).
        /// </summary>
        private static string GetUserId(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return "system";

            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? "system";
        }

        /// <summary>
        /// User name từ claims principal.
        /// Falls back to "System" khi không có principal.
        /// </summary>
        private static string GetUserName(ClaimsPrincipal? user)
        {
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
        /// W4: resolve principal qua GetRequestContextAsync (circuit-aware).
        /// </summary>
        private async Task<bool> IsSystemAdminWithoutTenantAsync()
        {
            ClaimsPrincipal? user = await GetPrincipalAsync();
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
