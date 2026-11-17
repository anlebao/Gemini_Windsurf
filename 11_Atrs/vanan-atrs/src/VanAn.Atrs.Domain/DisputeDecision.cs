namespace VanAn.Atrs.Domain;

/// <summary>
/// DisputeDecision — quyết định dispute, APPEND-ONLY (FR-012/FR-014).
/// Tách khỏi DisputeCase: một case có thể có nhiều decision rows (re-propose sau FAILED).
/// Maker-checker: ProposedBy (investigator) ≠ ApprovedBy (approver) — enforce ở service.
/// ResolutionReference = ledger transaction id sau khi adjust execute thành công.
/// </summary>
public sealed class DisputeDecision
{
    public Guid Id { get; private set; }
    public Guid DisputeCaseId { get; private set; }
    public DisputeDecisionKind Decision { get; private set; }
    public string DecisionReason { get; private set; } = string.Empty;
    public string? UserFacingExplanation { get; private set; }
    public string? InternalRationale { get; private set; }
    public string PolicyVersion { get; private set; } = "v1";
    public int? ProposedAdjustmentPoints { get; private set; }
    public Guid? ProposedAdjustmentTenantId { get; private set; }
    public string? ProposedAdjustmentReason { get; private set; }
    public DisputeDecisionStatus Status { get; private set; }
    public Guid ProposedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public Guid? RejectedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime ProposedAt { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public DateTime? ExecutedAt { get; private set; }
    public string? ResolutionReference { get; private set; }
    public string? IdempotencyKey { get; private set; }

    private DisputeDecision() { }

    public DisputeDecision(
        Guid id, Guid disputeCaseId, DisputeDecisionKind decision,
        string decisionReason, Guid proposedBy,
        string? userFacingExplanation = null, string? internalRationale = null,
        string policyVersion = "v1",
        int? proposedAdjustmentPoints = null, Guid? proposedAdjustmentTenantId = null,
        string? proposedAdjustmentReason = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (disputeCaseId == Guid.Empty) throw new ArgumentException("DisputeCaseId cannot be empty.", nameof(disputeCaseId));
        if (proposedBy == Guid.Empty) throw new ArgumentException("ProposedBy cannot be empty.", nameof(proposedBy));
        if (string.IsNullOrWhiteSpace(decisionReason))
            throw new ArgumentException("DecisionReason cannot be empty (audit).", nameof(decisionReason));
        if (proposedAdjustmentPoints == 0)
            throw new ArgumentException("Adjustment of 0 points is meaningless — use null.", nameof(proposedAdjustmentPoints));
        if (proposedAdjustmentPoints.HasValue && string.IsNullOrWhiteSpace(proposedAdjustmentReason))
            throw new ArgumentException("Adjustment requires ProposedAdjustmentReason (audit).", nameof(proposedAdjustmentReason));
        if (proposedAdjustmentPoints.HasValue && !proposedAdjustmentTenantId.HasValue)
            throw new ArgumentException("Adjustment requires ProposedAdjustmentTenantId.", nameof(proposedAdjustmentTenantId));

        Id = id;
        DisputeCaseId = disputeCaseId;
        Decision = decision;
        DecisionReason = decisionReason.Trim();
        UserFacingExplanation = userFacingExplanation;
        InternalRationale = internalRationale;
        PolicyVersion = policyVersion;
        ProposedAdjustmentPoints = proposedAdjustmentPoints;
        ProposedAdjustmentTenantId = proposedAdjustmentTenantId;
        ProposedAdjustmentReason = proposedAdjustmentReason;
        Status = DisputeDecisionStatus.PENDING_APPROVAL;
        ProposedBy = proposedBy;
        ProposedAt = DateTime.UtcNow;
        IdempotencyKey = $"dispute:{disputeCaseId:N}:adjust:{id:N}";
    }

    /// <summary>
    /// Approver duyệt proposal. Maker-checker: approver PHẢI khác proposer.
    /// Trả về true nếu decision cần ledger execution (có adjustment ≠ 0).
    /// </summary>
    public bool Approve(Guid approverId)
        => Approve(approverId, enforceMakerChecker: true);

    /// <summary>
    /// Approver duyệt proposal. Maker-checker mặc định enforce (approver ≠ proposer);
    /// có thể tắt qua policy `dispute.maker_checker_required` (A21 — security policy).
    /// Trả về true nếu decision cần ledger execution (có adjustment ≠ 0).
    /// </summary>
    public bool Approve(Guid approverId, bool enforceMakerChecker)
    {
        EnsurePending();
        if (approverId == Guid.Empty) throw new ArgumentException("approverId cannot be empty.", nameof(approverId));
        if (enforceMakerChecker && approverId == ProposedBy)
            throw new InvalidOperationException("Maker-checker: approver cannot be the proposer.");

        ApprovedBy = approverId;
        DecidedAt = DateTime.UtcNow;
        Status = DisputeDecisionStatus.APPROVED;
        return RequiresAdjustment;
    }

    /// <summary>Approver từ chối proposal — case quay lại UNDER_REVIEW.</summary>
    public void RejectByApprover(Guid approverId, string reason)
    {
        EnsurePending();
        if (approverId == Guid.Empty) throw new ArgumentException("approverId cannot be empty.", nameof(approverId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required (audit).", nameof(reason));
        RejectedBy = approverId;
        RejectionReason = reason.Trim();
        DecidedAt = DateTime.UtcNow;
        Status = DisputeDecisionStatus.REJECTED;
    }

    /// <summary>Ledger adjust thành công — decision EXECUTED + resolution reference.</summary>
    public void MarkExecuted(string resolutionReference)
    {
        if (Status != DisputeDecisionStatus.APPROVED)
            throw new InvalidOperationException($"Cannot execute decision in {Status}.");
        if (string.IsNullOrWhiteSpace(resolutionReference))
            throw new ArgumentException("resolutionReference cannot be empty.", nameof(resolutionReference));
        ResolutionReference = resolutionReference.Trim();
        ExecutedAt = DateTime.UtcNow;
        Status = DisputeDecisionStatus.EXECUTED;
    }

    /// <summary>Ledger adjust fail — decision FAILED, case quay lại review.</summary>
    public void MarkFailed()
    {
        if (Status != DisputeDecisionStatus.APPROVED)
            throw new InvalidOperationException($"Cannot fail decision in {Status}.");
        Status = DisputeDecisionStatus.FAILED;
    }

    public bool RequiresAdjustment => ProposedAdjustmentPoints.HasValue && ProposedAdjustmentPoints.Value != 0;

    private void EnsurePending()
    {
        if (Status != DisputeDecisionStatus.PENDING_APPROVAL)
            throw new InvalidOperationException($"Decision is not pending approval ({Status}).");
    }
}
