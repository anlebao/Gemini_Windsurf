namespace VanAn.Atrs.Contracts;

/// <summary>Query audit explorer (A20) — server-side filter + paging.</summary>
public sealed record AuditQuery(
    string? EventType = null,
    string? Outcome = null,
    string? ActorType = null,
    string? SourceApplication = null,
    Guid? SubjectId = null,
    DateTime? From = null,
    DateTime? To = null,
    int Skip = 0,
    int Take = 50);

/// <summary>
/// Item audit explorer (A20) — READ-ONLY, SANITIZED: KHÔNG expose
/// MinimalMetadataJson / EvidenceReferencesJson (payload có thể chứa chi tiết nhạy cảm).
/// </summary>
public sealed record SecurityEventItemDto(
    Guid Id,
    string EventType,
    DateTime OccurredAt,
    string ActorType,               // SecurityEventActorType
    string? ActorId,
    string? SourceApplication,
    Guid? DeviceId,
    string? SessionCorrelationId,
    string Outcome,                 // SecurityEventOutcome
    string? ReasonCode,
    Guid TrustSubjectId);

/// <summary>Page audit explorer — total cho phân trang server-side.</summary>
public sealed record SecurityEventPageDto(
    IReadOnlyList<SecurityEventItemDto> Items,
    int Total,
    int Skip,
    int Take);