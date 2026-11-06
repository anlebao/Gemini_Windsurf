namespace VanAn.TenantAi.Domain;

public enum IngestStatus
{
    Succeeded,
    Failed
}

/// <summary>
/// SRS v1.1 §7 + [G8] — ingest dedupe + audit. Unique (TenantId, Kind, IdempotencyKey):
/// a replayed batch returns the first result instead of double-applying.
/// </summary>
public sealed class IngestLog
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Batch kind: products | documents.</summary>
    public string Kind { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;
    public int ItemCount { get; private set; }
    public IngestStatus Status { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private IngestLog() { } // EF Core

    public static IngestLog Create(
        Guid tenantId,
        string kind,
        string idempotencyKey,
        int itemCount,
        IngestStatus status,
        string? error = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kind = kind,
            IdempotencyKey = idempotencyKey,
            ItemCount = itemCount,
            Status = status,
            Error = error,
            CreatedAtUtc = DateTime.UtcNow
        };
}
