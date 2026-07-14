using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;

namespace VanAn.CoreHub.Infrastructure.Repositories
{
    /// <summary>
    /// EF Core implementation of IAuditLogRepository
    /// Engineering Constitution Compliance:
    /// - Append-only: Create only, no Update or Delete methods
    /// - ALWAYS filter by tenant
    /// - Optimized for audit trail query patterns
    /// </summary>
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IAccountingDbContext _context;
        private readonly VanAnDbContext? _vanAnContext;

        public AuditLogRepository(IAccountingDbContext context)
        {
            _context = context;
            _vanAnContext = context as VanAnDbContext;
        }

        /// <summary>
        /// Current tenant ID — evaluated lazily at query time (not construction time).
        /// This ensures controller-side _tenantProvider.SetTenant() is reflected
        /// when the repository methods execute (fixes webhook AuditLog tenant mismatch).
        /// </summary>
        private Guid CurrentTenantId => _vanAnContext?.CurrentTenantId ?? Guid.Empty;

        public async Task<AuditLog?> GetByIdAsync(Guid id)
        {
            return await _context.AuditLogs
                .Where(a => a.Id == id && a.TenantId == new TenantId(CurrentTenantId))
                .FirstOrDefaultAsync();
        }

        public async Task<AuditLogPagedResult> GetByQueryAsync(AuditLogQuery query)
        {
            // Security: Always enforce tenant filter
            var dbQuery = _context.AuditLogs
                .Where(a => a.TenantId == new TenantId(CurrentTenantId));

            // Apply date range filters
            if (query.FromDate.HasValue)
            {
                dbQuery = dbQuery.Where(a => a.CreatedAt >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                dbQuery = dbQuery.Where(a => a.CreatedAt <= query.ToDate.Value);
            }

            // Apply action type filter
            if (query.Action.HasValue)
            {
                dbQuery = dbQuery.Where(a => a.Action == query.Action.Value);
            }

            // Apply entity type filter
            if (query.EntityType.HasValue)
            {
                dbQuery = dbQuery.Where(a => a.EntityType == query.EntityType.Value);
            }

            // Apply entity ID filter
            if (query.EntityId.HasValue)
            {
                dbQuery = dbQuery.Where(a => a.EntityId == query.EntityId.Value);
            }

            // Apply user ID filter
            if (!string.IsNullOrWhiteSpace(query.UserId))
            {
                dbQuery = dbQuery.Where(a => a.UserId == query.UserId);
            }

            // Apply correlation ID filter
            if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            {
                dbQuery = dbQuery.Where(a => a.CorrelationId == query.CorrelationId);
            }

            // Apply search term (searches in Reason, UserName, or NewValues/OldValues)
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                var searchLower = query.SearchTerm.ToLower();
                dbQuery = dbQuery.Where(a =>
                    (a.Reason != null && a.Reason.ToLower().Contains(searchLower)) ||
                    (a.UserName != null && a.UserName.ToLower().Contains(searchLower)) ||
                    (a.NewValues != null && a.NewValues.ToLower().Contains(searchLower)) ||
                    (a.OldValues != null && a.OldValues.ToLower().Contains(searchLower)));
            }

            // Get total count for pagination
            var totalCount = await dbQuery.CountAsync();

            // Apply ordering and pagination
            var items = await dbQuery
                .OrderByDescending(a => a.CreatedAt)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return new AuditLogPagedResult
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

        /// <summary>
        /// Cross-tenant query for SystemAdmin — bypasses tenant filter via IgnoreQueryFilters().
        /// Security: only called from AuditTrailService when user is SystemAdmin without impersonation.
        /// </summary>
        public async Task<AuditLogPagedResult> GetByQueryCrossTenantAsync(AuditLogQuery query)
        {
            // Cross-tenant: IgnoreQueryFilters to bypass global TenantId filter
            var dbQuery = _context.AuditLogs.IgnoreQueryFilters();

            // Apply date range filters
            if (query.FromDate.HasValue)
                dbQuery = dbQuery.Where(a => a.CreatedAt >= query.FromDate.Value);

            if (query.ToDate.HasValue)
                dbQuery = dbQuery.Where(a => a.CreatedAt <= query.ToDate.Value);

            if (query.Action.HasValue)
                dbQuery = dbQuery.Where(a => a.Action == query.Action.Value);

            if (query.EntityType.HasValue)
                dbQuery = dbQuery.Where(a => a.EntityType == query.EntityType.Value);

            if (query.EntityId.HasValue)
                dbQuery = dbQuery.Where(a => a.EntityId == query.EntityId.Value);

            if (!string.IsNullOrWhiteSpace(query.UserId))
                dbQuery = dbQuery.Where(a => a.UserId == query.UserId);

            if (!string.IsNullOrWhiteSpace(query.CorrelationId))
                dbQuery = dbQuery.Where(a => a.CorrelationId == query.CorrelationId);

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                var searchLower = query.SearchTerm.ToLower();
                dbQuery = dbQuery.Where(a =>
                    (a.Reason != null && a.Reason.ToLower().Contains(searchLower)) ||
                    (a.UserName != null && a.UserName.ToLower().Contains(searchLower)) ||
                    (a.NewValues != null && a.NewValues.ToLower().Contains(searchLower)) ||
                    (a.OldValues != null && a.OldValues.ToLower().Contains(searchLower)));
            }

            var totalCount = await dbQuery.CountAsync();

            var items = await dbQuery
                .OrderByDescending(a => a.CreatedAt)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return new AuditLogPagedResult
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

        public async Task<IReadOnlyList<AuditLog>> GetByEntityAsync(
            AuditableEntityType entityType, 
            Guid entityId, 
            int maxResults = 100)
        {
            return await _context.AuditLogs
                .Where(a => a.TenantId == new TenantId(CurrentTenantId) &&
                           a.EntityType == entityType &&
                           a.EntityId == entityId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(maxResults)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<AuditLog>> GetByCorrelationIdAsync(string correlationId)
        {
            // Security: Also filter by tenant for data isolation
            return await _context.AuditLogs
                .Where(a => a.TenantId == new TenantId(CurrentTenantId) &&
                           a.CorrelationId == correlationId)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<AuditLog> AddAsync(AuditLog auditLog)
        {
            // Security: Ensure tenant ID is set correctly
            if (auditLog.TenantId != new TenantId(CurrentTenantId))
            {
                throw new InvalidOperationException("AuditLog tenant ID does not match current tenant");
            }

            await _context.AuditLogs.AddAsync(auditLog);
            await _context.SaveChangesAsync();

            return auditLog;
        }

        public async Task<IReadOnlyList<AuditLog>> AddRangeAsync(IEnumerable<AuditLog> auditLogs)
        {
            var auditLogList = auditLogs.ToList();

            // Security: Validate all audit logs belong to current tenant
            foreach (var auditLog in auditLogList)
            {
                if (auditLog.TenantId != new TenantId(CurrentTenantId))
                {
                    throw new InvalidOperationException("AuditLog tenant ID does not match current tenant");
                }
            }

            await _context.AuditLogs.AddRangeAsync(auditLogList);
            await _context.SaveChangesAsync();

            return auditLogList;
        }

        public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count = 50)
        {
            return await _context.AuditLogs
                .Where(a => a.TenantId == new TenantId(CurrentTenantId))
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<int> GetCountAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.AuditLogs
                .Where(a => a.TenantId == new TenantId(CurrentTenantId));

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(a => a.CreatedAt <= toDate.Value);
            }

            return await query.CountAsync();
        }
    }
}
