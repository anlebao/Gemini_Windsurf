namespace VanAn.Atrs.Contracts;

/// <summary>Summary recovery request cho UI/queue (U10/A15).</summary>
public sealed record RecoveryRequestSummaryDto(
    Guid Id,
    string RequestType,          // RecoveryRequestType
    string Status,               // RecoveryRequestStatus
    Guid TrustSubjectId,
    Guid? NewDeviceId,
    string? RiskDecisionJson,
    string? ReviewerId,
    string? DecisionReasonCode,
    DateTime CreatedAt,
    DateTime? CompletedAt);

/// <summary>Kết quả submit — Existing=true khi trả về request đang active (FR-011 anti-duplicate).</summary>
public sealed record SubmitRecoveryResultDto(RecoveryRequestSummaryDto Request, bool Existing);

/// <summary>Kết quả verify code path trong recovery (tiered proof ①).</summary>
public sealed record VerifyRecoveryCodePathResult(
    bool Success,
    string Reason,               // Success | InvalidCode | Locked | WrongState
    RecoveryRequestSummaryDto? Request,
    string? RiskDecisionKind);