namespace VanAn.Atrs.Contracts;

/// <summary>Query staff recovery queue (A15) — server-side filter + paging.</summary>
public sealed record RecoveryQueueQuery(
    string? Status = null,
    string? RequestType = null,
    Guid? AssignedTo = null,
    bool Unassigned = false,
    Guid? SubjectId = null,
    int Skip = 0,
    int Take = 50);

/// <summary>Item trong recovery queue (A15) — masked subject, SLA indicator.</summary>
public sealed record RecoveryQueueItemDto(
    Guid Id,
    string RequestType,               // RecoveryRequestType
    string Status,                    // RecoveryRequestStatus
    Guid TrustSubjectId,              // UI MASK — chỉ hiển thị prefix
    Guid? NewDeviceId,
    Guid? ReviewerId,
    Guid? AssignedReviewerId,
    DateTime? AssignedAt,
    string? DecisionReasonCode,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    double AgeHours,
    bool SlaBreached);

/// <summary>Page recovery queue — total cho phân trang server-side.</summary>
public sealed record RecoveryQueuePageDto(
    IReadOnlyList<RecoveryQueueItemDto> Items,
    int Total,
    int Skip,
    int Take);

/// <summary>Internal note staff (A16) — KHÔNG lộ cho user.</summary>
public sealed record RecoveryCaseNoteDto(
    Guid Id,
    Guid AuthorId,
    string NoteType,                  // RecoveryNoteType
    string Body,
    DateTime CreatedAt);

/// <summary>Device của subject (A16 section account context) — metadata an toàn.</summary>
public sealed record RecoveryStaffDeviceDto(
    Guid Id,
    string DeviceId,
    string? FriendlyName,
    string? Platform,
    string DeviceType,                // DeviceType
    string Status,                    // DeviceStatus
    DateTime FirstSeenAt,
    DateTime LastSeenAt);

/// <summary>Identity link của subject (A16 account context) — metadata an toàn.</summary>
public sealed record RecoverySubjectLinkDto(
    string IdentityType,              // TrustIdentityType
    Guid IdentityId,
    Guid? TenantId);

/// <summary>Sự kiện bảo mật gần đây của subject (A16 timeline) — SANITIZED, KHÔNG payload.</summary>
public sealed record RecoverySecurityEventDto(
    string EventType,
    DateTime OccurredAt,
    string Outcome,                   // SecurityEventOutcome
    string ActorType);                // SecurityEventActorType

/// <summary>Kết quả risk engine parse từ RiskDecisionJson (A16 hiển thị policy version).</summary>
public sealed record RecoveryRiskSummaryDto(
    string? Kind,                     // RiskDecisionKind
    string? PolicyVersion,
    IReadOnlyList<string> ReasonCodes);

/// <summary>Staff detail recovery request (A16) — 9 sections UI spec.
/// KHÔNG expose EvidenceSummaryJson payload (user evidence có thể chứa nội dung nhạy cảm).</summary>
public sealed record RecoveryStaffDetailDto(
    Guid Id,
    string RequestType,
    string Status,
    Guid TrustSubjectId,
    Guid? NewDeviceId,
    string? ReviewerId,
    Guid? AssignedReviewerId,
    DateTime? AssignedAt,
    string? DecisionReasonCode,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    RecoveryRiskSummaryDto? Risk,
    IReadOnlyList<RecoveryCaseNoteDto> Notes,
    IReadOnlyList<RecoveryStaffDeviceDto> Devices,
    IReadOnlyList<RecoverySubjectLinkDto> SubjectLinks,
    IReadOnlyList<RecoverySecurityEventDto> RecentEvents);

/// <summary>Widgets A14 operations dashboard — data thật từ DB, không hardcode.</summary>
public sealed record AtrsDashboardDto(
    int NewRecovery24h,
    int AwaitingReview,               // PENDING_REVIEW
    int Escalated,                    // ESCALATED
    int NeedsInfo,                    // PENDING_USER_ACTION
    int OpenDisputes,                 // dispute chưa terminal
    int SlaOverdue,                   // pending > SLA threshold
    int RecentSecurityIncidents24h,   // AccountSecurityEvents Failure/Blocked 24h
    DateTime GeneratedAt);