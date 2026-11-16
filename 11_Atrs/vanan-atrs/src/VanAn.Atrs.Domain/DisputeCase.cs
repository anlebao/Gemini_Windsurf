namespace VanAn.Atrs.Domain;

/// <summary>
/// DisputeCase — hồ sơ khiếu nại của TrustSubject (FR-012, §7.7, D4).
/// Case-level state machine; decisions là entity RIÊNG append-only (DisputeDecision).
/// TenantId = tenant mà user khiếu nại (điểm/ví tại tenant đó); null khi case
/// account-level (vd account-takeover không gắn tenant cụ thể).
/// KHÔNG lưu PII thô — chỉ TrustSubjectId + claimed issue text + refs.
/// </summary>
public sealed class DisputeCase
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public Guid? TenantId { get; private set; }
    public DisputeCaseType CaseType { get; private set; }
    public string RelatedTransactionIdsJson { get; private set; } = "[]";
    public string RelatedLedgerReferencesJson { get; private set; } = "[]";
    public string ClaimedIssue { get; private set; } = string.Empty;
    public DisputeCaseStatus Status { get; private set; }
    public Guid? AssignedReviewerId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public DateTime? WithdrawnAt { get; private set; }
    public string? UserFacingStatusNote { get; private set; }

    private DisputeCase() { }

    public DisputeCase(
        Guid id, Guid trustSubjectId, Guid? tenantId, DisputeCaseType caseType,
        string claimedIssue, string relatedTransactionIdsJson, string relatedLedgerReferencesJson,
        string? idempotencyKey = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty) throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (string.IsNullOrWhiteSpace(claimedIssue))
            throw new ArgumentException("ClaimedIssue cannot be empty.", nameof(claimedIssue));

        Id = id;
        TrustSubjectId = trustSubjectId;
        TenantId = tenantId;
        CaseType = caseType;
        ClaimedIssue = claimedIssue.Trim();
        RelatedTransactionIdsJson = string.IsNullOrWhiteSpace(relatedTransactionIdsJson) ? "[]" : relatedTransactionIdsJson;
        RelatedLedgerReferencesJson = string.IsNullOrWhiteSpace(relatedLedgerReferencesJson) ? "[]" : relatedLedgerReferencesJson;
        IdempotencyKey = idempotencyKey;
        Status = DisputeCaseStatus.OPEN;
        SubmittedAt = DateTime.UtcNow;
    }

    /// <summary>Staff nhận case (A17 assign). Chỉ khi chưa terminal.</summary>
    public void Assign(Guid staffUserId)
    {
        EnsureNotTerminal();
        if (staffUserId == Guid.Empty) throw new ArgumentException("staffUserId cannot be empty.", nameof(staffUserId));
        AssignedReviewerId = staffUserId;
        if (Status == DisputeCaseStatus.OPEN)
            Status = DisputeCaseStatus.TRIAGE;
    }

    /// <summary>Chuyển trạng thái theo state machine FR-012.</summary>
    public void TransitionTo(DisputeCaseStatus target, string? userFacingNote = null)
    {
        EnsureNotTerminal();
        bool allowed = (Status, target) switch
        {
            (DisputeCaseStatus.OPEN, DisputeCaseStatus.TRIAGE) => true,
            (DisputeCaseStatus.OPEN, DisputeCaseStatus.WAITING_FOR_USER) => true,
            (DisputeCaseStatus.TRIAGE, DisputeCaseStatus.UNDER_REVIEW) => true,
            (DisputeCaseStatus.TRIAGE, DisputeCaseStatus.WAITING_FOR_USER) => true,
            (DisputeCaseStatus.TRIAGE, DisputeCaseStatus.WAITING_FOR_EXTERNAL_EVIDENCE) => true,
            (DisputeCaseStatus.WAITING_FOR_USER, DisputeCaseStatus.UNDER_REVIEW) => true,
            (DisputeCaseStatus.WAITING_FOR_USER, DisputeCaseStatus.WAITING_FOR_EXTERNAL_EVIDENCE) => true,
            (DisputeCaseStatus.UNDER_REVIEW, DisputeCaseStatus.WAITING_FOR_USER) => true,
            (DisputeCaseStatus.UNDER_REVIEW, DisputeCaseStatus.WAITING_FOR_EXTERNAL_EVIDENCE) => true,
            (DisputeCaseStatus.UNDER_REVIEW, DisputeCaseStatus.DECIDED) => true,
            (DisputeCaseStatus.WAITING_FOR_EXTERNAL_EVIDENCE, DisputeCaseStatus.UNDER_REVIEW) => true,
            // Decision FAILED → quay lại review để investigator sửa proposal.
            (DisputeCaseStatus.DECIDED, DisputeCaseStatus.UNDER_REVIEW) => true,
            // Mọi trạng thái non-terminal → CLOSED (manual close).
            (_, DisputeCaseStatus.CLOSED) => true,
            _ => false
        };
        if (!allowed)
            throw new InvalidOperationException($"Invalid dispute transition {Status} → {target}.");

        Status = target;
        if (!string.IsNullOrWhiteSpace(userFacingNote))
            UserFacingStatusNote = userFacingNote.Trim();
        if (target == DisputeCaseStatus.CLOSED)
            ClosedAt ??= DateTime.UtcNow;
    }

    /// <summary>DECIDED — decision proposal đã tạo (chờ approver). Gọi khi DisputeDecision pending.</summary>
    public void MarkDecidedPending() => TransitionTo(DisputeCaseStatus.DECIDED);

    /// <summary>RESOLVED — CHỈ sau khi ledger adjustment confirm (FR-013).</summary>
    public void MarkResolved()
    {
        if (Status is not (DisputeCaseStatus.DECIDED or DisputeCaseStatus.UNDER_REVIEW))
            throw new InvalidOperationException($"Cannot resolve dispute from {Status}.");
        Status = DisputeCaseStatus.RESOLVED;
        ClosedAt = DateTime.UtcNow;
    }

    /// <summary>REJECTED — decision cuối = rejected/no-action đã được approve.</summary>
    public void MarkRejected()
    {
        if (Status is not (DisputeCaseStatus.DECIDED or DisputeCaseStatus.UNDER_REVIEW))
            throw new InvalidOperationException($"Cannot reject dispute from {Status}.");
        Status = DisputeCaseStatus.REJECTED;
        ClosedAt = DateTime.UtcNow;
    }

    /// <summary>User rút đơn — chỉ khi chưa có decision (OPEN/TRIAGE/WAITING_*).</summary>
    public void Withdraw()
    {
        if (Status is not (DisputeCaseStatus.OPEN or DisputeCaseStatus.TRIAGE
            or DisputeCaseStatus.WAITING_FOR_USER or DisputeCaseStatus.WAITING_FOR_EXTERNAL_EVIDENCE))
            throw new InvalidOperationException($"Cannot withdraw dispute from {Status}.");
        Status = DisputeCaseStatus.CLOSED;
        WithdrawnAt = DateTime.UtcNow;
        ClosedAt = DateTime.UtcNow;
    }

    /// <summary>Ledger reversal ngoài hệ thống trong lúc dispute mở → chờ evidence thêm (FR-013 note).</summary>
    public void FlagExternalEvidencePending(string? note = null)
    {
        EnsureNotTerminal();
        if (Status is DisputeCaseStatus.OPEN or DisputeCaseStatus.TRIAGE or DisputeCaseStatus.UNDER_REVIEW)
        {
            Status = DisputeCaseStatus.WAITING_FOR_EXTERNAL_EVIDENCE;
            if (!string.IsNullOrWhiteSpace(note))
                UserFacingStatusNote = note.Trim();
        }
    }

    public bool IsTerminal => Status is DisputeCaseStatus.RESOLVED or DisputeCaseStatus.REJECTED or DisputeCaseStatus.CLOSED;

    private void EnsureNotTerminal()
    {
        if (IsTerminal)
            throw new InvalidOperationException($"Dispute case is terminal ({Status}).");
    }
}
