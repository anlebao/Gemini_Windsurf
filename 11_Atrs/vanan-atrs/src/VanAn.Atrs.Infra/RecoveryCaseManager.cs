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
    Task<IReadOnlyList<RecoveryRequestSummaryDto>> ListQueueAsync(string? status, int skip, int take, CancellationToken ct = default);
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
    ITokenVersionBumpNotifier tokenVersionBumpNotifier) : IRecoveryCaseManager
{
    private static readonly RecoveryRequestStatus[] ActiveStatuses =
    [
        RecoveryRequestStatus.SUBMITTED,
        RecoveryRequestStatus.AUTOMATED_CHECK,
        RecoveryRequestStatus.PENDING_USER_ACTION,
        RecoveryRequestStatus.PENDING_REVIEW
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
        await db.SaveChangesAsync(ct);
        return ToDto(request);
    }

    public async Task<RecoveryRequestSummaryDto> ManualRejectAsync(Guid requestId, Guid reviewerId, string reasonCode, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        if (request.Status == RecoveryRequestStatus.SUBMITTED)
            request.StartAutomatedCheck();
        request.Reject(reviewerId, reasonCode);
        await db.SaveChangesAsync(ct);
        return ToDto(request);
    }

    /// <summary>ATRS-10 subset (A15/A16): staff request more info — PENDING_USER_ACTION.</summary>
    public async Task<RecoveryRequestSummaryDto> RequestMoreInfoAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default)
    {
        var request = await LoadAsync(requestId, ct);
        request.RequestMoreInfo();
        await db.SaveChangesAsync(ct);
        return ToDto(request);
    }

    /// <summary>ATRS-10 subset (A15): recovery queue — server-side filter + paging.</summary>
    public async Task<IReadOnlyList<RecoveryRequestSummaryDto>> ListQueueAsync(string? status, int skip, int take, CancellationToken ct = default)
    {
        var q = db.RecoveryRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RecoveryRequestStatus>(status, true, out var st))
            q = q.Where(r => r.Status == st);
        var items = await q.OrderBy(r => r.CreatedAt)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
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
}