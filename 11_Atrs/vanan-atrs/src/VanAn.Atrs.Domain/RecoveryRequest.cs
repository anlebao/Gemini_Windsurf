namespace VanAn.Atrs.Domain;

/// <summary>Loại yêu cầu recovery (FR-011).</summary>
public enum RecoveryRequestType
{
    LOST_DEVICE = 0,
    NO_CREDENTIAL = 1,
    COMPROMISED = 2,
    OTHER = 3
}

/// <summary>Trạng thái recovery request (FR-011).</summary>
public enum RecoveryRequestStatus
{
    SUBMITTED = 0,
    AUTOMATED_CHECK = 1,
    PENDING_USER_ACTION = 2,
    PENDING_REVIEW = 3,
    APPROVED = 4,
    REJECTED = 5,
    CANCELLED = 6,
    EXPIRED = 7,
    COMPLETED = 8
}

/// <summary>
/// RecoveryRequest — state machine đầy đủ (FR-011).
/// Transition hợp lệ:
///   SUBMITTED → AUTOMATED_CHECK → (APPROVED | PENDING_REVIEW | PENDING_USER_ACTION | REJECTED)
///   PENDING_REVIEW → (APPROVED | REJECTED | PENDING_USER_ACTION)
///   APPROVED → COMPLETED
///   SUBMITTED/AUTOMATED_CHECK/PENDING_USER_ACTION → CANCELLED
///   (mọi pending) → EXPIRED
/// COMPLETED = sau khi user re-enroll credential mới (tách khỏi APPROVED).
/// </summary>
public sealed class RecoveryRequest
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public RecoveryRequestType RequestType { get; private set; }
    public RecoveryRequestStatus Status { get; private set; }
    public Guid? NewDeviceId { get; private set; }
    public string? EvidenceSummaryJson { get; private set; }
    public string? RiskDecisionJson { get; private set; }
    public Guid? ReviewerId { get; private set; }
    public string? DecisionReasonCode { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private RecoveryRequest() { }

    public RecoveryRequest(Guid id, Guid trustSubjectId, RecoveryRequestType requestType, string idempotencyKey, Guid? newDeviceId = null, string? evidenceSummaryJson = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty) throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("IdempotencyKey cannot be empty.", nameof(idempotencyKey));
        Id = id;
        TrustSubjectId = trustSubjectId;
        RequestType = requestType;
        IdempotencyKey = idempotencyKey;
        NewDeviceId = newDeviceId;
        EvidenceSummaryJson = evidenceSummaryJson;
        Status = RecoveryRequestStatus.SUBMITTED;
        CreatedAt = DateTime.UtcNow;
    }

    public void StartAutomatedCheck()
    {
        EnsureState(RecoveryRequestStatus.SUBMITTED);
        Status = RecoveryRequestStatus.AUTOMATED_CHECK;
    }

    /// <summary>Ghi kết quả risk engine (FR-010): JSON decision + chuyển trạng thái theo quyết định.</summary>
    public void ApplyAutomatedDecision(string riskDecisionJson, RecoveryRequestStatus nextStatus, string? reasonCode)
    {
        EnsureState(RecoveryRequestStatus.AUTOMATED_CHECK);
        if (nextStatus is not (RecoveryRequestStatus.APPROVED or RecoveryRequestStatus.PENDING_REVIEW
            or RecoveryRequestStatus.PENDING_USER_ACTION or RecoveryRequestStatus.REJECTED))
            throw new InvalidOperationException($"Invalid automated next status {nextStatus}.");
        RiskDecisionJson = riskDecisionJson;
        DecisionReasonCode = reasonCode;
        Status = nextStatus;
        if (nextStatus is RecoveryRequestStatus.APPROVED or RecoveryRequestStatus.REJECTED)
            CompletedAt = DateTime.UtcNow;
    }

    public void Approve(Guid reviewerId)
    {
        EnsureState(RecoveryRequestStatus.PENDING_REVIEW, RecoveryRequestStatus.AUTOMATED_CHECK);
        ReviewerId = reviewerId;
        Status = RecoveryRequestStatus.APPROVED;
        CompletedAt = DateTime.UtcNow;
    }

    public void Reject(Guid reviewerId, string reasonCode)
    {
        EnsureState(RecoveryRequestStatus.PENDING_REVIEW, RecoveryRequestStatus.AUTOMATED_CHECK, RecoveryRequestStatus.PENDING_USER_ACTION);
        ReviewerId = reviewerId;
        DecisionReasonCode = reasonCode;
        Status = RecoveryRequestStatus.REJECTED;
        CompletedAt = DateTime.UtcNow;
    }

    public void RequestMoreInfo()
    {
        EnsureState(RecoveryRequestStatus.AUTOMATED_CHECK, RecoveryRequestStatus.PENDING_REVIEW);
        Status = RecoveryRequestStatus.PENDING_USER_ACTION;
    }

    public void Cancel()
    {
        EnsureState(RecoveryRequestStatus.SUBMITTED, RecoveryRequestStatus.AUTOMATED_CHECK, RecoveryRequestStatus.PENDING_USER_ACTION);
        Status = RecoveryRequestStatus.CANCELLED;
        CompletedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        EnsureState(
            RecoveryRequestStatus.SUBMITTED, RecoveryRequestStatus.AUTOMATED_CHECK,
            RecoveryRequestStatus.PENDING_USER_ACTION, RecoveryRequestStatus.PENDING_REVIEW);
        Status = RecoveryRequestStatus.EXPIRED;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>COMPLETED — chỉ từ APPROVED, sau khi user re-enroll credential mới.</summary>
    public void Complete(Guid? newDeviceId = null)
    {
        EnsureState(RecoveryRequestStatus.APPROVED);
        if (newDeviceId.HasValue)
            NewDeviceId = newDeviceId;
        Status = RecoveryRequestStatus.COMPLETED;
        CompletedAt = DateTime.UtcNow;
    }

    private void EnsureState(params RecoveryRequestStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new InvalidOperationException($"Invalid transition from {Status} — allowed: {string.Join(", ", allowed)}.");
    }
}