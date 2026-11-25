using VanAn.Voice.Api.Services;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Api.Services;

public sealed record PermissionResult(bool Allowed, CallStatus? TerminalStatus, string? Reason, Guid? TenantId, Guid? TargetUserId = null);

/// <summary>
/// StartCall permission rules (SRS v1.1 FR-002, SEC-002), evaluated in order:
/// self-call → caller busy (FR-007) → kill-switch (§14.4) → verified context (O6) →
/// open-calling flag. Tenant resolution: staff token claim, or the verified context's tenant
/// for customers (they carry no tenant claim).
/// </summary>
public sealed class PermissionService(
    VoiceStateStore store,
    TenantFeatureStore features,
    IContextVerifier contextVerifier)
{
    public async Task<PermissionResult> CheckStartCallAsync(
        VoiceIdentity caller, Guid targetUserId, string? contextType, string? contextReferenceId,
        DateTime now, CancellationToken ct = default)
    {
        // Chat-first UX (P4): the caller may omit the target — resolve the OTHER active
        // participants of the verified conversation. Shop threads hold many staff
        // participants but only some have a live voice connection: prefer the first
        // candidate that is reachable and not already in a call (S8 — deterministic
        // single-pick kept ringing an offline staff member while others were online).
        if (targetUserId == Guid.Empty
            && !string.IsNullOrEmpty(contextType) && !string.IsNullOrEmpty(contextReferenceId))
        {
            var candidates = await contextVerifier.ResolveCounterpartCandidatesAsync(contextType!, contextReferenceId!, caller.UserId, ct);
            // R9 (voice_presence Phase C): FR-007 caps 1 call/user nên "load" = phân phối
            // đều — round-robin theo contextReferenceId trong nhóm reachable+idle,
            // tránh luôn dồn cuộc gọi vào staff đầu danh sách.
            var reachable = candidates.Where(c => store.ConnectionsOf(c).Count > 0 && !store.HasActiveCall(c)).ToList();
            targetUserId = reachable.Count switch
            {
                0 => candidates.Count > 0 ? candidates[0] : Guid.Empty,
                1 => reachable[0],
                _ => reachable[(store.NextRotationIndex(contextReferenceId!) - 1) % reachable.Count]
            };
            if (targetUserId == Guid.Empty)
            {
                // No callable counterpart — guests are excluded from candidates (SRS O2),
                // so a guest-only thread resolves empty. Distinguish it for UX: staff sees
                // "khách chưa đăng nhập" instead of a misleading INVALID_CONTEXT.
                var guestOnly = await contextVerifier.HasGuestCounterpartAsync(contextType!, contextReferenceId!, caller.UserId, ct);
                return new PermissionResult(false, CallStatus.Failed,
                    guestOnly ? CallEndReason.GuestPeer : CallEndReason.InvalidContext, caller.TenantId);
            }
        }

        if (targetUserId == Guid.Empty || targetUserId == caller.UserId)
            return new PermissionResult(false, CallStatus.Failed, CallEndReason.SelfCall, caller.TenantId);

        // FR-007: one active call per user — the caller must not already be in a call.
        if (store.HasActiveCall(caller.UserId))
            return new PermissionResult(false, CallStatus.Busy, CallEndReason.CallerBusy, caller.TenantId);

        Guid? tenantId = caller.TenantId;

        if (!string.IsNullOrEmpty(contextType) && !string.IsNullOrEmpty(contextReferenceId))
        {
            // Context supplied → must be verified (record exists + both users are participants).
            var verification = await contextVerifier.VerifyAsync(contextType!, contextReferenceId!, caller.UserId, targetUserId, ct);
            if (verification is not { Verified: true } || verification.TenantId is null)
                return new PermissionResult(false, CallStatus.Failed, CallEndReason.InvalidContext, tenantId);
            tenantId = verification.TenantId;
        }
        else if (tenantId is null)
        {
            // Customer without context has no tenant source → deny.
            return new PermissionResult(false, CallStatus.Failed, CallEndReason.ContextRequired, null);
        }

        // Kill-switch (§14.4): admin can disable the feature per tenant regardless of wallet.
        var config = await features.GetAsync(tenantId.Value, ct);
        if (!config.VoiceFeatureEnabled)
            return new PermissionResult(false, CallStatus.Failed, CallEndReason.FeatureDisabled, tenantId);

        // No context → allowed only when the tenant opted into open calling.
        if (string.IsNullOrEmpty(contextType) && !config.VoiceOpenCalling)
            return new PermissionResult(false, CallStatus.Failed, CallEndReason.ContextRequired, tenantId);

        return new PermissionResult(true, null, null, tenantId, targetUserId);
    }
}