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
///
/// CROSS-PROVIDER FIX (2026-07-28):
///   Previous raw SQL used unquoted table names ("OutboxMessages") + SQLite-only COLLATE NOCASE.
///   PostgreSQL lowercases unquoted identifiers → "outboxmessages" does not exist → 42P01 error.
///   Fix: Use EF Core LINQ queries (ExecuteUpdateAsync/Where) which handle provider-specific
///   identifier quoting automatically. Works on both PostgreSQL and SQLite.
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
        //
        // Query by Status (integer comparison — no Guid case issue) → safe with EF Core LINQ.
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
        if (_dbContext is not DbContext efCtx)
        {
            _logger?.LogError("MarkAsProcessedAsync: DbContext is not EF Core DbContext");
            return;
        }

        var now = DateTime.UtcNow;
        var rowsAffected = await efCtx.Set<OutboxMessage>()
            .IgnoreQueryFilters()
            .Where(m => m.Id == outboxEventId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxMessageStatus.Processed)
                .SetProperty(m => m.ProcessedAt, now)
                .SetProperty(m => m.Error, (string?)null)
                .SetProperty(m => m.NextRetryAt, (DateTime?)null),
                cancellationToken);

        if (rowsAffected == 0)
        {
            _logger?.LogError(
                "MarkAsProcessedAsync: 0 rows affected for id={Id} — row may not exist",
                outboxEventId);
        }
        else
        {
            _logger?.LogDebug(
                "MarkAsProcessedAsync: {RowsAffected} row(s) updated for id={Id}",
                rowsAffected, outboxEventId);
        }
    }

    public async Task MarkAsFailedAsync(Guid outboxEventId, string errorDetails, CancellationToken cancellationToken = default)
    {
        if (_dbContext is not DbContext efCtx)
        {
            _logger?.LogError("MarkAsFailedAsync: DbContext is not EF Core DbContext");
            return;
        }

        var message = await efCtx.Set<OutboxMessage>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Id == outboxEventId)
            .FirstOrDefaultAsync(cancellationToken);

        if (message == null)
        {
            _logger?.LogError("MarkAsFailedAsync: row not found for id={Id}", outboxEventId);
            return;
        }

        var newRetryCount = message.RetryCount + 1;
        var delayMinutes = Math.Min(60, Math.Pow(2, newRetryCount));
        var nextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);

        await efCtx.Set<OutboxMessage>()
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
        if (_dbContext is not DbContext efCtx)
            return null;

        var message = await efCtx.Set<OutboxMessage>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Id == outboxEventId)
            .FirstOrDefaultAsync(cancellationToken);

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
            RoutingKey = e.RoutingKey,
            CorrelationId = e.CorrelationId  // VALCN v2.0 Phase 1 — propagate for traceability
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
