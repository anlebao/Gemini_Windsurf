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
        // participant of the verified conversation.
        if (targetUserId == Guid.Empty
            && !string.IsNullOrEmpty(contextType) && !string.IsNullOrEmpty(contextReferenceId))
        {
            targetUserId = await contextVerifier.ResolveCounterpartAsync(contextType!, contextReferenceId!, caller.UserId, ct) ?? Guid.Empty;
            if (targetUserId == Guid.Empty)
                return new PermissionResult(false, CallStatus.Failed, CallEndReason.InvalidContext, caller.TenantId);
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