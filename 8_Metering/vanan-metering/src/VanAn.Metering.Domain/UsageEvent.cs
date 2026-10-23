namespace VanAn.Metering.Domain;

/// <summary>
/// SRS §7 — usage fact recorded by a caller system. Append-only; deduplicated by
/// (CallerId, IdempotencyKey) unique index. Single identity per [G13].
/// </summary>
public sealed class UsageEvent
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? ShopInstanceId { get; private set; }
    public string ServiceCode { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public string? RequestedBy { get; private set; }
    public string? TaskRef { get; private set; }
    public string? MetadataJson { get; private set; }
    public bool LateEvent { get; private set; }
    public string CallerId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;

    private UsageEvent() { } // EF Core

    public static UsageEvent Create(
        string callerId,
        string idempotencyKey,
        Guid tenantId,
        string serviceCode,
        decimal quantity,
        string unit,
        DateTime occurredAtUtc,
        DateTime receivedAtUtc,
        Guid? shopInstanceId = null,
        string? requestedBy = null,
        string? taskRef = null,
        string? metadataJson = null,
        bool lateEvent = false)
        => new()
        {
            Id = Guid.NewGuid(),
            CallerId = callerId,
            IdempotencyKey = idempotencyKey,
            TenantId = tenantId,
            ShopInstanceId = shopInstanceId,
            ServiceCode = serviceCode,
            Quantity = quantity,
            Unit = unit,
            OccurredAtUtc = occurredAtUtc,
            ReceivedAtUtc = receivedAtUtc,
            RequestedBy = requestedBy,
            TaskRef = taskRef,
            MetadataJson = metadataJson,
            LateEvent = lateEvent
        };
}
