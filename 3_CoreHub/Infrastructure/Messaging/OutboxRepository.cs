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
/// ROOT CAUSE FIX (A2 — outbox stuck loop):
///   EF Core's SQLite provider sends Guid parameters as UPPERCASE strings, but some OutboxMessage
///   rows have lowercase Ids (from NATS sync / JSON deserialization). SQLite's default BINARY
///   collation is case-sensitive, so WHERE Id = @param (UPPERCASE) doesn't match lowercase rows.
///   This caused ExecuteUpdateAsync to return 0 rowsAffected → NatsSyncWorker re-published the
///   same event indefinitely.
///
///   Fix: ALL Id-based queries use raw SQL with `COLLATE NOCASE` for case-insensitive Guid matching.
///   This is correct because Guids are case-insensitive by definition in .NET (Guid.Parse is case-insensitive).
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
        // ROOT CAUSE FIX: Use raw SQL with COLLATE NOCASE for case-insensitive Guid matching.
        // EF Core's SQLite provider sends Guid parameters as UPPERCASE, but some rows have lowercase Ids.
        // SQLite BINARY collation is case-sensitive → 0 rows affected → infinite loop.
        // COLLATE NOCASE makes the WHERE clause case-insensitive, matching .NET Guid semantics.
        var now = DateTime.UtcNow;
        var idStr = outboxEventId.ToString("D").ToUpperInvariant();

        if (_dbContext is not DbContext efCtx)
        {
            _logger?.LogError("MarkAsProcessedAsync: DbContext is not EF Core DbContext — cannot execute raw SQL");
            return;
        }

        var rowsAffected = await efCtx.Database.ExecuteSqlRawAsync(
            "UPDATE OutboxMessages SET Status = 2, ProcessedAt = {0}, Error = NULL, NextRetryAt = NULL " +
            "WHERE Id COLLATE NOCASE = {1}",
            new object[] { now, idStr },
            cancellationToken);

        if (rowsAffected == 0)
        {
            _logger?.LogError(
                "MarkAsProcessedAsync: 0 rows affected even with COLLATE NOCASE for id={Id} — row may not exist",
                idStr);
        }
        else
        {
            _logger?.LogDebug(
                "MarkAsProcessedAsync: {RowsAffected} row(s) updated for id={Id}",
                rowsAffected, idStr);
        }
    }

    public async Task MarkAsFailedAsync(Guid outboxEventId, string errorDetails, CancellationToken cancellationToken = default)
    {
        // ROOT CAUSE FIX: Same COLLATE NOCASE approach as MarkAsProcessedAsync.
        // Load RetryCount first (by Status-based query, no Guid case issue), then raw SQL UPDATE.
        var idStr = outboxEventId.ToString("D").ToUpperInvariant();

        if (_dbContext is not DbContext efCtx)
        {
            _logger?.LogError("MarkAsFailedAsync: DbContext is not EF Core DbContext — cannot execute raw SQL");
            return;
        }

        // Load current RetryCount using raw SQL with COLLATE NOCASE
        var retryCount = await efCtx.Database.SqlQueryRaw<int>(
            "SELECT RetryCount FROM OutboxMessages WHERE Id COLLATE NOCASE = {0}",
            idStr)
            .FirstOrDefaultAsync(cancellationToken);

        var newRetryCount = retryCount + 1;
        var delayMinutes = Math.Min(60, Math.Pow(2, newRetryCount));
        var nextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);

        await efCtx.Database.ExecuteSqlRawAsync(
            "UPDATE OutboxMessages SET Status = 3, Error = {0}, RetryCount = {1}, NextRetryAt = {2} " +
            "WHERE Id COLLATE NOCASE = {3}",
            new object[] { errorDetails, newRetryCount, nextRetryAt, idStr },
            cancellationToken);
    }

    public async Task<OutboxEvent?> GetByIdAsync(Guid outboxEventId, CancellationToken cancellationToken = default)
    {
        // ROOT CAUSE FIX: Use raw SQL with COLLATE NOCASE for case-insensitive Guid lookup.
        var idStr = outboxEventId.ToString("D").ToUpperInvariant();

        if (_dbContext is not DbContext efCtx)
            return null;

        var message = await efCtx.Set<OutboxMessage>()
            .FromSqlRaw("SELECT * FROM OutboxMessages WHERE Id COLLATE NOCASE = {0}", idStr)
            .AsNoTracking()
            .IgnoreQueryFilters()
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
