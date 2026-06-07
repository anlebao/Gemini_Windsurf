using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// OutboxRepository - Stub implementation for OutboxEvent repository
/// TODO: Implement with actual database repository (EF Core)
/// </summary>
public class OutboxRepository : IOutboxRepository
{
    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
    {
        // Stub: Add outbox event to database
        // TODO: Implement with actual EF Core DbContext
        await Task.CompletedTask;
    }

    public async Task<List<OutboxEvent>> GetPendingEventsAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        // Stub: Get pending outbox events
        // TODO: Implement with actual EF Core query
        return await Task.FromResult(new List<OutboxEvent>());
    }

    public async Task MarkAsProcessedAsync(Guid outboxEventId, CancellationToken cancellationToken = default)
    {
        // Stub: Mark outbox event as processed
        // TODO: Implement with actual EF Core update
        await Task.CompletedTask;
    }

    public async Task MarkAsFailedAsync(Guid outboxEventId, string errorDetails, CancellationToken cancellationToken = default)
    {
        // Stub: Mark outbox event as failed
        // TODO: Implement with actual EF Core update
        await Task.CompletedTask;
    }

    public async Task<OutboxEvent?> GetByIdAsync(Guid outboxEventId, CancellationToken cancellationToken = default)
    {
        // Stub: Get outbox event by ID
        // TODO: Implement with actual EF Core query
        return await Task.FromResult<OutboxEvent?>(null);
    }
}
