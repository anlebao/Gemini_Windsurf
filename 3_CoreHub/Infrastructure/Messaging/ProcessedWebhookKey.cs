namespace VanAn.CoreHub.Infrastructure.Messaging;

/// <summary>
/// Durable idempotency record for webhook deduplication.
/// DB-backed — survives process restart. Infrastructure concern only.
/// </summary>
public class ProcessedWebhookKey
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ProcessedWebhookKey() { }

    public ProcessedWebhookKey(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        IdempotencyKey = idempotencyKey;
        ProcessedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }
}
