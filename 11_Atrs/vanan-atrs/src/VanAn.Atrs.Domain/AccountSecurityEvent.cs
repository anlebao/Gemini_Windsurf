namespace VanAn.Atrs.Domain;

/// <summary>
/// AccountSecurityEvent — lịch sử sự kiện bảo mật, APPEND-ONLY (FR-009, §7.8, D8).
/// KHÔNG có method update/delete — mọi thay đổi là bản ghi mới.
/// KHÔNG lưu secret/PII trong payload (MinimalMetadataJson/EvidenceReferencesJson chỉ metadata an toàn).
/// </summary>
public sealed class AccountSecurityEvent
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public SecurityEventActorType ActorType { get; private set; }
    public string? ActorId { get; private set; }
    public string? SourceApplication { get; private set; }
    public Guid? DeviceId { get; private set; }
    public string? SessionCorrelationId { get; private set; }
    public SecurityEventOutcome Outcome { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? EvidenceReferencesJson { get; private set; }
    public int SchemaVersion { get; private set; }
    public string? MinimalMetadataJson { get; private set; }

    private AccountSecurityEvent() { }

    public static AccountSecurityEvent Create(
        Guid id,
        Guid trustSubjectId,
        string eventType,
        SecurityEventActorType actorType,
        string? actorId,
        string? sourceApplication,
        Guid? deviceId,
        string? sessionCorrelationId,
        SecurityEventOutcome outcome,
        string? reasonCode,
        string? evidenceReferencesJson = null,
        string? minimalMetadataJson = null,
        int schemaVersion = 1,
        DateTime? occurredAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty)
            throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType cannot be empty.", nameof(eventType));
        if (schemaVersion < 1)
            throw new ArgumentException("SchemaVersion must be >= 1.", nameof(schemaVersion));

        return new AccountSecurityEvent
        {
            Id = id,
            TrustSubjectId = trustSubjectId,
            EventType = eventType,
            OccurredAt = occurredAt ?? DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow,
            ActorType = actorType,
            ActorId = actorId,
            SourceApplication = sourceApplication,
            DeviceId = deviceId,
            SessionCorrelationId = sessionCorrelationId,
            Outcome = outcome,
            ReasonCode = reasonCode,
            EvidenceReferencesJson = evidenceReferencesJson,
            SchemaVersion = schemaVersion,
            MinimalMetadataJson = minimalMetadataJson
        };
    }
}