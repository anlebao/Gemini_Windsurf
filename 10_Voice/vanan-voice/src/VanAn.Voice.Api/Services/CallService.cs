using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanAn.Voice.Contracts;
using VanAn.Voice.Domain;
using VanAn.Voice.Infra;

namespace VanAn.Voice.Api.Services;

public sealed record StartCallResult(Guid CallId, bool IsNew, string Status, string? Reason);

/// <summary>
/// Voice call orchestrator (SRS v1.1 §4–§7, §14): permission → metering gate → state machine
/// → persistence → event publishing. Server state is authoritative; all transitions are
/// idempotent. A live call is NEVER cut by wallet depletion — usage is deducted after the call.
/// </summary>
public sealed class CallService(
    VoiceDbContext db,
    VoiceStateStore store,
    PermissionService permission,
    MeteringClient metering,
    ICallEventPublisher publisher,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CallService> logger)
{
    private const int MaxSdpBytes = 64 * 1024;
    private const int MaxIceBytes = 8 * 1024;
    private const int MaxIcePerCall = 64;

    private int RingTimeoutSeconds => configuration.GetValue("Voice:RingTimeoutSeconds", 30);
    private int NegotiationTimeoutSeconds => configuration.GetValue("Voice:NegotiationTimeoutSeconds", 20);
    private int IceTimeoutSeconds => configuration.GetValue("Voice:IceTimeoutSeconds", 15);
    private int ReconnectWindowSeconds => configuration.GetValue("Voice:ReconnectWindowSeconds", 10);
    private int MaxDurationMinutes => configuration.GetValue("Voice:MaxDurationMinutes", 120);
    private int MaxIceRestarts => configuration.GetValue("Voice:MaxIceRestarts", 2);
    private int EstimatedMinutesPerCall => configuration.GetValue("Voice:EstimatedMinutesPerCall", 3);
    private string SourceApplication => configuration["Voice:SourceApplication"] ?? "unknown";

    private CallTimeoutConfig TimeoutConfig => new(RingTimeoutSeconds, NegotiationTimeoutSeconds, IceTimeoutSeconds, MaxDurationMinutes);

    // ---- StartCall ----

    public async Task<StartCallResult> StartCallAsync(VoiceIdentity caller, StartCallRequest req, string? callerConnectionId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Idempotency: the client callId (UUIDv7) is the key — a duplicate never creates a second call.
        var existing = store.Get(req.CallId)?.Call
            ?? await db.VoiceCalls.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CallId, ct);
        if (existing != null)
            return new StartCallResult(existing.Id, false, existing.Status.ToString().ToUpperInvariant(), existing.EndReason);

        // Permission rules (self-call, caller busy, kill-switch, context, open-calling).
        // For chat-first calls the target may be resolved server-side (P4) — use it everywhere.
        var perm = await permission.CheckStartCallAsync(caller, req.TargetUserId, req.ContextType, req.ContextReferenceId, now, ct);
        if (!perm.Allowed)
        {
            logger.LogInformation("StartCall {CallId} denied: reason={Reason} terminal={Terminal} tenant={Tenant} target={Target}",
                req.CallId, perm.Reason, perm.TerminalStatus, perm.TenantId, perm.TargetUserId ?? req.TargetUserId);
            // FR-013: call collision is rejected WITHOUT inserting a row.
            if (perm.TerminalStatus == CallStatus.CallCollision)
            {
                await publisher.SendToAsync(callerConnectionId, "CallCollision", new CallIdDto(req.CallId));
                return new StartCallResult(req.CallId, true, "CALL_COLLISION", CallEndReason.Collision);
            }

            // Terminal outcomes are recorded when a tenant is known (attempt history).
            // Self-calls / unresolved targets carry no meaningful record — event only.
            var deniedTarget = perm.TargetUserId ?? req.TargetUserId;
            if (perm.TenantId is { } deniedTenant && deniedTarget != Guid.Empty && caller.UserId != deniedTarget)
            {
                var denied = VoiceCall.Create(req.CallId, deniedTenant, caller.UserId, deniedTarget,
                    SourceApplication, req.ContextType, req.ContextReferenceId, now);
                denied.ForceTerminal(perm.TerminalStatus!.Value, perm.Reason!, now);
                denied.StampConnectionIds(callerConnectionId, null);
                db.VoiceCalls.Add(denied);
                db.VoiceCallEvents.Add(new VoiceCallEvent(req.CallId, "call.denied", caller.UserId, now,
                    $"{{\"reason\":\"{perm.Reason}\"}}"));
                await db.SaveChangesAsync(ct);
                await PublishDeniedAsync(callerConnectionId, req.CallId, perm.TerminalStatus!.Value, perm.Reason!);
                return new StartCallResult(req.CallId, true, perm.TerminalStatus!.Value.ToString().ToUpperInvariant(), perm.Reason);
            }

            // No tenant (customer without context) — event only, no row.
            await PublishDeniedAsync(callerConnectionId, req.CallId, CallStatus.Failed, perm.Reason!);
            return new StartCallResult(req.CallId, true, "FAILED", perm.Reason);
        }

        var targetUserId = perm.TargetUserId ?? req.TargetUserId;
        logger.LogInformation("StartCall {CallId} permitted: caller={Caller} target={Target} tenant={Tenant} ctx={Ctx}/{Ref}",
            req.CallId, caller.UserId, targetUserId, perm.TenantId, req.ContextType, req.ContextReferenceId);

        // Metering gate (§14.2): deny BEFORE ringing when the wallet is below the estimate.
        var tenantId = perm.TenantId!.Value;
        if (!await MeteringGateAllowsAsync(tenantId, caller.UserId, now, ct))
        {
            var denied = VoiceCall.Create(req.CallId, tenantId, caller.UserId, targetUserId,
                SourceApplication, req.ContextType, req.ContextReferenceId, now);
            denied.ForceTerminal(CallStatus.Failed, CallEndReason.MeteringDenied, now);
            denied.StampConnectionIds(callerConnectionId, null);
            db.VoiceCalls.Add(denied);
            db.VoiceCallEvents.Add(new VoiceCallEvent(req.CallId, "call.denied", caller.UserId, now,
                $"{{\"reason\":\"{CallEndReason.MeteringDenied}\"}}"));
            await db.SaveChangesAsync(ct);
            await publisher.SendToAsync(callerConnectionId, "CallFailed", new CallIdWithReasonDto(req.CallId, CallEndReason.MeteringDenied));
            return new StartCallResult(req.CallId, true, "FAILED", CallEndReason.MeteringDenied);
        }

        // FR-013 collision: the target is actively calling the caller (target's active call is TO the caller).
        if (store.ActiveCallOf(targetUserId) is { } targetActive && targetActive.Call.ReceiverUserId == caller.UserId)
        {
            await publisher.SendToAsync(callerConnectionId, "CallCollision", new CallIdDto(req.CallId));
            return new StartCallResult(req.CallId, true, "CALL_COLLISION", CallEndReason.Collision);
        }

        // FR-007 receiver busy: the target already has an active call.
        if (store.HasActiveCall(targetUserId))
        {
            var busy = VoiceCall.Create(req.CallId, tenantId, caller.UserId, targetUserId,
                SourceApplication, req.ContextType, req.ContextReferenceId, now);
            busy.ForceTerminal(CallStatus.Busy, CallEndReason.ReceiverBusy, now);
            busy.StampConnectionIds(callerConnectionId, null);
            db.VoiceCalls.Add(busy);
            db.VoiceCallEvents.Add(new VoiceCallEvent(req.CallId, "call.busy", caller.UserId, now));
            await db.SaveChangesAsync(ct);
            await publisher.SendToAsync(callerConnectionId, "CallBusy", new CallIdDto(req.CallId));
            return new StartCallResult(req.CallId, true, "BUSY", CallEndReason.ReceiverBusy);
        }

        // Create + persist + activate (FR-007 enforced in DB by the partial unique indexes).
        var call = VoiceCall.Create(req.CallId, tenantId, caller.UserId, targetUserId,
            SourceApplication, req.ContextType, req.ContextReferenceId, now);
        call.StampConnectionIds(callerConnectionId, null);
        db.VoiceCalls.Add(call);
        db.VoiceCallEvents.Add(new VoiceCallEvent(req.CallId, "call.created", caller.UserId, now));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent StartCall won the race — this caller already has an active call.
            logger.LogWarning(ex, "StartCall {CallId} rejected: unique active-call constraint (race).", req.CallId);
            await publisher.SendToAsync(callerConnectionId, "CallBusy", new CallIdDto(req.CallId));
            return new StartCallResult(req.CallId, true, "BUSY", CallEndReason.CallerBusy);
        }

        var active = new VoiceStateStore.ActiveCall
        {
            Call = call,
            CallerConnectionId = callerConnectionId,
            RingStartedAt = now
        };
        if (!store.TryAddActive(active, out _))
        {
            // In-memory race backstop (single instance) — terminate the row.
            call.ForceTerminal(CallStatus.Busy, CallEndReason.CallerBusy, now);
            await db.SaveChangesAsync(ct);
            await publisher.SendToAsync(callerConnectionId, "CallBusy", new CallIdDto(req.CallId));
            return new StartCallResult(req.CallId, true, "BUSY", CallEndReason.CallerBusy);
        }
        if (callerConnectionId != null) store.MapConnectionToCall(callerConnectionId, req.CallId);

        // Presence: zero connections ⇒ immediate OFFLINE (no wasted ringing, FR-014).
        var receiverConnections = store.ConnectionsOf(targetUserId);
        logger.LogInformation("StartCall {CallId}: receiver {Target} has {Count} voice connection(s)",
            req.CallId, targetUserId, receiverConnections.Count);
        if (receiverConnections.Count == 0)
        {
            call.ForceTerminal(CallStatus.Offline, CallEndReason.Offline, now);
            await FinalizeAsync(call, now);
            await publisher.SendToAsync(callerConnectionId, "CallOffline", new CallOfflineDto(req.CallId, true));
            return new StartCallResult(req.CallId, true, "OFFLINE", CallEndReason.Offline);
        }

        call.Transition(CallStatus.Ringing, null, now);
        await db.SaveChangesAsync(ct);
        await publisher.SendToAsync(callerConnectionId, "CallCreated",
            new CallCreatedDto(req.CallId, call.Status.ToString().ToUpperInvariant(), TimeoutConfig));
        await publisher.SendToConnectionsAsync(receiverConnections, "IncomingCall",
            new IncomingCallDto(req.CallId, caller.UserId, CallerDisplayName(caller), VerifiedContextLabel(call), TimeoutConfig));
        logger.LogInformation("StartCall {CallId} RINGING: caller={Caller} target={Target} receiverConns={Count}",
            req.CallId, caller.UserId, targetUserId, receiverConnections.Count);
        Schedule(active, TimeSpan.FromSeconds(RingTimeoutSeconds), svc => svc.RingTimeoutAsync(req.CallId));
        return new StartCallResult(req.CallId, true, "RINGING", null);
    }

    // ---- Accept / Reject / Cancel ----

    public async Task<bool> AcceptCallAsync(VoiceIdentity user, Guid callId, string connectionId, CancellationToken ct = default)
    {
        var active = store.Get(callId);
        if (active == null) return false;
        if (active.Call.ReceiverUserId != user.UserId) return false;

        if (active.Call.Status == CallStatus.Accepted)
        {
            // First-accept-wins: other devices of the same receiver clean up locally.
            await publisher.SendToAsync(connectionId, "CallAccepted", new CallIdDto(callId));
            return true;
        }
        if (active.Call.Status != CallStatus.Ringing) return false;

        var now = DateTime.UtcNow;
        active.Call.Transition(CallStatus.Accepted, null, now);
        active.ReceiverConnectionId = connectionId;
        active.Call.StampConnectionIds(active.CallerConnectionId, connectionId);
        store.MapConnectionToCall(connectionId, callId);
        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "call.accepted", user.UserId, now));
        await db.SaveChangesAsync(ct);
        CancelTimer(active);

        await publisher.SendToAsync(active.CallerConnectionId, "CallAccepted", new CallIdDto(callId));
        foreach (var conn in store.ConnectionsOf(user.UserId))
            await publisher.SendToAsync(conn, "CallAccepted", new CallIdDto(callId));

        Schedule(active, TimeSpan.FromSeconds(NegotiationTimeoutSeconds), svc => svc.NegotiationTimeoutAsync(callId));
        return true;
    }

    public async Task<bool> RejectCallAsync(VoiceIdentity user, Guid callId, CancellationToken ct = default)
    {
        var active = store.Get(callId);
        if (active == null || active.Call.ReceiverUserId != user.UserId || active.Call.Status != CallStatus.Ringing)
            return false;

        var now = DateTime.UtcNow;
        active.Call.Transition(CallStatus.Rejected, CallEndReason.Rejected, now);
        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "call.rejected", user.UserId, now));
        await publisher.SendToAsync(active.CallerConnectionId, "CallRejected", new CallIdDto(callId));
        foreach (var conn in store.ConnectionsOf(user.UserId))
            await publisher.SendToAsync(conn, "CallRejected", new CallIdDto(callId));
        await FinalizeAsync(active.Call, now);
        return true;
    }

    public async Task<bool> CancelCallAsync(VoiceIdentity user, Guid callId, CancellationToken ct = default)
    {
        var active = store.Get(callId);
        if (active == null || active.Call.CallerUserId != user.UserId || active.Call.Status != CallStatus.Ringing)
            return false;

        var now = DateTime.UtcNow;
        active.Call.Transition(CallStatus.Cancelled, CallEndReason.Cancelled, now);
        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "call.cancelled", user.UserId, now));
        await publisher.SendToAsync(active.CallerConnectionId, "CallCancelled", new CallIdDto(callId));
        foreach (var conn in store.ConnectionsOf(user.UserId))
            await publisher.SendToAsync(conn, "CallCancelled", new CallIdDto(callId));
        await FinalizeAsync(active.Call, now);
        return true;
    }

    // ---- Signaling relay (SDP / ICE) ----

    public async Task<bool> SendOfferAsync(VoiceIdentity user, SdpDto sdp, CancellationToken ct = default)
    {
        if (sdp.Sdp.Length > MaxSdpBytes) return false;
        var active = store.Get(sdp.CallId);
        if (active == null || active.Call.CallerUserId != user.UserId) return false;

        if (active.Call.Status == CallStatus.Accepted)
        {
            var now = DateTime.UtcNow;
            if (!active.Call.Transition(CallStatus.Negotiating, null, now)) return false;
            db.VoiceCallEvents.Add(new VoiceCallEvent(sdp.CallId, "call.negotiating", user.UserId, now));
            await db.SaveChangesAsync(ct);
        }
        else if (active.Call.Status is not (CallStatus.Negotiating or CallStatus.Reconnecting))
        {
            return false;
        }

        if (active.ReceiverConnectionId == null) return false;
        await publisher.SendToAsync(active.ReceiverConnectionId, "ReceiveOffer", new ReceiveSdpDto(sdp.CallId, sdp.Sdp));
        return true;
    }

    public async Task<bool> SendAnswerAsync(VoiceIdentity user, SdpDto sdp, CancellationToken ct = default)
    {
        if (sdp.Sdp.Length > MaxSdpBytes) return false;
        var active = store.Get(sdp.CallId);
        if (active == null || active.Call.ReceiverUserId != user.UserId) return false;
        if (active.Call.Status is not (CallStatus.Negotiating or CallStatus.Reconnecting)) return false;
        if (active.CallerConnectionId == null) return false;

        await publisher.SendToAsync(active.CallerConnectionId, "ReceiveAnswer", new ReceiveSdpDto(sdp.CallId, sdp.Sdp));
        return true;
    }

    public async Task<bool> SendIceCandidateAsync(VoiceIdentity user, IceCandidateDto candidate, CancellationToken ct = default)
    {
        if (candidate.Candidate.Length > MaxIceBytes) return false;
        var active = store.Get(candidate.CallId);
        if (active == null) return false;
        if (active.Call.CallerUserId != user.UserId && active.Call.ReceiverUserId != user.UserId) return false;
        if (active.Call.Status is not (CallStatus.Accepted or CallStatus.Negotiating or CallStatus.Connected or CallStatus.Reconnecting)) return false;
        if (++active.IceCandidatesCount > MaxIcePerCall) return false;

        var target = active.Call.CallerUserId == user.UserId ? active.ReceiverConnectionId : active.CallerConnectionId;
        if (target == null) return false;
        await publisher.SendToAsync(target, "ReceiveIceCandidate", new ReceiveIceCandidateDto(candidate.CallId, candidate.Candidate));
        return true;
    }

    // ---- Media state (P2 SDK calls these) ----

    public async Task<bool> NotifyConnectedAsync(VoiceIdentity user, Guid callId, CancellationToken ct = default)
    {
        var active = store.Get(callId);
        if (active == null || !IsParticipant(active, user.UserId)) return false;
        if (active.Call.Status != CallStatus.Negotiating) return false;

        var now = DateTime.UtcNow;
        active.Call.Transition(CallStatus.Connected, null, now);
        active.ConnectedAt = now;
        active.Call.SetConnectionType("P2P");
        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "call.connected", user.UserId, now));
        await db.SaveChangesAsync(ct);
        CancelTimer(active);
        Schedule(active, TimeSpan.FromMinutes(MaxDurationMinutes), svc => svc.MaxDurationAsync(callId));

        await publisher.SendToAsync(active.CallerConnectionId, "CallConnected", new CallIdDto(callId));
        await publisher.SendToAsync(active.ReceiverConnectionId, "CallConnected", new CallIdDto(callId));
        return true;
    }

    public async Task<bool> NotifyReconnectingAsync(VoiceIdentity user, Guid callId, CancellationToken ct = default)
    {
        var active = store.Get(callId);
        if (active == null || !IsParticipant(active, user.UserId) || active.Call.Status != CallStatus.Connected)
            return false;

        if (++active.IceRestartCount > MaxIceRestarts)
            return await FailCallAsync(callId, CallEndReason.ReconnectFailed, user.UserId, ct);

        var now = DateTime.UtcNow;
        active.Call.Transition(CallStatus.Reconnecting, null, now);
        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "call.reconnecting", user.UserId, now,
            $"{{\"attempt\":{active.IceRestartCount}}}"));
        await db.SaveChangesAsync(ct);
        await publisher.SendToAsync(OtherSideConnection(active, user.UserId), "CallReconnecting", new CallIdDto(callId));
        Schedule(active, TimeSpan.FromSeconds(ReconnectWindowSeconds), svc => svc.ReconnectTimeoutAsync(callId));
        return true;
    }

    public async Task<bool> NotifyReconnectedAsync(VoiceIdentity user, Guid callId, string connectionId, CancellationToken ct = default)
    {
        var active = store.Get(callId);
        if (active == null || !IsParticipant(active, user.UserId) || active.Call.Status != CallStatus.Reconnecting)
            return false;

        var now = DateTime.UtcNow;
        active.Call.Transition(CallStatus.Connected, null, now);
        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "call.reconnected", user.UserId, now));
        await db.SaveChangesAsync(ct);
        CancelTimer(active);

        if (active.Call.CallerUserId == user.UserId)
        {
            active.CallerConnectionId = connectionId;
            store.MapConnectionToCall(connectionId, callId);
        }
        else
        {
            active.ReceiverConnectionId = connectionId;
            store.MapConnectionToCall(connectionId, callId);
        }

        await publisher.SendToAsync(active.CallerConnectionId, "CallReconnected", new CallIdDto(callId));
        await publisher.SendToAsync(active.ReceiverConnectionId, "CallReconnected", new CallIdDto(callId));
        return true;
    }

    // ---- End / missed-call ----

    public async Task<bool> EndCallAsync(VoiceIdentity user, Guid callId, string? reason = null, CancellationToken ct = default)
    {
        var active = store.Get(callId);
        if (active == null || !IsParticipant(active, user.UserId) || active.Call.IsTerminal()) return false;

        var now = DateTime.UtcNow;
        active.Call.Transition(CallStatus.Ended, CallEndReason.UserEnded, now);
        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "call.ended", user.UserId, now,
            reason == null ? null : $"{{\"reason\":\"{reason}\"}}"));
        await publisher.SendToAsync(active.CallerConnectionId, "CallEnded", new CallIdWithReasonDto(callId, CallEndReason.UserEnded));
        await publisher.SendToAsync(active.ReceiverConnectionId, "CallEnded", new CallIdWithReasonDto(callId, CallEndReason.UserEnded));
        await FinalizeAsync(active.Call, now);
        return true;
    }

    public async Task<bool> SendMissedCallNotificationAsync(VoiceIdentity user, Guid callId, CancellationToken ct = default)
    {
        var call = store.Get(callId)?.Call
            ?? await db.VoiceCalls.AsNoTracking().FirstOrDefaultAsync(c => c.Id == callId, ct);
        if (call == null || call.CallerUserId != user.UserId) return false;
        if (call.Status is not (CallStatus.Offline or CallStatus.Missed)) return false;

        // Once per call (the event itself is the dedupe — EventType+CallId).
        var already = await db.VoiceCallEvents.AnyAsync(
            e => e.CallId == callId && e.EventType == "missed-call", ct);
        if (already) return false;

        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "missed-call", call.ReceiverUserId, DateTime.UtcNow,
            $"{{\"callerUserId\":\"{call.CallerUserId}\",\"contextType\":\"{call.ContextType}\"}}"));
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Connection loss ----

    public async Task ConnectionLostAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        var callId = store.CallIdOfConnection(connectionId);
        if (callId == null) return;
        var active = store.Get(callId.Value);
        if (active == null) return;

        var now = DateTime.UtcNow;
        if (active.Call.CallerUserId == userId) active.CallerConnectionId = null;
        if (active.Call.ReceiverUserId == userId) active.ReceiverConnectionId = null;

        switch (active.Call.Status)
        {
            case CallStatus.Ringing when active.Call.CallerUserId == userId:
                // Caller vanished while ringing → cancel.
                active.Call.Transition(CallStatus.Cancelled, CallEndReason.NetworkLost, now);
                db.VoiceCallEvents.Add(new VoiceCallEvent(active.Call.Id, "call.cancelled", userId, now,
                    $"{{\"reason\":\"{CallEndReason.NetworkLost}\"}}"));
                await publisher.SendToAsync(active.ReceiverConnectionId, "CallCancelled", new CallIdDto(active.Call.Id));
                await FinalizeAsync(active.Call, now);
                break;

            case CallStatus.Accepted or CallStatus.Negotiating:
                await FailCallAsync(active.Call.Id, CallEndReason.NetworkLost, userId, ct);
                break;

            case CallStatus.Connected:
                if (active.CallerConnectionId == null && active.ReceiverConnectionId == null)
                {
                    await FailCallAsync(active.Call.Id, CallEndReason.NetworkLost, userId, ct);
                }
                else
                {
                    active.Call.Transition(CallStatus.Reconnecting, null, now);
                    db.VoiceCallEvents.Add(new VoiceCallEvent(active.Call.Id, "call.reconnecting", userId, now,
                        $"{{\"reason\":\"{CallEndReason.NetworkLost}\"}}"));
                    await db.SaveChangesAsync(ct);
                    Schedule(active, TimeSpan.FromSeconds(ReconnectWindowSeconds), svc => svc.ReconnectTimeoutAsync(active.Call.Id));
                }
                break;
        }
    }

    // ---- Timeouts ----

    internal async Task RingTimeoutAsync(Guid callId, CancellationToken ct = default)
    {
        var active = store.Get(callId);
        if (active == null || active.Call.Status != CallStatus.Ringing) return;

        var now = DateTime.UtcNow;
        active.Call.Transition(CallStatus.Missed, CallEndReason.RingTimeout, now);
        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "call.missed", actorUserId: null, now));
        await publisher.SendToAsync(active.CallerConnectionId, "CallEnded", new CallIdWithReasonDto(callId, CallEndReason.RingTimeout));
        await FinalizeAsync(active.Call, now);
    }

    internal async Task NegotiationTimeoutAsync(Guid callId, CancellationToken ct = default)
        => await FailCallAsync(callId, CallEndReason.NegotiationTimeout, null, ct);

    internal async Task ReconnectTimeoutAsync(Guid callId, CancellationToken ct = default)
        => await FailCallAsync(callId, CallEndReason.ReconnectFailed, null, ct);

    internal async Task MaxDurationAsync(Guid callId, CancellationToken ct = default)
    {
        var active = store.Get(callId);
        if (active == null || active.Call.IsTerminal()) return;

        var now = DateTime.UtcNow;
        active.Call.Transition(CallStatus.Ended, CallEndReason.MaxDuration, now);
        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "call.ended", actorUserId: null, now,
            $"{{\"reason\":\"{CallEndReason.MaxDuration}\"}}"));
        await publisher.SendToAsync(active.CallerConnectionId, "CallEnded", new CallIdWithReasonDto(callId, CallEndReason.MaxDuration));
        await publisher.SendToAsync(active.ReceiverConnectionId, "CallEnded", new CallIdWithReasonDto(callId, CallEndReason.MaxDuration));
        await FinalizeAsync(active.Call, now);
    }

    // ---- Helpers ----

    private async Task<bool> FailCallAsync(Guid callId, string reason, Guid? actorUserId, CancellationToken ct)
    {
        var active = store.Get(callId);
        if (active == null || active.Call.IsTerminal()) return false;

        var now = DateTime.UtcNow;
        active.Call.ForceTerminal(CallStatus.Failed, reason, now);
        db.VoiceCallEvents.Add(new VoiceCallEvent(callId, "call.failed", actorUserId, now, $"{{\"reason\":\"{reason}\"}}"));
        await publisher.SendToAsync(active.CallerConnectionId, "CallFailed", new CallIdWithReasonDto(callId, reason));
        await publisher.SendToAsync(active.ReceiverConnectionId, "CallFailed", new CallIdWithReasonDto(callId, reason));
        await FinalizeAsync(active.Call, now);
        return true;
    }

    /// <summary>Persists the terminal state, ingests usage (idempotent by callId), releases state.</summary>
    private async Task FinalizeAsync(VoiceCall call, DateTime now)
    {
        if (!call.IsTerminal()) call.ForceTerminal(CallStatus.Failed, CallEndReason.NetworkLost, now);
        if (call.EndReason == CallEndReason.Offline)
            db.VoiceCallEvents.Add(new VoiceCallEvent(call.Id, "call.offline", call.CallerUserId, now));
        // Hub invocations resolve CallService in different DI scopes → the VoiceCall
        // entity tracked at StartCall is DETACHED here; without re-attach, SaveChanges
        // persists only the new event row and voice_calls.Status/EndedAt stay stale
        // (found by RV call-sim 2026-11-12 — row stuck Ringing after call.ended).
        if (db.Entry(call).State == EntityState.Detached)
            db.VoiceCalls.Update(call);
        await db.SaveChangesAsync();
        IngestUsage(call);
        store.Remove(call.Id);
    }

    private void IngestUsage(VoiceCall call)
    {
        if (call.EndedAt == null || call.TenantId == Guid.Empty) return;

        var events = new List<MeteringUsageEvent>
        {
            new(call.Id.ToString(), call.TenantId, "voice.call", 1, "call", DateTime.UtcNow,
                RequestedBy: call.CallerUserId.ToString(),
                MetadataJson: $"{{\"callId\":\"{call.Id}\",\"contextType\":\"{call.ContextType}\",\"connectionType\":\"{call.ConnectionType}\"}}")
        };
        if (call.DurationSeconds > 0)
        {
            events.Add(new MeteringUsageEvent(
                call.Id + ":min", call.TenantId, "voice.minute", Math.Max(1, (call.DurationSeconds + 59) / 60), "minute", DateTime.UtcNow,
                RequestedBy: call.CallerUserId.ToString()));
        }

        // Fire-and-forget, fail-open: metering death or replay must never break the call flow.
        _ = metering.RecordUsageBatchAsync(events);
    }

    private async Task<bool> MeteringGateAllowsAsync(Guid tenantId, Guid callerUserId, DateTime now, CancellationToken ct)
    {
        // Fail-open (G9): a null result (metering down / not configured) means "allow".
        if (await metering.CheckAsync(tenantId, "voice.call", 1, ct) is { Deny: true }) return false;
        if (await metering.CheckAsync(tenantId, "voice.minute", EstimatedMinutesPerCall, ct) is { Deny: true }) return false;
        return true;
    }

    private async Task PublishDeniedAsync(string? callerConnectionId, Guid callId, CallStatus status, string reason)
    {
        switch (status)
        {
            case CallStatus.Busy:
                await publisher.SendToAsync(callerConnectionId, "CallBusy", new CallIdDto(callId));
                break;
            default:
                await publisher.SendToAsync(callerConnectionId, "CallFailed", new CallIdWithReasonDto(callId, reason));
                break;
        }
    }

    private void Schedule(VoiceStateStore.ActiveCall active, TimeSpan delay, Func<CallService, Task> action)
    {
        CancelTimer(active);
        var cts = new CancellationTokenSource();
        active.Timer = cts;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(delay, cts.Token); }
            catch (TaskCanceledException) { return; }
            if (cts.IsCancellationRequested) return;
            try
            {
                using var scope = scopeFactory.CreateScope();
                await action(scope.ServiceProvider.GetRequiredService<CallService>());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Scheduled call action failed for {CallId}.", active.Call.Id);
            }
        });
    }

    private static void CancelTimer(VoiceStateStore.ActiveCall active) => active.Timer?.Cancel();

    private static bool IsParticipant(VoiceStateStore.ActiveCall active, Guid userId)
        => active.Call.CallerUserId == userId || active.Call.ReceiverUserId == userId;

    private static string? OtherSideConnection(VoiceStateStore.ActiveCall active, Guid userId)
        => active.Call.CallerUserId == userId ? active.ReceiverConnectionId : active.CallerConnectionId;

    private static string CallerDisplayName(VoiceIdentity caller) => caller.UserId.ToString();

    private static string? VerifiedContextLabel(VoiceCall call)
        => call.ContextType == null ? null : $"{call.ContextType}:{call.ContextReferenceId}";

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is SqliteException sqlite && sqlite.SqliteErrorCode == 19; // SQLITE_CONSTRAINT
}