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

/// <summary>SRS v1.1 §6.1 — session history (GET /v1/chat/sessions/{sid}/messages).</summary>
public sealed record SessionMessageDto(
    string Role,
    string Content,
    IReadOnlyList<ChatProductDto> Products,
    DateTime CreatedAtUtc);

public sealed record SessionMessagesResponse(
    string SessionId,
    IReadOnlyList<SessionMessageDto> Messages);

/// <summary>SRS v1.1 §6.1 — GET /v1/config/widget. aiEnabled=false → client hides the widget.</summary>
public sealed record WidgetConfigResponse(
    bool AiEnabled,
    string? AiName,
    string? Greeting,
    IReadOnlyList<string>? Suggestions);
