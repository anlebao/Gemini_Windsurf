using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// ATRS-9 (Wave 3) — DisputeCaseManager: user dispute flow + staff investigation
/// + maker-checker decision → ledger adjustment (FR-012/013/014).
///
/// Nguyên tắc:
/// - Ledger (PG qua Gateway internal API) là source of truth — KHÔNG sửa số dư trực tiếp.
/// - Maker-checker enforce ở SERVICE layer (không chỉ UI): approver ≠ proposer.
/// - Case chỉ RESOLVED sau khi ledger confirm; adjust idempotent
///   ("dispute:{caseId}:adjust:{decisionId}") — retry không double-adjust.
/// - Mọi staff access/action → AccountSecurityEvent (ActorType=Staff).
/// - User-facing DTO KHÔNG lộ internal notes/fraud signals/AccessClassification.
/// </summary>
public interface IDisputeCaseManager
{
    // ── User (U11-U13 backend) ──
    Task<SubmitDisputeResultDto> SubmitAsync(Guid trustSubjectId, SubmitDisputeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DisputeCaseSummaryDto>> ListMineAsync(Guid trustSubjectId, CancellationToken ct = default);
    Task<DisputeCaseDetailDto?> GetMineAsync(Guid caseId, Guid trustSubjectId, CancellationToken ct = default);
    Task<DisputeCaseDetailDto?> AddEvidenceAsync(Guid caseId, Guid trustSubjectId, AddDisputeEvidenceRequest request, CancellationToken ct = default);
    Task<bool> WithdrawAsync(Guid caseId, Guid trustSubjectId, CancellationToken ct = default);
    Task<IReadOnlyList<LoyaltyLedgerEntryDto>> GetTransactionsAsync(Guid trustSubjectId, Guid tenantId, CancellationToken ct = default);

    // ── Staff (A17/A18/A19 backend) ──
    Task<IReadOnlyList<DisputeQueueItemDto>> ListQueueAsync(DisputeQueueQuery query, CancellationToken ct = default);
    Task<DisputeStaffDetailDto?> GetStaffDetailAsync(Guid caseId, Guid? staffUserId = null, CancellationToken ct = default);
    Task<DisputeQueueItemDto?> AssignAsync(Guid caseId, Guid staffUserId, CancellationToken ct = default);
    Task<DisputeCaseSummaryDto?> TransitionAsync(Guid caseId, Guid staffUserId, DisputeCaseStatus target, string? note, CancellationToken ct = default);
    Task<DisputeDecisionDto> ProposeDecisionAsync(Guid caseId, Guid staffUserId, ProposeDisputeDecisionRequest request, CancellationToken ct = default);
    Task<DisputeDecisionDto> ApproveDecisionAsync(Guid decisionId, Guid approverId, CancellationToken ct = default);
    Task<DisputeDecisionDto> RejectDecisionAsync(Guid decisionId, Guid approverId, string reason, CancellationToken ct = default);
}

public sealed class DisputeCaseManager(
    AtrsDbContext db,
    ILoyaltyGatewayClient loyaltyGateway,
    ISecurityPolicyService policyService,
    ILogger<DisputeCaseManager> logger) : IDisputeCaseManager
{
    private const string PolicyVersion = "atrs-dispute-v1";

    // ══════════════════════════════════════════════════════════
    // USER SIDE
    // ══════════════════════════════════════════════════════════

    public async Task<SubmitDisputeResultDto> SubmitAsync(Guid trustSubjectId, SubmitDisputeRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<DisputeCaseType>(request.CaseType, ignoreCase: true, out var caseType))
            throw new ArgumentException($"Invalid CaseType: {request.CaseType}");

        // Idempotency: cùng key → trả case cũ.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var byKey = await db.DisputeCases
                .FirstOrDefaultAsync(c => c.IdempotencyKey == request.IdempotencyKey, ct);
            if (byKey is not null)
                return new SubmitDisputeResultDto(ToUserSummary(byKey), Existing: true);
        }

        var txIdsJson = JsonSerializer.Serialize(request.RelatedTransactionIds ?? []);
        var ledgerRefsJson = JsonSerializer.Serialize(request.RelatedLedgerReferences ?? []);

