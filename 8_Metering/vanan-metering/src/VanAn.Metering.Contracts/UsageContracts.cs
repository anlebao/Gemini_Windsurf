namespace VanAn.Metering.Contracts;

/// <summary>SRS §6.1 — POST /v1/usage/events request body.</summary>
public sealed record UsageEventRequest(
    Guid TenantId,
    string ServiceCode,
    decimal Quantity,
    string Unit,
    DateTime OccurredAtUtc,
    Guid? ShopInstanceId = null,
    string? RequestedBy = null,
    string? TaskRef = null,
    string? MetadataJson = null);

public sealed record UsageEventResponse(Guid EventId, bool IdempotentReplay = false);

/// <summary>POST /v1/usage/events:batch item — per-event idempotency key (task_p3 D2/D9).</summary>
public sealed record UsageBatchItem(
    string IdempotencyKey,
    Guid TenantId,
    string ServiceCode,
    decimal Quantity,
    string Unit,
    DateTime OccurredAtUtc,
    Guid? ShopInstanceId = null,
    string? RequestedBy = null,
    string? TaskRef = null,
    string? MetadataJson = null)
{
    public UsageEventRequest ToRequest() => new(
        TenantId, ServiceCode, Quantity, Unit, OccurredAtUtc,
        ShopInstanceId, RequestedBy, TaskRef, MetadataJson);
}

/// <summary>Per-item status: "accepted" | "replayed" | "rejected" (task_p3 D2).</summary>
public sealed record UsageBatchItemResult(int Index, string Status, Guid? EventId, string? Error);

public sealed record UsageBatchResponse(
    int Accepted, int Replayed, int Rejected, IReadOnlyList<UsageBatchItemResult> Results);

/// <summary>
/// Shadow report row (task_p3 D6/D8). Credits = Σ(quantity × rate at each event's
/// occurredAtUtc); null when any event in the group is unpriced (Priced=false).
/// </summary>
public sealed record UsageSummaryItem(
    string ServiceCode, string Unit, decimal Quantity,
    decimal? Credits, bool Priced, int UnpricedEvents);

public sealed record UsageSummaryResponse(string Period, IReadOnlyList<UsageSummaryItem> Items);

public sealed record UsageEventDto(
    Guid Id, Guid TenantId, Guid? ShopInstanceId, string ServiceCode, decimal Quantity,
    string Unit, DateTime OccurredAtUtc, DateTime ReceivedAtUtc, bool LateEvent,
    string? RequestedBy, string? TaskRef, string CallerId);

public sealed record UsageEventListResponse(IReadOnlyList<UsageEventDto> Items, int Page, int PageSize);

/// <summary>Standard error shape — caller uses Retryable to decide retry (SRS §6.1).</summary>
public sealed record ApiError(string Code, string Message, bool Retryable);
