using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// RecoveryCaseManager — quy trình recovery (FR-011, tiered proof ① recovery code).
/// Wave 1 minimal: submit (idempotent + anti-duplicate) → automated check →
/// verify code path → risk engine → approve/review/reject → complete.
/// Hooks chưa implement (đánh dấu ở card ATRS-8/ATRS-3): token revocation
/// (TokenVersion bump), revoke credential/device cũ, delay giao dịch nhạy cảm,
/// notification — sẽ wire khi ATRS-3/13.
/// </summary>
public interface IRecoveryCaseManager
{
    Task<SubmitRecoveryResultDto> SubmitAsync(
        Guid trustSubjectId, RecoveryRequestType requestType, string idempotencyKey,
        Guid? newDeviceId = null, string? evidenceSummaryJson = null, CancellationToken ct = default);

    /// <summary>Tiered proof ①: verify recovery code → risk engine → quyết định.</summary>
    Task<VerifyRecoveryCodePathResult> VerifyCodePathAsync(
        Guid requestId, string code, Guid? deviceId, bool highAccountValue = false, CancellationToken ct = default);

    Task<RecoveryRequestSummaryDto> ManualApproveAsync(Guid requestId, Guid reviewerId, CancellationToken ct = default);
    Task<RecoveryRequestSummaryDto> ManualRejectAsync(Guid requestId, Guid reviewerId, string reasonCode, CancellationToken ct = default);
    Task<RecoveryRequestSummaryDto> RequestMoreInfoAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default);
    /// <summary>A15 — recovery queue: server-side filter + paging + total + SLA.</summary>
    Task<RecoveryQueuePageDto> ListQueuePageAsync(RecoveryQueueQuery query, CancellationToken ct = default);
    /// <summary>A18-style — staff detail: request + risk + notes + devices + links + timeline (audit staff_view).</summary>
    Task<RecoveryStaffDetailDto?> GetStaffDetailAsync(Guid requestId, Guid? staffUserId = null, CancellationToken ct = default);
    /// <summary>A15/A16 — assign case cho staff (case.assign).</summary>
    Task<RecoveryQueueItemDto?> AssignAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default);
    /// <summary>A16 — add internal note / recommendation (recovery.review).</summary>
    Task<RecoveryCaseNoteDto> AddNoteAsync(Guid requestId, Guid authorId, string body, RecoveryNoteType noteType, CancellationToken ct = default);
    /// <summary>A16 — escalate lên cấp xử lý cao hơn (recovery.review).</summary>
    Task<RecoveryRequestSummaryDto> EscalateAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default);
    /// <summary>A16 — de-escalate: trả case về PENDING_REVIEW.</summary>
    Task<RecoveryRequestSummaryDto> ReturnToReviewAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default);
    /// <summary>A14 — operations dashboard aggregate (data thật, không hardcode).</summary>
    Task<AtrsDashboardDto> GetDashboardAsync(CancellationToken ct = default);
    /// <summary>Wave 4 (U10): recovery requests của chính subject — resume pending.</summary>
    Task<IReadOnlyList<RecoveryRequestSummaryDto>> ListMineAsync(Guid trustSubjectId, CancellationToken ct = default);
    /// <summary>Wave 4 (U10): user tự huỷ request đang mở (SUBMITTED/AUTOMATED_CHECK/PENDING_USER_ACTION).</summary>
    Task<RecoveryRequestSummaryDto?> CancelAsync(Guid requestId, Guid trustSubjectId, CancellationToken ct = default);
    Task<RecoveryRequestSummaryDto> CompleteAsync(Guid requestId, Guid? newDeviceId = null, CancellationToken ct = default);
    Task<RecoveryRequestSummaryDto?> GetAsync(Guid requestId, CancellationToken ct = default);
}