        // Anti-duplicate (FR-012 note): active case cùng subject + cùng transaction ref chính → cảnh báo/trả existing.
        var active = await db.DisputeCases
            .Where(c => c.TrustSubjectId == trustSubjectId && !TerminalStatuses.Contains(c.Status))
            .OrderByDescending(c => c.SubmittedAt)
            .ToListAsync(ct);
        var primaryRef = (request.RelatedTransactionIds ?? []).FirstOrDefault() ?? (request.RelatedLedgerReferences ?? []).FirstOrDefault();
        if (primaryRef is not null)
        {
            var dup = active.FirstOrDefault(c =>
                c.RelatedTransactionIdsJson.Contains(primaryRef, StringComparison.Ordinal)
                || c.RelatedLedgerReferencesJson.Contains(primaryRef, StringComparison.Ordinal));
            if (dup is not null)
                return new SubmitDisputeResultDto(ToUserSummary(dup), Existing: true);
        }

        var entity = new DisputeCase(
            Guid.NewGuid(), trustSubjectId, request.TenantId, caseType,
            request.ClaimedIssue, txIdsJson, ledgerRefsJson,
            string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim());

        db.DisputeCases.Add(entity);
        await RecordEventAsync(trustSubjectId, "dispute.submitted", SecurityEventActorType.User, null,
            SecurityEventOutcome.Success, null, minimalMetadataJson: $"{{\"caseType\":\"{caseType}\"}}", ct: ct);
        await db.SaveChangesAsync(ct);
        return new SubmitDisputeResultDto(ToUserSummary(entity), Existing: false);
    }

    public async Task<IReadOnlyList<DisputeCaseSummaryDto>> ListMineAsync(Guid trustSubjectId, CancellationToken ct = default)
    {
        var cases = await db.DisputeCases
            .AsNoTracking()
            .Where(c => c.TrustSubjectId == trustSubjectId)
            .OrderByDescending(c => c.SubmittedAt)
            .Take(100)
            .ToListAsync(ct);
        return cases.Select(ToUserSummary).ToList();
    }

    public async Task<DisputeCaseDetailDto?> GetMineAsync(Guid caseId, Guid trustSubjectId, CancellationToken ct = default)
    {
        var entity = await db.DisputeCases.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == caseId && c.TrustSubjectId == trustSubjectId, ct);
        if (entity is null)
            return null;

        var evidence = await db.DisputeEvidenceItems.AsNoTracking()
            .Where(e => e.DisputeCaseId == caseId && e.AccessClassification != EvidenceAccessClass.RESTRICTED)
            .OrderBy(e => e.RecordedAt)
            .ToListAsync(ct);

        return ToUserDetail(entity, evidence);
    }

    public async Task<DisputeCaseDetailDto?> AddEvidenceAsync(Guid caseId, Guid trustSubjectId, AddDisputeEvidenceRequest request, CancellationToken ct = default)
    {
        var entity = await db.DisputeCases
            .FirstOrDefaultAsync(c => c.Id == caseId && c.TrustSubjectId == trustSubjectId, ct);
        if (entity is null || entity.IsTerminal)
            return null;

        if (!Enum.TryParse<DisputeEvidenceType>(request.EvidenceType, ignoreCase: true, out var evType))
            throw new ArgumentException($"Invalid EvidenceType: {request.EvidenceType}");
        // User submit luôn USER_VISIBLE — không cho user tự gắn STAFF_ONLY/RESTRICTED.
        var item = new DisputeEvidenceItem(
            Guid.NewGuid(), caseId, evType,
            string.IsNullOrWhiteSpace(request.SourceSystem) ? "khachlink" : request.SourceSystem,
            request.SourceReference, request.CapturedAt, request.IntegrityHash,
            submittedBy: $"user:{trustSubjectId:N}",
            reliabilityClass: EvidenceReliabilityClass.USER_CLAIM,
            description: request.Description,
            accessClassification: EvidenceAccessClass.USER_VISIBLE);

        db.DisputeEvidenceItems.Add(item);
        await RecordEventAsync(trustSubjectId, "dispute.evidence_added", SecurityEventActorType.User, null,
            SecurityEventOutcome.Success, null, minimalMetadataJson: $"{{\"caseId\":\"{caseId:N}\",\"evidenceType\":\"{evType}\"}}", ct: ct);
        await db.SaveChangesAsync(ct);

        return await GetMineAsync(caseId, trustSubjectId, ct);
    }

    public async Task<bool> WithdrawAsync(Guid caseId, Guid trustSubjectId, CancellationToken ct = default)
    {
        var entity = await db.DisputeCases
            .FirstOrDefaultAsync(c => c.Id == caseId && c.TrustSubjectId == trustSubjectId, ct);
        if (entity is null)
            return false;
        try
        {
            entity.Withdraw();
        }
        catch (InvalidOperationException)
        {
            return false; // đã có decision / terminal — không withdraw được
        }

        await RecordEventAsync(trustSubjectId, "dispute.withdrawn", SecurityEventActorType.User, null,
            SecurityEventOutcome.Success, null, minimalMetadataJson: $"{{\"caseId\":\"{caseId:N}\"}}", ct: ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Transaction history (FR-013): resolve mọi Customer link của subject →
    /// entries per (customerId, tenantId) qua ledger gateway, merge + sort desc.
    /// </summary>
    public async Task<IReadOnlyList<LoyaltyLedgerEntryDto>> GetTransactionsAsync(Guid trustSubjectId, Guid tenantId, CancellationToken ct = default)
    {
        var customerIds = await GetCustomerLinksAsync(trustSubjectId, ct);
        var all = new List<LoyaltyLedgerEntryDto>();
        foreach (var customerId in customerIds)
        {
            var entries = await loyaltyGateway.GetEntriesAsync(customerId, tenantId, limit: 100, ct: ct);
            all.AddRange(entries);
        }
        return all.OrderByDescending(e => e.OccurredAt).Take(200).ToList();
    }

    // ══════════════════════════════════════════════════════════
    // STAFF SIDE
    // ══════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<DisputeQueueItemDto>> ListQueueAsync(DisputeQueueQuery query, CancellationToken ct = default)
    {
        var q = db.DisputeCases.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<DisputeCaseStatus>(query.Status, true, out var st))
            q = q.Where(c => c.Status == st);
        if (query.AssignedTo.HasValue)
            q = q.Where(c => c.AssignedReviewerId == query.AssignedTo.Value);

        var cases = await q.OrderBy(c => c.SubmittedAt)
            .Skip(Math.Max(0, query.Skip)).Take(Math.Clamp(query.Take, 1, 200))
            .ToListAsync(ct);

        var caseIds = cases.Select(c => c.Id).ToList();
        var evidenceCounts = await db.DisputeEvidenceItems.AsNoTracking()
            .Where(e => caseIds.Contains(e.DisputeCaseId))
            .GroupBy(e => e.DisputeCaseId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var pendingDecisions = await db.DisputeDecisions.AsNoTracking()
            .Where(d => caseIds.Contains(d.DisputeCaseId) && d.Status == DisputeDecisionStatus.PENDING_APPROVAL)
            .GroupBy(d => d.DisputeCaseId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return cases.Select(c => new DisputeQueueItemDto(
            c.Id, c.CaseType.ToString(), c.Status.ToString(), c.TenantId, c.TrustSubjectId,
            c.AssignedReviewerId, c.SubmittedAt,
            evidenceCounts.GetValueOrDefault(c.Id), pendingDecisions.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<DisputeStaffDetailDto?> GetStaffDetailAsync(Guid caseId, Guid? staffUserId = null, CancellationToken ct = default)
    {
        var entity = await db.DisputeCases.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == caseId, ct);
        if (entity is null)
            return null;

        // FR-014: mọi staff access case → AccountSecurityEvent.
        await RecordEventAsync(entity.TrustSubjectId, "dispute.staff_view", SecurityEventActorType.Staff,
            staffUserId?.ToString(), SecurityEventOutcome.Success, null,
            minimalMetadataJson: $"{{\"caseId\":\"{caseId:N}\"}}", ct: ct);
        await db.SaveChangesAsync(ct);

        var evidence = await db.DisputeEvidenceItems.AsNoTracking()
            .Where(e => e.DisputeCaseId == caseId)
            .OrderBy(e => e.RecordedAt)
            .ToListAsync(ct);
        var decisions = await db.DisputeDecisions.AsNoTracking()
            .Where(d => d.DisputeCaseId == caseId)
            .OrderByDescending(d => d.ProposedAt)
            .ToListAsync(ct);

        // Transactions: resolve customer links → entries tại case.TenantId (nếu có).
        IReadOnlyList<LoyaltyLedgerEntryDto> transactions = [];
        if (entity.TenantId.HasValue)
        {
            var customerIds = await GetCustomerLinksAsync(entity.TrustSubjectId, ct);
            var all = new List<LoyaltyLedgerEntryDto>();
            foreach (var cid in customerIds)
                all.AddRange(await loyaltyGateway.GetEntriesAsync(cid, entity.TenantId.Value, limit: 100, ct: ct));
            transactions = all.OrderByDescending(e => e.OccurredAt).Take(200).ToList();
        }

        return new DisputeStaffDetailDto(
            entity.Id, entity.CaseType.ToString(), entity.Status.ToString(), entity.TenantId,
            entity.TrustSubjectId, entity.ClaimedIssue,
            ParseJsonArray(entity.RelatedTransactionIdsJson),
            ParseJsonArray(entity.RelatedLedgerReferencesJson),
            entity.AssignedReviewerId, entity.SubmittedAt, entity.ClosedAt, entity.UserFacingStatusNote,
            evidence.Select(ToStaffEvidence).ToList(),
            decisions.Select(ToDecisionDto).ToList(),
            transactions);
    }

    public async Task<DisputeQueueItemDto?> AssignAsync(Guid caseId, Guid staffUserId, CancellationToken ct = default)
    {
        var entity = await db.DisputeCases.FirstOrDefaultAsync(c => c.Id == caseId, ct);
        if (entity is null || entity.IsTerminal)
            return null;
        entity.Assign(staffUserId);
        await RecordEventAsync(entity.TrustSubjectId, "dispute.assigned", SecurityEventActorType.Staff, staffUserId.ToString(),
            SecurityEventOutcome.Success, null, minimalMetadataJson: $"{{\"caseId\":\"{caseId:N}\"}}", ct: ct);
        await db.SaveChangesAsync(ct);
        return await GetQueueItemAsync(entity, ct);
    }

    public async Task<DisputeCaseSummaryDto?> TransitionAsync(Guid caseId, Guid staffUserId, DisputeCaseStatus target, string? note, CancellationToken ct = default)
    {
        var entity = await db.DisputeCases.FirstOrDefaultAsync(c => c.Id == caseId, ct);
        if (entity is null)
            return null;
        entity.TransitionTo(target, note);
        await RecordEventAsync(entity.TrustSubjectId, "dispute.transition", SecurityEventActorType.Staff, staffUserId.ToString(),
            SecurityEventOutcome.Success, null,
            minimalMetadataJson: $"{{\"caseId\":\"{caseId:N}\",\"to\":\"{target}\"}}", ct: ct);
        await db.SaveChangesAsync(ct);
        return ToUserSummary(entity);
    }

    /// <summary>
    /// Investigator propose decision → DisputeDecision PENDING_APPROVAL + case → DECIDED.
    /// Mọi decision đều qua maker-checker (approve bởi người khác).
    /// </summary>
    public async Task<DisputeDecisionDto> ProposeDecisionAsync(Guid caseId, Guid staffUserId, ProposeDisputeDecisionRequest request, CancellationToken ct = default)
    {
        var entity = await db.DisputeCases.FirstOrDefaultAsync(c => c.Id == caseId, ct)
            ?? throw new InvalidOperationException($"DisputeCase {caseId} not found.");
        if (entity.IsTerminal)
            throw new InvalidOperationException($"Dispute case is terminal ({entity.Status}).");
        if (entity.Status is not (DisputeCaseStatus.UNDER_REVIEW or DisputeCaseStatus.TRIAGE or DisputeCaseStatus.WAITING_FOR_EXTERNAL_EVIDENCE))
            throw new InvalidOperationException($"Cannot propose decision from {entity.Status} — move to UNDER_REVIEW first.");

        // Chặn propose thứ 2 khi còn decision pending.
        var hasPending = await db.DisputeDecisions.AnyAsync(
            d => d.DisputeCaseId == caseId && d.Status == DisputeDecisionStatus.PENDING_APPROVAL, ct);
        if (hasPending)
            throw new InvalidOperationException("Case already has a pending decision — approve/reject it first.");

        if (!Enum.TryParse<DisputeDecisionKind>(request.Decision, ignoreCase: true, out var kind))
            throw new ArgumentException($"Invalid Decision: {request.Decision}");

        // Adjustment chỉ hợp lệ khi decision là approved-partially (có điểm điều chỉnh).
        var needsAdjustment = request.ProposedAdjustmentPoints is > 0 or < 0;
        if (needsAdjustment && kind is DisputeDecisionKind.REJECTED or DisputeDecisionKind.NO_ACTION)
            throw new ArgumentException("Rejected/NoAction decision cannot carry a points adjustment.");

        var decision = new DisputeDecision(
            Guid.NewGuid(), caseId, kind, request.DecisionReason, staffUserId,
            userFacingExplanation: request.UserFacingExplanation,
            internalRationale: request.InternalRationale,
            policyVersion: string.IsNullOrWhiteSpace(request.PolicyVersion) ? PolicyVersion : request.PolicyVersion,
            proposedAdjustmentPoints: needsAdjustment ? request.ProposedAdjustmentPoints : null,
            proposedAdjustmentTenantId: needsAdjustment ? request.ProposedAdjustmentTenantId ?? entity.TenantId : null,
            proposedAdjustmentReason: needsAdjustment ? request.ProposedAdjustmentReason : null);

        db.DisputeDecisions.Add(decision);

        // Case → UNDER_REVIEW (nếu đang TRIAGE/WAITING_*) rồi → DECIDED (pending approval).
        if (entity.Status != DisputeCaseStatus.UNDER_REVIEW)
            entity.TransitionTo(DisputeCaseStatus.UNDER_REVIEW);
        entity.MarkDecidedPending();

        await RecordEventAsync(entity.TrustSubjectId, "dispute.decision_proposed", SecurityEventActorType.Staff, staffUserId.ToString(),
            SecurityEventOutcome.Pending, null,
            minimalMetadataJson: $"{{\"caseId\":\"{caseId:N}\",\"decisionId\":\"{decision.Id:N}\",\"kind\":\"{kind}\"}}", ct: ct);
        await db.SaveChangesAsync(ct);
        return ToDecisionDto(decision);
    }

    /// <summary>
    /// Approver duyệt → maker-checker → execute ledger adjustment (nếu có) →
    /// case RESOLVED/REJECTED CHỈ sau khi ledger confirm. Idempotent theo decision.Id.
    /// </summary>
    public async Task<DisputeDecisionDto> ApproveDecisionAsync(Guid decisionId, Guid approverId, CancellationToken ct = default)
    {
        var decision = await db.DisputeDecisions.FirstOrDefaultAsync(d => d.Id == decisionId, ct)
            ?? throw new InvalidOperationException($"DisputeDecision {decisionId} not found.");
        var entity = await db.DisputeCases.FirstOrDefaultAsync(c => c.Id == decision.DisputeCaseId, ct)
            ?? throw new InvalidOperationException($"DisputeCase {decision.DisputeCaseId} not found.");

        bool makerCheckerRequired = await policyService.GetEffectiveBoolAsync(
            SecurityPolicyKeys.DisputeMakerCheckerRequired, "required", fallback: true, ct);
        bool needsAdjustment = decision.Approve(approverId, makerCheckerRequired); // maker-checker enforce ở đây (A21 policy)

        if (!needsAdjustment)
        {
            // Decision không adjustment → resolve/reject ngay (ledger không liên quan).
            // MarkExecuted với reference rõ ràng để audit phân biệt "đã chạy ledger" vs "không cần ledger".
            decision.MarkExecuted($"no-ledger-adjustment:{decision.Id:N}");
            FinalizeCaseWithoutAdjustment(entity, decision);
            await RecordEventAsync(entity.TrustSubjectId, "dispute.decision_approved", SecurityEventActorType.Staff, approverId.ToString(),
                SecurityEventOutcome.Success, null,
                minimalMetadataJson: $"{{\"caseId\":\"{entity.Id:N}\",\"decisionId\":\"{decision.Id:N}\"}}", ct: ct);
            await db.SaveChangesAsync(ct);
            return ToDecisionDto(decision);
        }

        // Execute adjustment qua ledger gateway — idempotent key đã gắn trên decision.
        var (customerId, resolveError) = await ResolveAdjustmentCustomerAsync(entity, ct);
        if (customerId is null)
        {
            decision.MarkFailed();
            entity.TransitionTo(DisputeCaseStatus.UNDER_REVIEW, "Điều chỉnh thất bại: không xác định được customer ledger.");
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Dispute {CaseId} decision {DecisionId} FAILED — {Error}", entity.Id, decision.Id, resolveError);
            return ToDecisionDto(decision);
        }

        var adjust = await loyaltyGateway.AdjustPointsAsync(
            customerId.Value, decision.ProposedAdjustmentTenantId!.Value,
            decision.ProposedAdjustmentPoints!.Value,
            $"Dispute {entity.Id:N}: {decision.ProposedAdjustmentReason}",
            decision.IdempotencyKey!, ct);

        if (!adjust.Success)
        {
            decision.MarkFailed();
            entity.TransitionTo(DisputeCaseStatus.UNDER_REVIEW, "Điều chỉnh điểm thất bại — vui lòng thử lại hoặc liên hệ hỗ trợ.");
            await RecordEventAsync(entity.TrustSubjectId, "dispute.adjust_failed", SecurityEventActorType.Staff, approverId.ToString(),
                SecurityEventOutcome.Failure, "ledger-adjust-failed",
                minimalMetadataJson: $"{{\"caseId\":\"{entity.Id:N}\",\"decisionId\":\"{decision.Id:N}\"}}", ct: ct);
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Dispute {CaseId} adjust FAILED: {Error}", entity.Id, adjust.Error);
            return ToDecisionDto(decision);
        }

        decision.MarkExecuted(adjust.ResolutionReference ?? decision.IdempotencyKey!);
        entity.MarkResolved();
        await RecordEventAsync(entity.TrustSubjectId, "dispute.adjust_executed", SecurityEventActorType.Staff, approverId.ToString(),
            SecurityEventOutcome.Success, null,
            minimalMetadataJson: $"{{\"caseId\":\"{entity.Id:N}\",\"decisionId\":\"{decision.Id:N}\",\"ref\":\"{decision.ResolutionReference}\"}}", ct: ct);
        await db.SaveChangesAsync(ct);
        return ToDecisionDto(decision);
    }

    public async Task<DisputeDecisionDto> RejectDecisionAsync(Guid decisionId, Guid approverId, string reason, CancellationToken ct = default)
    {
        var decision = await db.DisputeDecisions.FirstOrDefaultAsync(d => d.Id == decisionId, ct)
            ?? throw new InvalidOperationException($"DisputeDecision {decisionId} not found.");
        var entity = await db.DisputeCases.FirstOrDefaultAsync(c => c.Id == decision.DisputeCaseId, ct)
            ?? throw new InvalidOperationException($"DisputeCase {decision.DisputeCaseId} not found.");

        decision.RejectByApprover(approverId, reason);
        // Proposal bị từ chối → case quay lại UNDER_REVIEW để investigator làm lại.
        if (entity.Status == DisputeCaseStatus.DECIDED)
            entity.TransitionTo(DisputeCaseStatus.UNDER_REVIEW, "Quyết định bị từ chối — đang xem xét lại.");

        await RecordEventAsync(entity.TrustSubjectId, "dispute.decision_rejected", SecurityEventActorType.Staff, approverId.ToString(),
            SecurityEventOutcome.Success, null,
            minimalMetadataJson: $"{{\"caseId\":\"{entity.Id:N}\",\"decisionId\":\"{decision.Id:N}\"}}", ct: ct);
        await db.SaveChangesAsync(ct);
        return ToDecisionDto(decision);
    }

    // ══════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════

    private static readonly DisputeCaseStatus[] TerminalStatuses =
        [DisputeCaseStatus.RESOLVED, DisputeCaseStatus.REJECTED, DisputeCaseStatus.CLOSED];

    /// <summary>Customer links (IdentityType=Customer) của subject — dùng resolve ledger customerId.</summary>
    private async Task<IReadOnlyList<Guid>> GetCustomerLinksAsync(Guid trustSubjectId, CancellationToken ct)
        => await db.TrustSubjectLinks.AsNoTracking()
            .Where(l => l.TrustSubjectId == trustSubjectId && l.IdentityType == TrustIdentityType.Customer)
            .Select(l => l.IdentityId)
            .ToListAsync(ct);

    /// <summary>
    /// Resolve customerId cho adjustment: ưu tiên link có ledger entries tại case.TenantId;
    /// fallback link đầu tiên. Trả (null, error) khi subject không có Customer link nào.
    /// </summary>
    private async Task<(Guid? CustomerId, string? Error)> ResolveAdjustmentCustomerAsync(DisputeCase entity, CancellationToken ct)
    {
        var links = await GetCustomerLinksAsync(entity.TrustSubjectId, ct);
        if (links.Count == 0)
            return (null, "Subject has no Customer identity link.");
        if (links.Count == 1)
            return (links[0], null);
        if (!entity.TenantId.HasValue)
            return (links[0], null);

        foreach (var cid in links)
        {
            var entries = await loyaltyGateway.GetEntriesAsync(cid, entity.TenantId.Value, limit: 5, ct: ct);
            if (entries.Count > 0)
                return (cid, null);
        }
        return (links[0], null);
    }

    private static void FinalizeCaseWithoutAdjustment(DisputeCase entity, DisputeDecision decision)
    {
        // Decision approved, không adjustment: Approved/PartiallyApproved → RESOLVED (giải thích cho user);
        // Rejected/NoAction → REJECTED.
        if (decision.Decision is DisputeDecisionKind.REJECTED or DisputeDecisionKind.NO_ACTION)
            entity.MarkRejected();
        else
            entity.MarkResolved();
    }

    private async Task RecordEventAsync(
        Guid trustSubjectId, string eventType, SecurityEventActorType actorType, string? actorId,
        SecurityEventOutcome outcome, string? reasonCode,
        string? evidenceReferencesJson = null, string? minimalMetadataJson = null, CancellationToken ct = default)
    {
        db.AccountSecurityEvents.Add(AccountSecurityEvent.Create(
            Guid.NewGuid(), trustSubjectId, eventType, actorType, actorId,
            sourceApplication: "vanan-atrs", deviceId: null, sessionCorrelationId: null,
            outcome, reasonCode, evidenceReferencesJson, minimalMetadataJson));
        await Task.CompletedTask;
    }

    private async Task<DisputeQueueItemDto?> GetQueueItemAsync(DisputeCase entity, CancellationToken ct)
    {
        var evidenceCount = await db.DisputeEvidenceItems.CountAsync(e => e.DisputeCaseId == entity.Id, ct);
        var pendingCount = await db.DisputeDecisions.CountAsync(
            d => d.DisputeCaseId == entity.Id && d.Status == DisputeDecisionStatus.PENDING_APPROVAL, ct);
        return new DisputeQueueItemDto(entity.Id, entity.CaseType.ToString(), entity.Status.ToString(),
            entity.TenantId, entity.TrustSubjectId, entity.AssignedReviewerId, entity.SubmittedAt,
            evidenceCount, pendingCount);
    }

    private static DisputeCaseSummaryDto ToUserSummary(DisputeCase c)
        => new(c.Id, c.CaseType.ToString(), c.Status.ToString(), c.TenantId, c.ClaimedIssue,
            c.SubmittedAt, c.ClosedAt, c.UserFacingStatusNote);

    private static DisputeCaseDetailDto ToUserDetail(DisputeCase c, List<DisputeEvidenceItem> evidence)
        => new(c.Id, c.CaseType.ToString(), c.Status.ToString(), c.TenantId, c.ClaimedIssue,
            ParseJsonArray(c.RelatedTransactionIdsJson), ParseJsonArray(c.RelatedLedgerReferencesJson),
            c.SubmittedAt, c.ClosedAt, c.UserFacingStatusNote,
            evidence.Select(e => new DisputeEvidenceItemDto(e.Id, e.EvidenceType.ToString(),
                e.SourceReference, e.Description, e.RecordedAt)).ToList(),
            BuildUserTimeline(c));

    private static DisputeEvidenceItemDto ToStaffEvidence(DisputeEvidenceItem e)
        => new(e.Id, e.EvidenceType.ToString(), e.SourceReference, e.Description, e.RecordedAt,
            e.AccessClassification.ToString(), e.ReliabilityClass.ToString(), e.SubmittedBy);

    private static DisputeDecisionDto ToDecisionDto(DisputeDecision d)
        => new(d.Id, d.Decision.ToString(), d.Status.ToString(), d.DecisionReason,
            d.UserFacingExplanation, d.InternalRationale,
            d.ProposedAdjustmentPoints, d.ProposedAdjustmentTenantId, d.ProposedAdjustmentReason,
            d.ProposedBy, d.ApprovedBy, d.ProposedAt, d.ExecutedAt, d.ResolutionReference, d.RejectionReason);

    private static List<string> ParseJsonArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>User-visible timeline — CHỈ trạng thái an toàn, không lộ fraud/internal (U13 rule).</summary>
    private static List<string> BuildUserTimeline(DisputeCase c)
    {
        var timeline = new List<string> { $"submitted:{c.SubmittedAt:O}" };
        if (c.ClosedAt.HasValue)
            timeline.Add($"closed:{c.ClosedAt:O}:{c.Status}");
        return timeline;
    }
}
