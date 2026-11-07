namespace VanAn.UI.Platform.Components.Ai;

/// <summary>
/// Task AI-P4 / SRS v1.1 §6.1 — widget-facing contract for the tenant AI chat.
/// Implemented per host (KhachLink: TenantAiPublicHttpService → Gateway
/// /api/public/tenant-ai/*). Returns null on any transport error — the widget
/// treats null as "AI tạm nghỉ" (NFR-3: AI down ≠ storefront down).
/// </summary>
public interface IAiChatClient
{
    /// <summary>Widget config — aiEnabled=false hides the widget completely.</summary>
    Task<AiWidgetConfig?> GetWidgetConfigAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Send one message; messageId is the end-to-end idempotency key.</summary>
    Task<AiChatResult?> SendChatAsync(Guid tenantId, string sessionId, string messageId,
        string text, string? locale = null, CancellationToken ct = default);

    /// <summary>Session history for resume — newest last, may be empty.</summary>
    Task<IReadOnlyList<AiSessionMessage>?> GetSessionMessagesAsync(Guid tenantId,
        string sessionId, CancellationToken ct = default);
}

public sealed record AiWidgetConfig(
    bool AiEnabled,
    string? AiName,
    string? Greeting,
    IReadOnlyList<string>? Suggestions);

/// <summary>Product card rendered inside an assistant answer (FR-5: available + non-stale only).</summary>
public sealed record AiProductCardItem(Guid ProductId, string Name, decimal Price, string? ImageUrl);

public sealed record AiChatResult(
    string Answer,
    IReadOnlyList<AiProductCardItem> Products,
    IReadOnlyList<string> SourceIds,
    double Confidence,
    bool Refused,
    string? RefuseReason,
    bool StaleData,
    bool Degraded);

/// <summary>
/// Refused = assistant could not/would not answer (policy refuse, rate-limit, or local
/// transport fallback) — Task AI-P8 shows the "Hỏi chủ quán trực tiếp" handoff chip on it.
/// Server history payloads without `refused` deserialize to false (backward compatible).
/// </summary>
public sealed record AiSessionMessage(
    string Role,
    string Content,
    IReadOnlyList<AiProductCardItem> Products,
    DateTime CreatedAtUtc,
    bool Refused = false);
