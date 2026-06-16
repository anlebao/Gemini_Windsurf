using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// IOutboxRepository - Repository for OutboxEvent entities
/// Atomic transaction support for Invoice + Outbox
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Enqueue outbox event into change tracker (does NOT call SaveChangesAsync).
    /// Caller owns the Unit of Work and must commit the transaction.
    /// </summary>
    Task EnqueueAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending outbox events for processing
    /// </summary>
    Task<List<OutboxEvent>> GetPendingEventsAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark outbox event as processed
    /// </summary>
    Task MarkAsProcessedAsync(
        Guid outboxEventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark outbox event as failed with retry increment
    /// </summary>
    Task MarkAsFailedAsync(
        Guid outboxEventId,
        string errorDetails,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get outbox event by ID
    /// </summary>
    Task<OutboxEvent?> GetByIdAsync(
        Guid outboxEventId,
        CancellationToken cancellationToken = default);
}
