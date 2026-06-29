namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// Contract for publishing events to NATS message broker.
/// Used by NatsSyncWorker to flush Outbox → NATS.
/// ADR-001 v2 Edge: enables async event-driven sync between SQLite station and PostgreSQL cloud.
/// </summary>
public interface INatsEventPublisher : IDisposable
{
    /// <summary>
    /// Publish a raw byte payload to a NATS subject.
    /// Fire-and-forget at the transport level; caller handles retry via Outbox pattern.
    /// </summary>
    Task PublishAsync(string subject, byte[] payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the underlying NATS connection is currently active.
    /// NatsSyncWorker checks this before each batch to avoid unnecessary Outbox queries.
    /// </summary>
    bool IsConnected { get; }
}
