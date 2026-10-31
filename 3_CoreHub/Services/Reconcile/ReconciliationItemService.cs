using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Reconcile
{
    /// <summary>
    /// SRS v1.2 W5/F (M10 — FR-RECNC-005): workflow xử lý chênh lệch tra soát.
    /// Emit/upsert từ ReconcileResult (Warning/Error → item, dedup per check+period);
    /// check pass lần sau → auto-resolve. Assign/Resolve/Dismiss thủ công trên UI.
    /// </summary>
    public interface IReconciliationItemService
    {
        /// <summary>Emit items từ kết quả tra soát — tạo mới item Open cho check Warning/Error
        /// (dedup theo code+period), auto-resolve item Open/Investigating của check đã Pass/Info.</summary>
        Task<int> EmitFromReconcileAsync(ReconcileResult result, CancellationToken ct = default);
        Task<IReadOnlyList<ReconciliationItemEntity>> ListAsync(TenantId tenantId,
            string? status = null, CancellationToken ct = default);
        Task AssignAsync(Guid itemId, string assignedTo, CancellationToken ct = default);
        Task ResolveAsync(Guid itemId, string resolvedBy, string? note = null, CancellationToken ct = default);
        Task DismissAsync(Guid itemId, string resolvedBy, string? note = null, CancellationToken ct = default);
    }

    public sealed class ReconciliationItemService(
        IAccountingDbContext dbContext,
        ILogger<ReconciliationItemService> logger) : IReconciliationItemService
    {
        private readonly IAccountingDbContext _dbContext = dbContext;
        private readonly ILogger<ReconciliationItemService> _logger = logger;

        /// <inheritdoc />
        public async Task<int> EmitFromReconcileAsync(ReconcileResult result, CancellationToken ct = default)
        {
            if (result.Checks.Count == 0)
            {
                return 0;
            }

            // Period "YYYY-MM" → year/month.
            string[] parts = result.Period.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int year) || !int.TryParse(parts[1], out int month))
            {
                return 0;
            }

            var tenantId = new TenantId(result.TenantId);
            var existing = await _dbContext.ReconciliationItems
                .IgnoreQueryFilters()
                .Where(i => i.TenantId == tenantId && i.PeriodYear == year && i.PeriodMonth == month)
                .ToListAsync(ct);

            var failing = result.Checks
                .Where(c => c.Level is ReconcileLevel.Warning or ReconcileLevel.Error)
                .ToList();
            var passing = result.Checks
                .Where(c => c.Level is ReconcileLevel.Pass or ReconcileLevel.Info)
                .Select(c => c.Code)
                .ToHashSet();

            int created = 0;
            foreach (var check in failing)
            {
                if (existing.Any(i => i.CheckCode == check.Code && i.Status is "Open" or "Investigating"))
                {
                    continue; // dedup — item đã mở cho check+period này
                }
                var item = new ReconciliationItemEntity(tenantId, check.Code, year, month,
                    check.Level == ReconcileLevel.Error ? "error" : "warning",
                    $"{check.Name}: {check.Summary}");
                _dbContext.ReconciliationItems.Add(item);
                created++;
            }

            // Auto-resolve items của check đã pass (T6 — optional per card).
            int autoResolved = 0;
            foreach (var item in existing.Where(i => passing.Contains(i.CheckCode) && i.Status is "Open" or "Investigating"))
            {
                item.AutoResolve();
                autoResolved++;
            }

            if (created > 0 || autoResolved > 0)
            {
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "ReconciliationItems emitted: tenant {TenantId} {Period} — {Created} created, {AutoResolved} auto-resolved",
                    result.TenantId, result.Period, created, autoResolved);
            }
            return created;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ReconciliationItemEntity>> ListAsync(TenantId tenantId,
            string? status = null, CancellationToken ct = default)
        {
            var query = _dbContext.ReconciliationItems
                .Where(i => i.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(i => i.Status == status);
            }
            return await query
                .OrderByDescending(i => i.PeriodYear).ThenByDescending(i => i.PeriodMonth)
                .ThenBy(i => i.CheckCode)
                .ToListAsync(ct);
        }

        /// <inheritdoc />
        public async Task AssignAsync(Guid itemId, string assignedTo, CancellationToken ct = default)
        {
            var item = await GetTrackedAsync(itemId, ct);
            item.Assign(assignedTo);
            await _dbContext.SaveChangesAsync(ct);
        }

        /// <inheritdoc />
        public async Task ResolveAsync(Guid itemId, string resolvedBy, string? note = null, CancellationToken ct = default)
        {
            var item = await GetTrackedAsync(itemId, ct);
            item.Resolve(resolvedBy, note);
            await _dbContext.SaveChangesAsync(ct);
        }

        /// <inheritdoc />
        public async Task DismissAsync(Guid itemId, string resolvedBy, string? note = null, CancellationToken ct = default)
        {
            var item = await GetTrackedAsync(itemId, ct);
            item.Dismiss(resolvedBy, note);
            await _dbContext.SaveChangesAsync(ct);
        }

        private async Task<ReconciliationItemEntity> GetTrackedAsync(Guid itemId, CancellationToken ct)
        {
            var item = await _dbContext.ReconciliationItems
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == itemId, ct);
            return item ?? throw new KeyNotFoundException($"ReconciliationItem {itemId} not found");
        }
    }
}
