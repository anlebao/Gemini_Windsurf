namespace VanAn.TenantAi.Domain;

public enum MessageRole
{
    User,
    Assistant
}

/// <summary>
/// SRS v1.1 §7 + [G8][G10] — persisted chat turn. MessageId is the caller-supplied
/// idempotency key (unique per tenant+session — replay dedupe). Token counts feed
/// cost analysis [NFR-6]; ExpiresAtUtc enforces 90d retention 【PO G10】.
/// </summary>
public sealed class ConversationMessage
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string SessionId { get; private set; } = string.Empty;
    public string MessageId { get; private set; } = string.Empty;
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? Intent { get; private set; }
    public string? ProductRefsJson { get; private set; }
    public string? SourceIdsJson { get; private set; }
    public double? Confidence { get; private set; }
    public bool Refused { get; private set; }
    public string? RefuseReason { get; private set; }
    public bool Degraded { get; private set; }
    public int? PromptTokens { get; private set; }
    public int? CompletionTokens { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    private ConversationMessage() { } // EF Core

    public static ConversationMessage Create(
        Guid tenantId,
        string sessionId,
        string messageId,
        MessageRole role,
        string content,
        DateTime expiresAtUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId,
            MessageId = messageId,
            Role = role,
            Content = content,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc
        };

    /// <summary>Enrich an assistant message with orchestrator + validator output.</summary>
    public void SetAnswerMeta(
        string? intent,
        string? productRefsJson,
        string? sourceIdsJson,
        double? confidence,
        bool refused,
        string? refuseReason,
        bool degraded,
        int? promptTokens,
        int? completionTokens)
    {
        Intent = intent;
        ProductRefsJson = productRefsJson;
        SourceIdsJson = sourceIdsJson;
        Confidence = confidence;
        Refused = refused;
        RefuseReason = refuseReason;
        Degraded = degraded;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
    }
}