public sealed class RecoveryCaseManager(
    AtrsDbContext db,
    IRecoveryCodeService recoveryCodeService,
    IRiskPolicyEngine riskPolicyEngine,
    ITokenVersionBumpNotifier tokenVersionBumpNotifier,
    ISecurityPolicyService policyService,
    IAtrsNotificationWriter notifications,
    AtrsMetrics metrics) : IRecoveryCaseManager
{
    private static readonly RecoveryRequestStatus[] ActiveStatuses =
    [
        RecoveryRequestStatus.SUBMITTED,
        RecoveryRequestStatus.AUTOMATED_CHECK,
        RecoveryRequestStatus.PENDING_USER_ACTION,
        RecoveryRequestStatus.PENDING_REVIEW,
        RecoveryRequestStatus.ESCALATED
    ];

    /// <summary>SLA (A15/A14): pending > N giờ = breached — mặc định 24h, override qua policy `recovery.sla_hours` (A21).</summary>
    public const double DefaultSlaThresholdHours = 24;

    private static readonly DisputeCaseStatus[] TerminalDisputeStatuses =
    [
        DisputeCaseStatus.RESOLVED,
        DisputeCaseStatus.REJECTED,
        DisputeCaseStatus.CLOSED
    ];

    private static readonly RecoveryRequestStatus[] TerminalRecoveryStatuses =
    [
        RecoveryRequestStatus.APPROVED,
        RecoveryRequestStatus.REJECTED,
        RecoveryRequestStatus.CANCELLED,
        RecoveryRequestStatus.EXPIRED,
        RecoveryRequestStatus.COMPLETED
    ];

    public async Task<SubmitRecoveryResultDto> SubmitAsync(
        Guid trustSubjectId, RecoveryRequestType requestType, string idempotencyKey,
        Guid? newDeviceId = null, string? evidenceSummaryJson = null, CancellationToken ct = default)
    {
        // Idempotency (FR-011): cùng key → trả request cũ.
        var byKey = await db.RecoveryRequests.FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);
        if (byKey is not null)
            return new SubmitRecoveryResultDto(ToDto(byKey), Existing: true);

        // Anti-duplicate (FR-011): đang có request active → trả cái đó.
        var active = await db.RecoveryRequests
            .Where(r => r.TrustSubjectId == trustSubjectId && ActiveStatuses.Contains(r.Status))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (active is not null)
            return new SubmitRecoveryResultDto(ToDto(active), Existing: true);

        var request = new RecoveryRequest(
            Guid.NewGuid(), trustSubjectId, requestType, idempotencyKey, newDeviceId, evidenceSummaryJson);
        db.RecoveryRequests.Add(request);
        await db.SaveChangesAsync(ct);
        metrics.Record("recovery.created");
        await notifications.NotifyAsync(trustSubjectId, "recovery.initiated",
            "Yêu cầu khôi phục đã gửi",
            $"Yêu cầu khôi phục ({requestType}) đang được xử lý. Theo dõi trạng thái trong Trung tâm bảo mật.", ct: ct);
        return new SubmitRecoveryResultDto(ToDto(request), Existing: false);
    }

    public async Task<VerifyRecoveryCodePathResult> VerifyCodePathAsync(
        Guid requestId, string code, Guid? deviceId, bool highAccountValue = false, CancellationToken ct = default)
    {
        var request = await db.RecoveryRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null)
            return new VerifyRecoveryCodePathResult(false, "NotFound", null, null);

        if (request.Status is not (RecoveryRequestStatus.SUBMITTED
            or RecoveryRequestStatus.AUTOMATED_CHECK
            or RecoveryRequestStatus.PENDING_USER_ACTION))
            return new VerifyRecoveryCodePathResult(false, "WrongState", ToDto(request), null);

        if (request.Status == RecoveryRequestStatus.SUBMITTED)
            request.StartAutomatedCheck();

        var verify = await recoveryCodeService.VerifyAndConsumeAsync(request.TrustSubjectId, code, deviceId, ct);
        if (!verify.Success)
        {
            await db.SaveChangesAsync(ct);
            return new VerifyRecoveryCodePathResult(false, verify.Reason, ToDto(request), null);
        }

        var ctx = new RiskContext(
            request.TrustSubjectId,
            Action: "recovery.submit",
            IsNewDevice: deviceId.HasValue,
            HighAccountValue: highAccountValue);

        var decision = riskPolicyEngine.Evaluate(ctx);
        var (nextStatus, reasonCode) = decision.Decision switch
        {
            RiskDecisionKind.ALLOW or RiskDecisionKind.ALLOW_WITH_STEP_UP
                => (RecoveryRequestStatus.APPROVED, "code-verified"),
            RiskDecisionKind.REQUIRE_MANUAL_REVIEW or RiskDecisionKind.TEMPORARY_RESTRICT
                => (RecoveryRequestStatus.PENDING_REVIEW, "manual-review"),
            _ => (RecoveryRequestStatus.REJECTED, "policy-deny")
        };

        request.ApplyAutomatedDecision(
            JsonSerializer.Serialize(decision),
            nextStatus,
            reasonCode);
        await db.SaveChangesAsync(ct);
        return new VerifyRecoveryCodePathResult(true, "Success", ToDto(request), decision.Decision.ToString());
    }

    public async Task<RecoveryRequestSummaryDto> ManualApproveAsync(Guid requestId, Guid reviewerId, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        if (request.Status == RecoveryRequestStatus.SUBMITTED)
            request.StartAutomatedCheck(); // reviewer xử lý case mới: vào automated check trước
        request.Approve(reviewerId);
        await RecordStaffEventAsync(request.TrustSubjectId, "recovery.approved", reviewerId,
            $"{{\"requestId\":\"{requestId:N}\"}}", ct);
        await db.SaveChangesAsync(ct);
        metrics.Record("recovery.approved");
        await notifications.NotifyAsync(request.TrustSubjectId, "recovery.approved",
            "Yêu cầu khôi phục được duyệt",
            "Yêu cầu khôi phục tài khoản của bạn đã được duyệt. Hãy đăng nhập thiết bị mới để hoàn tất.", ct: ct);
        return ToDto(request);
    }

    public async Task<RecoveryRequestSummaryDto> ManualRejectAsync(Guid requestId, Guid reviewerId, string reasonCode, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        if (request.Status == RecoveryRequestStatus.SUBMITTED)
            request.StartAutomatedCheck();
        request.Reject(reviewerId, reasonCode);
        await RecordStaffEventAsync(request.TrustSubjectId, "recovery.rejected", reviewerId,
            $"{{\"requestId\":\"{requestId:N}\",\"reasonCode\":\"{reasonCode}\"}}", ct);
        await db.SaveChangesAsync(ct);
        metrics.Record("recovery.rejected");
        await notifications.NotifyAsync(request.TrustSubjectId, "recovery.rejected",
            "Yêu cầu khôi phục bị từ chối",
            "Yêu cầu khôi phục tài khoản không được duyệt. Liên hệ hỗ trợ nếu bạn cần trợ giúp.", ct: ct);
        return ToDto(request);
    }

    /// <summary>ATRS-10 subset (A15/A16): staff request more info — PENDING_USER_ACTION.</summary>
    public async Task<RecoveryRequestSummaryDto> RequestMoreInfoAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        request.RequestMoreInfo();
        await RecordStaffEventAsync(request.TrustSubjectId, "recovery.info_requested", staffUserId,
            $"{{\"requestId\":\"{requestId:N}\"}}", ct);
        await db.SaveChangesAsync(ct);
        return ToDto(request);
    }

    /// <summary>A15 — recovery queue: server-side filter + paging + total + SLA indicator.</summary>
    public async Task<RecoveryQueuePageDto> ListQueuePageAsync(RecoveryQueueQuery query, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var slaHours = await GetSlaHoursAsync(ct);
        var q = db.RecoveryRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<RecoveryRequestStatus>(query.Status, true, out var st))
            q = q.Where(r => r.Status == st);
        if (!string.IsNullOrWhiteSpace(query.RequestType) && Enum.TryParse<RecoveryRequestType>(query.RequestType, true, out var rt))
            q = q.Where(r => r.RequestType == rt);
        if (query.AssignedTo.HasValue)
            q = q.Where(r => r.AssignedReviewerId == query.AssignedTo.Value);
        if (query.Unassigned)
            q = q.Where(r => r.AssignedReviewerId == null);
        if (query.SubjectId.HasValue)
            q = q.Where(r => r.TrustSubjectId == query.SubjectId.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.CreatedAt)
            .Skip(Math.Max(0, query.Skip)).Take(Math.Clamp(query.Take, 1, 200))
            .ToListAsync(ct);
        return new RecoveryQueuePageDto(
            items.Select(r => ToQueueItem(r, now, slaHours)).ToList(),
            total, Math.Max(0, query.Skip), Math.Clamp(query.Take, 1, 200));
    }

    /// <summary>A16 — staff detail: request + risk + notes + devices + links + timeline (audit staff_view).</summary>
    public async Task<RecoveryStaffDetailDto?> GetStaffDetailAsync(Guid requestId, Guid? staffUserId = null, CancellationToken ct = default)
    {
        var request = await db.RecoveryRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null)
            return null;

        // FR-014: mọi staff access case → AccountSecurityEvent.
        await RecordStaffEventAsync(request.TrustSubjectId, "recovery.staff_view", staffUserId,
            $"{{\"requestId\":\"{requestId:N}\"}}", ct);

        var notes = await db.RecoveryCaseNotes.AsNoTracking()
            .Where(n => n.RecoveryRequestId == requestId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        var devices = await db.UserDevices.AsNoTracking()
            .Where(d => d.TrustSubjectId == request.TrustSubjectId)
            .OrderByDescending(d => d.LastSeenAt)
            .Take(50)
            .ToListAsync(ct);
        var links = await db.TrustSubjectLinks.AsNoTracking()
            .Where(l => l.TrustSubjectId == request.TrustSubjectId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
        var events = await db.AccountSecurityEvents.AsNoTracking()
            .Where(e => e.TrustSubjectId == request.TrustSubjectId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(20)
            .ToListAsync(ct);

        return new RecoveryStaffDetailDto(
            request.Id, request.RequestType.ToString(), request.Status.ToString(), request.TrustSubjectId,
            request.NewDeviceId,
            request.ReviewerId?.ToString(), request.AssignedReviewerId, request.AssignedAt,
            request.DecisionReasonCode, request.CreatedAt, request.CompletedAt,
            ParseRisk(request.RiskDecisionJson),
            notes.Select(n => new RecoveryCaseNoteDto(n.Id, n.AuthorId, n.NoteType.ToString(), n.Body, n.CreatedAt)).ToList(),
            devices.Select(d => new RecoveryStaffDeviceDto(
                d.Id, d.DeviceId, d.FriendlyName, d.Platform, d.DeviceType.ToString(), d.Status.ToString(),
                d.FirstSeenAt, d.LastSeenAt)).ToList(),
            links.Select(l => new RecoverySubjectLinkDto(l.IdentityType.ToString(), l.IdentityId, l.TenantId)).ToList(),
            events.Select(e => new RecoverySecurityEventDto(e.EventType, e.OccurredAt, e.Outcome.ToString(), e.ActorType.ToString())).ToList());
    }

    /// <summary>A15/A16 — assign case cho staff.</summary>
    public async Task<RecoveryQueueItemDto?> AssignAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default)
    {
        var request = await db.RecoveryRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null)
            return null;
        try
        {
            request.Assign(staffUserId);
        }
        catch (InvalidOperationException)
        {
            return null; // terminal — không assign được
        }
        await RecordStaffEventAsync(request.TrustSubjectId, "recovery.assigned", staffUserId,
            $"{{\"requestId\":\"{requestId:N}\"}}", ct);
        await db.SaveChangesAsync(ct);
        return ToQueueItem(request, DateTime.UtcNow, await GetSlaHoursAsync(ct));
    }

    /// <summary>A16 — add internal note / recommendation (recommend KHÔNG phải quyết định cuối).</summary>
    public async Task<RecoveryCaseNoteDto> AddNoteAsync(Guid requestId, Guid authorId, string body, RecoveryNoteType noteType, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        var note = new RecoveryCaseNote(Guid.NewGuid(), requestId, authorId, noteType, body);
        db.RecoveryCaseNotes.Add(note);
        await RecordStaffEventAsync(request.TrustSubjectId, "recovery.note_added", authorId,
            $"{{\"requestId\":\"{requestId:N}\",\"noteType\":\"{noteType}\"}}", ct);
        await db.SaveChangesAsync(ct);
        return new RecoveryCaseNoteDto(note.Id, note.AuthorId, note.NoteType.ToString(), note.Body, note.CreatedAt);
    }

    /// <summary>A16 — escalate lên cấp xử lý cao hơn.</summary>
    public async Task<RecoveryRequestSummaryDto> EscalateAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        request.Escalate();
        await RecordStaffEventAsync(request.TrustSubjectId, "recovery.escalated", staffUserId,
            $"{{\"requestId\":\"{requestId:N}\"}}", ct);
        await db.SaveChangesAsync(ct);
        return ToDto(request);
    }

    /// <summary>A16 — de-escalate: trả case về PENDING_REVIEW.</summary>
    public async Task<RecoveryRequestSummaryDto> ReturnToReviewAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        request.ReturnToReview();
        await RecordStaffEventAsync(request.TrustSubjectId, "recovery.deescalated", staffUserId,
            $"{{\"requestId\":\"{requestId:N}\"}}", ct);
        await db.SaveChangesAsync(ct);
        return ToDto(request);
    }

    /// <summary>A14 — operations dashboard aggregate (data thật, không hardcode).</summary>
    public async Task<AtrsDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var since24h = now.AddHours(-24);
        var slaHours = await GetSlaHoursAsync(ct);
        var slaCutoff = now.AddHours(-slaHours);

        var newRecovery24h = await db.RecoveryRequests.CountAsync(r => r.CreatedAt >= since24h, ct);
        var awaitingReview = await db.RecoveryRequests.CountAsync(r => r.Status == RecoveryRequestStatus.PENDING_REVIEW, ct);
        var escalated = await db.RecoveryRequests.CountAsync(r => r.Status == RecoveryRequestStatus.ESCALATED, ct);
        var needsInfo = await db.RecoveryRequests.CountAsync(r => r.Status == RecoveryRequestStatus.PENDING_USER_ACTION, ct);
        var openDisputes = await db.DisputeCases.CountAsync(c => !TerminalDisputeStatuses.Contains(c.Status), ct);
        var slaOverdue = await db.RecoveryRequests.CountAsync(
            r => !TerminalRecoveryStatuses.Contains(r.Status) && r.CreatedAt < slaCutoff, ct);
        var incidents24h = await db.AccountSecurityEvents.CountAsync(
            e => e.OccurredAt >= since24h && (e.Outcome == SecurityEventOutcome.Failure || e.Outcome == SecurityEventOutcome.Blocked), ct);

        return new AtrsDashboardDto(
            newRecovery24h, awaitingReview, escalated, needsInfo, openDisputes, slaOverdue, incidents24h, now);
    }

    /// <summary>U10: list recovery requests của subject (mới nhất trước, tối đa 50).</summary>
    public async Task<IReadOnlyList<RecoveryRequestSummaryDto>> ListMineAsync(Guid trustSubjectId, CancellationToken ct = default)
    {
        var items = await db.RecoveryRequests.AsNoTracking()
            .Where(r => r.TrustSubjectId == trustSubjectId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    /// <summary>U10: user cancel — ownership check + state machine (chỉ trạng thái mở).</summary>
    public async Task<RecoveryRequestSummaryDto?> CancelAsync(Guid requestId, Guid trustSubjectId, CancellationToken ct = default)
    {
        var request = await db.RecoveryRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TrustSubjectId == trustSubjectId, ct);
        if (request is null)
            return null;
        try
        {
            request.Cancel();
        }
        catch (InvalidOperationException)
        {
            return ToDto(request); // đã terminal/không cancel được — trả state hiện tại
        }
        db.AccountSecurityEvents.Add(AccountSecurityEvent.Create(
            Guid.NewGuid(), trustSubjectId, "recovery.cancelled", SecurityEventActorType.User, null,
            sourceApplication: "vanan-atrs", deviceId: null, sessionCorrelationId: null,
            SecurityEventOutcome.Success, null,
            minimalMetadataJson: $"{{\"requestId\":\"{requestId:N}\"}}"));
        await db.SaveChangesAsync(ct);
        return ToDto(request);
    }

    public async Task<RecoveryRequestSummaryDto> CompleteAsync(Guid requestId, Guid? newDeviceId = null, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        request.Complete(newDeviceId);
        await db.SaveChangesAsync(ct);
        metrics.Record("recovery.completed");
        await notifications.NotifyAsync(request.TrustSubjectId, "recovery.completed",
            "Khôi phục hoàn tất",
            "Tài khoản đã được khôi phục và thiết bị mới đã được liên kết.", ct: ct);

        // ATRS-3 (D3): recovery COMPLETED → bump token version mọi identity của subject
        // (revoke token cũ). Best-effort: fail không làm hỏng recovery (retry sau).
        try
        {
            var links = await db.TrustSubjectLinks
                .Where(l => l.TrustSubjectId == request.TrustSubjectId)
                .ToListAsync(ct);
            foreach (var link in links)
            {
                var identityType = link.IdentityType == TrustIdentityType.Customer ? "Customer" : "DemoUser";
                await tokenVersionBumpNotifier.BumpAsync(link.IdentityId, identityType, ct);
            }
        }
        catch (Exception)
        {
            // best-effort — log bên Api; recovery đã COMPLETED, revoke có thể retry thủ công.
        }

        return ToDto(request);
    }

    public async Task<RecoveryRequestSummaryDto?> GetAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await db.RecoveryRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        return request is null ? null : ToDto(request);
    }

    private async Task<RecoveryRequest> LoadAsync(Guid requestId, CancellationToken ct)
        => await db.RecoveryRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new InvalidOperationException($"RecoveryRequest {requestId} not found.");

    private static RecoveryRequestSummaryDto ToDto(RecoveryRequest r)
        => new(
            r.Id, r.RequestType.ToString(), r.Status.ToString(), r.TrustSubjectId,
            r.NewDeviceId, r.RiskDecisionJson,
            r.ReviewerId?.ToString(), r.DecisionReasonCode, r.CreatedAt, r.CompletedAt);

    private static RecoveryQueueItemDto ToQueueItem(RecoveryRequest r, DateTime now, double slaHours)
    {
        var ageHours = (now - r.CreatedAt).TotalHours;
        return new RecoveryQueueItemDto(
            r.Id, r.RequestType.ToString(), r.Status.ToString(), r.TrustSubjectId,
            r.NewDeviceId, r.ReviewerId, r.AssignedReviewerId, r.AssignedAt,
            r.DecisionReasonCode, r.CreatedAt, r.CompletedAt,
            Math.Round(ageHours, 1), !r.IsTerminal && ageHours > slaHours);
    }

    private async Task<double> GetSlaHoursAsync(CancellationToken ct)
        => await policyService.GetEffectiveNumberAsync(SecurityPolicyKeys.RecoverySlaHours, "hours", DefaultSlaThresholdHours, ct);

    private static RecoveryRiskSummaryDto? ParseRisk(string? riskDecisionJson)
    {
        if (string.IsNullOrWhiteSpace(riskDecisionJson))
            return null;
        try
        {
            var risk = JsonSerializer.Deserialize<RiskDecision>(riskDecisionJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return risk is null
                ? null
                : new RecoveryRiskSummaryDto(risk.Decision.ToString(), risk.PolicyVersion, risk.ReasonCodes.ToList());
        }
        catch (JsonException)
        {
            return null; // payload cũ/hỏng — không chặn staff review
        }
    }

    private async Task RecordStaffEventAsync(
        Guid trustSubjectId, string eventType, Guid? staffUserId, string? metadataJson, CancellationToken ct)
    {
        db.AccountSecurityEvents.Add(AccountSecurityEvent.Create(
            Guid.NewGuid(), trustSubjectId, eventType, SecurityEventActorType.Staff,
            staffUserId?.ToString(), sourceApplication: "vanan-atrs", deviceId: null,
            sessionCorrelationId: null, SecurityEventOutcome.Success, null,
            minimalMetadataJson: metadataJson));
        await db.SaveChangesAsync(ct);
    }
}