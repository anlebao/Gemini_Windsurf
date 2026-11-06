namespace VanAn.TenantAi.Domain;

/// <summary>
/// SRS v1.1 §7 — chat session keyed by client-supplied SessionId, unique per tenant.
/// Channel: public storefront | admin preview. MetaJson carries client hints (locale, ua).
/// </summary>
public sealed class ConversationSession
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string SessionId { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string MetaJson { get; private set; } = "{}";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastActiveAtUtc { get; private set; }

    private ConversationSession() { } // EF Core

    public static ConversationSession Create(
        Guid tenantId,
        string sessionId,
        string channel,
        string metaJson = "{}")
    {
        var now = DateTime.UtcNow;
        return new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId,
            Channel = channel,
            MetaJson = metaJson,
            CreatedAtUtc = now,
            LastActiveAtUtc = now
        };
    }

    public void Touch() => LastActiveAtUtc = DateTime.UtcNow;
}
