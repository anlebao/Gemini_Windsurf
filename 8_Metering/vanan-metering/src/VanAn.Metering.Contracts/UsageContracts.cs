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

/// <summary>Standard error shape — caller uses Retryable to decide retry (SRS §6.1).</summary>
public sealed record ApiError(string Code, string Message, bool Retryable);
