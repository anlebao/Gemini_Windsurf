using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// OutboxRepository - EF Core implementation using OutboxMessage as persistence model
/// Maps between OutboxEvent (domain) and OutboxMessage (EF entity)
/// </summary>
public class OutboxRepository : IOutboxRepository
{
    private readonly VanAnDbContext _dbContext;

    public OutboxRepository(VanAnDbContext dbContext)
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

    private static OutboxMessage ToMessage(OutboxEvent e)
    {
        var data = JsonSerializer.Serialize(new
        {
            invoiceId = e.InvoiceId.Value,
            originalData = e.EventData
        });

        return new OutboxMessage
        {
            Id = e.OutboxEventId,
            EventType = e.EventType,
            EventData = data,
            CreatedAt = DateTime.UtcNow,
            TenantId = e.TenantId,
            Status = MapToMessageStatus(e.Status),
            RetryCount = e.RetryCount,
            ProcessedAt = e.ProcessedAt,
            Error = e.ErrorDetails
        };
    }

    private static OutboxEvent ToDomain(OutboxMessage m)
    {
        var invoiceId = ExtractInvoiceId(m.EventData);
        var tenantId = m.TenantId;
        var e = new OutboxEvent(tenantId, invoiceId, m.EventType, m.EventData);

        if (m.Status == OutboxMessageStatus.Processed)
            e.MarkAsProcessed();
        else if (m.Status == OutboxMessageStatus.Failed && m.Error is not null)
        {
            for (int i = 0; i < m.RetryCount; i++)
                e.MarkAsFailed(m.Error);
        }

        return e;
    }

    private static ElectronicInvoiceId ExtractInvoiceId(string eventData)
    {
        try
        {
            var doc = JsonDocument.Parse(eventData);
            if (doc.RootElement.TryGetProperty("invoiceId", out var idProp))
                return new ElectronicInvoiceId(idProp.GetGuid());
        }
        catch { }
        return new ElectronicInvoiceId(Guid.Empty);
    }

    private static OutboxMessageStatus MapToMessageStatus(EventStatus status) => status switch
    {
        EventStatus.Processed => OutboxMessageStatus.Processed,
        EventStatus.Failed => OutboxMessageStatus.Failed,
        _ => OutboxMessageStatus.Pending
    };
}
