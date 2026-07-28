using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// OutboxRepository - EF Core implementation using OutboxMessage as persistence model
/// Maps between OutboxEvent (domain) and OutboxMessage (EF entity)
///
/// W-1-T1: Inject IVanAnDbContext (not VanAnDbContext) so DI resolves correctly per scope:
///   - ShopERP scope → ShopERPDbContext (SQLite) — Outbox lives in SQLite for offline-first
///   - Gateway scope → VanAnDbContext (PostgreSQL) — for direct PostgreSQL access if needed
/// </summary>
public class OutboxRepository : IOutboxRepository
{
    private readonly IVanAnDbContext _dbContext;
    private readonly ILogger<OutboxRepository>? _logger;

    public OutboxRepository(IVanAnDbContext dbContext, ILogger<OutboxRepository>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task EnqueueAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
    {
        var message = ToMessage(outboxEvent);
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<List<OutboxEvent>> GetPendingEventsAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters: OutboxMessages is IMustHaveTenant, but NatsSyncWorker processes
        // events across ALL tenants. Without this, the global TenantId query filter excludes
        // all Outbox messages (CurrentTenantIdValue = Guid.Empty in background worker scope).
        var messages = await _dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return messages.Select(ToDomain).ToList();
    }

    public async Task MarkAsProcessedAsync(Guid outboxEventId, CancellationToken cancellationToken = default)
    {
        // DIAGNOSTIC: log incoming outboxEventId + DB path for evidence gathering
        var dbConnStr = _dbContext.GetType().GetProperty("Database")?.GetValue(_dbContext)?.ToString();
        _logger?.LogWarning(
            "DIAG MarkAsProcessedAsync: outboxEventId={Id}, dbContextType={CtxType}, connection={Conn}",
            outboxEventId, _dbContext.GetType().FullName, dbConnStr ?? "unknown");

        // First: check if the row exists at all
        var existsBefore = await _dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Id == outboxEventId)
            .Select(m => new { m.Id, m.Status, m.TenantId })
            .FirstOrDefaultAsync(cancellationToken);

        _logger?.LogWarning(
            "DIAG MarkAsProcessedAsync: row lookup before UPDATE = {Found}, id={Id}, status={Status}, tenantId={TenantId}",
            existsBefore != null, outboxEventId, existsBefore?.Status, existsBefore?.TenantId);

        var now = DateTime.UtcNow;
        var rowsAffected = await _dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.Id == outboxEventId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxMessageStatus.Processed)
                .SetProperty(m => m.ProcessedAt, now)
                .SetProperty(m => m.Error, (string?)null)
                .SetProperty(m => m.NextRetryAt, (DateTime?)null),
                cancellationToken);

        _logger?.LogWarning(
            "DIAG MarkAsProcessedAsync: rowsAffected={RowsAffected} for outboxEventId={Id}",
            rowsAffected, outboxEventId);

        if (rowsAffected == 0)
        {
            // Fallback: try raw SQL UPDATE via DbContext's Database facade
            _logger?.LogError(
                "DIAG MarkAsProcessedAsync: 0 rows affected! Trying raw SQL fallback for id={Id}",
                outboxEventId);
            if (_dbContext is DbContext efCtx)
            {
                await efCtx.Database.ExecuteSqlRawAsync(
                    "UPDATE OutboxMessages SET Status = 2, ProcessedAt = {0}, Error = NULL, NextRetryAt = NULL WHERE Id = {1}",
                    now, outboxEventId);
            }
        }
    }

    public async Task MarkAsFailedAsync(Guid outboxEventId, string errorDetails, CancellationToken cancellationToken = default)
    {
        // FIX: Use ExecuteUpdateAsync for the same reason as MarkAsProcessedAsync —
        // guarantees an UPDATE statement is generated regardless of change-tracker state.
        // Load current RetryCount (AsNoTracking) to compute exponential backoff client-side,
        // then bulk-UPDATE with fixed values — avoids server-side Math.Pow translation issues.
        var current = await _dbContext.OutboxMessages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.Id == outboxEventId)
            .Select(m => new { m.RetryCount })
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null) return;

        var newRetryCount = current.RetryCount + 1;
        var delayMinutes = Math.Min(60, Math.Pow(2, newRetryCount));
        var nextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);

        await _dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.Id == outboxEventId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxMessageStatus.Failed)
                .SetProperty(m => m.Error, errorDetails)
                .SetProperty(m => m.RetryCount, newRetryCount)
                .SetProperty(m => m.NextRetryAt, nextRetryAt),
                cancellationToken);
    }

    public async Task<OutboxEvent?> GetByIdAsync(Guid outboxEventId, CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == outboxEventId, cancellationToken);

        return message is null ? null : ToDomain(message);
    }

    /// <summary>
    /// W-1-T2: Generalized mapping — stores raw EventData without wrapping in {invoiceId, originalData}.
    /// Works for any event type (Order, Invoice, Customer, etc.).
    /// InvoiceId is preserved on the OutboxEvent domain side; persistence model only stores EventData.
    /// </summary>
    private static OutboxMessage ToMessage(OutboxEvent e)
    {
        return new OutboxMessage
        {
            Id = e.OutboxEventId,
            EventType = e.EventType,
            EventData = e.EventData,
            CreatedAt = DateTime.UtcNow,
            TenantId = e.TenantId,
            Status = MapToMessageStatus(e.Status),
            RetryCount = e.RetryCount,
            ProcessedAt = e.ProcessedAt,
            Error = e.ErrorDetails,
            RoutingKey = e.RoutingKey
        };
    }

    /// <summary>
    /// W-1-T2: Reconstruct OutboxEvent from persistence model.
    /// InvoiceId is set to Guid.Empty for non-invoice events — subscribers parse EventData for type-specific fields.
    /// RC-1 fix: Preserve original OutboxEventId = m.Id so MarkAsProcessedAsync can find the row.
    /// </summary>
    private static OutboxEvent ToDomain(OutboxMessage m)
    {
        var e = new OutboxEvent(m.TenantId, new ElectronicInvoiceId(Guid.Empty), m.EventType, m.EventData, m.RoutingKey);

        // Preserve original ID from persistence model (constructor generates new Guid — would break MarkAsProcessedAsync)
        typeof(OutboxEvent).GetProperty("OutboxEventId")?.SetValue(e, m.Id);

        if (m.Status == OutboxMessageStatus.Processed)
            e.MarkAsProcessed();
        else if (m.Status == OutboxMessageStatus.Failed && m.Error is not null)
        {
            for (int i = 0; i < m.RetryCount; i++)
                e.MarkAsFailed(m.Error);
        }

        return e;
    }

    private static OutboxMessageStatus MapToMessageStatus(EventStatus status) => status switch
    {
        EventStatus.Processed => OutboxMessageStatus.Processed,
        EventStatus.Failed => OutboxMessageStatus.Failed,
        _ => OutboxMessageStatus.Pending
    };
}
