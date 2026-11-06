namespace VanAn.TenantAi.Contracts;

/// <summary>SRS v1.1 §6.1 — public chat wire DTOs (caller `gateway`, scope public-chat).</summary>
public sealed record ChatRequest(
    string SessionId,
    string MessageId,
    string Text,
    string? Locale = null);

public sealed record ChatProductDto(
    Guid ProductId,
    string Name,
    decimal Price,
    string? ImageUrl);

public sealed record ChatResponse(
    string Answer,
    IReadOnlyList<ChatProductDto> Products,
    IReadOnlyList<string> SourceIds,
    double Confidence,
    bool Refused,
    string? RefuseReason,
    bool StaleData,
    bool Degraded);
