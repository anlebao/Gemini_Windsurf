namespace VanAn.Atrs.Contracts;

// ═══ ATRS-9 (Wave 3) — Dispute DTOs (FR-012/013/014) ═══

/// <summary>Dispute case summary cho user list (U11) — KHÔNG lộ internal fields.</summary>
public sealed record DisputeCaseSummaryDto(
    Guid Id,
    string CaseType,
    string Status,
    Guid? TenantId,
    string ClaimedIssue,
    DateTime SubmittedAt,
    DateTime? ClosedAt,
    string? UserFacingStatusNote);

/// <summary>Chi tiết dispute phía user (U13) — chỉ user-visible data.</summary>
public sealed record DisputeCaseDetailDto(
    Guid Id,
    string CaseType,
    string Status,
    Guid? TenantId,
    string ClaimedIssue,
    IReadOnlyList<string> RelatedTransactionIds,
    IReadOnlyList<string> RelatedLedgerReferences,
    DateTime SubmittedAt,
    DateTime? ClosedAt,
    string? UserFacingStatusNote,
    IReadOnlyList<DisputeEvidenceItemDto> Evidence,
    IReadOnlyList<string> UserVisibleTimeline);

/// <summary>
/// Evidence item — user-facing view mặc định KHÔNG lộ AccessClassification;
/// staff detail fill 3 field cuối (FR-014).
/// </summary>
public sealed record DisputeEvidenceItemDto(
    Guid Id,
    string EvidenceType,
    string? SourceReference,
    string? Description,
    DateTime RecordedAt,
    string? AccessClassification = null,
    string? ReliabilityClass = null,
    string? SubmittedBy = null);

/// <summary>Staff queue row (A17) — masked, không PII.</summary>
public sealed record DisputeQueueItemDto(
    Guid Id,
    string CaseType,
    string Status,
    Guid? TenantId,
    Guid TrustSubjectId,
    Guid? AssignedReviewerId,
    DateTime SubmittedAt,
    int EvidenceCount,
    int PendingDecisionCount);

/// <summary>Staff detail (A18/A19) — đầy đủ evidence + decisions + ledger transactions.</summary>
public sealed record DisputeStaffDetailDto(
    Guid Id,
    string CaseType,
    string Status,
    Guid? TenantId,
    Guid TrustSubjectId,
    string ClaimedIssue,
    IReadOnlyList<string> RelatedTransactionIds,
    IReadOnlyList<string> RelatedLedgerReferences,
    Guid? AssignedReviewerId,
    DateTime SubmittedAt,
    DateTime? ClosedAt,
    string? UserFacingStatusNote,
    IReadOnlyList<DisputeEvidenceItemDto> Evidence,
    IReadOnlyList<DisputeDecisionDto> Decisions,
    IReadOnlyList<LoyaltyLedgerEntryDto> Transactions);

/// <summary>Decision row (append-only) cho staff UI.</summary>
public sealed record DisputeDecisionDto(
    Guid Id,
    string Decision,
    string Status,
    string DecisionReason,
    string? UserFacingExplanation,
    string? InternalRationale,
    int? ProposedAdjustmentPoints,
    Guid? ProposedAdjustmentTenantId,
    string? ProposedAdjustmentReason,
    Guid ProposedBy,
    Guid? ApprovedBy,
    DateTime ProposedAt,
    DateTime? ExecutedAt,
    string? ResolutionReference,
    string? RejectionReason);

/// <summary>
/// Ledger entry read model (FR-013 transaction history) — union của
/// LoyaltyIssuanceRecord (Silo earn) + AllianceTransaction + LoyaltyRewards.History.
/// </summary>
public sealed record LoyaltyLedgerEntryDto(
    string Id,
    string Type,               // EARN | SPEND | REDEEM | ADJUST | REFUND | REVERSAL
    int Points,                // signed: dương = cộng, âm = trừ
    int? BalanceAfter,
    string Reason,
    string? Reference,         // orderId / voucherCode / source ref
    DateTime OccurredAt,
    Guid TenantId,
    string Source,             // SILO_ISSUANCE | ALLIANCE | SILO_HISTORY
    bool IsReversed = false);

/// <summary>Submit dispute request (U12 backend).</summary>
public sealed record SubmitDisputeRequest(
    string CaseType,
    string ClaimedIssue,
    Guid? TenantId,
    IReadOnlyList<string>? RelatedTransactionIds,
    IReadOnlyList<string>? RelatedLedgerReferences,
    string? IdempotencyKey);

/// <summary>Add evidence item (U12/U13 + staff A18).</summary>
public sealed record AddDisputeEvidenceRequest(
    string EvidenceType,
    string SourceSystem,
    string? SourceReference,
    string? IntegrityHash,
    string? Description,
    DateTime? CapturedAt,
    string? AccessClassification);

/// <summary>Staff: tạo decision proposal (investigator — A19).</summary>
public sealed record ProposeDisputeDecisionRequest(
    string Decision,                       // DisputeDecisionKind
    string DecisionReason,
    string? UserFacingExplanation,
    string? InternalRationale,
    string PolicyVersion,
    int? ProposedAdjustmentPoints,
    Guid? ProposedAdjustmentTenantId,
    string? ProposedAdjustmentReason);

/// <summary>Staff: approve/reject decision proposal (approver — maker-checker).</summary>
public sealed record DecideDisputeRequest(string? RejectionReason = null);

/// <summary>Staff queue filter.</summary>
public sealed record DisputeQueueQuery(
    string? Status = null,
    Guid? AssignedTo = null,
    int Skip = 0,
    int Take = 50);

/// <summary>Recovery queue item (A15) — masked.</summary>
public sealed record RecoveryQueueItemDto(
    Guid Id,
    string RequestType,
    string Status,
    Guid TrustSubjectId,
    Guid? ReviewerId,
    string? DecisionReasonCode,
    DateTime CreatedAt,
    DateTime? CompletedAt);

/// <summary>Kết quả submit dispute — Existing=true khi anti-duplicate/idempotent hit.</summary>
public sealed record SubmitDisputeResultDto(DisputeCaseSummaryDto Case, bool Existing);
