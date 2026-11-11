namespace VanAn.Voice.Domain;

/// <summary>
/// Append-only lifecycle event (SRS v1.1 §7 <c>voice_call_events</c>): who, when, from which
/// connection — without conversation content. Metadata is a restricted JSON key whitelist.
/// </summary>
public sealed class VoiceCallEvent
{
    public Guid Id { get; private set; }
    public Guid CallId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public Guid? ActorUserId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string? MetadataJson { get; private set; }

    protected VoiceCallEvent() { } // EF

    public VoiceCallEvent(Guid callId, string eventType, Guid? actorUserId, DateTime occurredAt, string? metadataJson = null)
    {
        Id = Guid.NewGuid();
        CallId = callId;
        EventType = eventType;
        ActorUserId = actorUserId;
        OccurredAt = occurredAt;
        MetadataJson = metadataJson;
    }
}