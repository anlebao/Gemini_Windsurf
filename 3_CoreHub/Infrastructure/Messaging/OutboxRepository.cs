using Microsoft.EntityFrameworkCore;
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

    public OutboxRepository(IVanAnDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
    {
        var message = ToMessage(outboxEvent);
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<List<OutboxEvent>> GetPendingEventsAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return messages.Select(ToDomain).ToList();
    }

    public async Task MarkAsProcessedAsync(Guid outboxEventId, CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == outboxEventId, cancellationToken);

        if (message is null) return;

        message.MarkAsProcessed();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAsFailedAsync(Guid outboxEventId, string errorDetails, CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == outboxEventId, cancellationToken);

        if (message is null) return;

        message.MarkAsFailed(errorDetails);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<OutboxEvent?> GetByIdAsync(Guid outboxEventId, CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.OutboxMessages
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
            Error = e.ErrorDetails
        };
    }

    /// <summary>
    /// W-1-T2: Reconstruct OutboxEvent from persistence model.
    /// InvoiceId is set to Guid.Empty for non-invoice events — subscribers parse EventData for type-specific fields.
    /// </summary>
    private static OutboxEvent ToDomain(OutboxMessage m)
    {
        var e = new OutboxEvent(m.TenantId, new ElectronicInvoiceId(Guid.Empty), m.EventType, m.EventData);

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
