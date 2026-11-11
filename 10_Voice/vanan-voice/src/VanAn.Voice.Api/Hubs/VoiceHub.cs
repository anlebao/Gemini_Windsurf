using Microsoft.AspNetCore.SignalR;
using VanAn.Voice.Api.Auth;
using VanAn.Voice.Api.Services;
using VanAn.Voice.Contracts;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Api.Hubs;

/// <summary>
/// Voice signaling hub at <c>/hubs/voice</c> (SRS v1.1 §6). Every connection is authenticated
/// on connect via the validator chain (staff JWT / customer token); every callId is validated
/// against the authenticated participants. Payload size limits + per-user rate limits
/// (SEC-005). The hub is thin — all logic lives in <see cref="CallService"/>.
/// </summary>
public class VoiceHub(
    VoiceIdentityResolver identityResolver,
    VoiceStateStore store,
    CallService callService,
    RateLimiter rateLimiter,
    ILogger<VoiceHub> logger) : Hub
{
    private const string IdentityKey = "VoiceIdentity";
    private const int MaxSdpBytes = 64 * 1024;
    private const int MaxIceBytes = 8 * 1024;

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var identity = httpContext == null ? null : await identityResolver.ResolveAsync(httpContext, Context.ConnectionAborted);
        if (identity == null)
            throw new HubException("Unauthorized: no valid access_token or customerToken.");

        Context.Items[IdentityKey] = identity;
        store.AddConnection(identity.UserId, Context.ConnectionId);
        logger.LogInformation("VoiceHub: {Kind} {UserId} connected ({ConnectionId}).", identity.Kind, identity.UserId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(IdentityKey, out var value) && value is VoiceIdentity identity)
        {
            store.RemoveConnection(identity.UserId, Context.ConnectionId);
            await callService.ConnectionLostAsync(identity.UserId, Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<StartCallResult> StartCall(StartCallRequest req)
    {
        var identity = RequireIdentity();
        if (req == null || req.CallId == Guid.Empty || req.TargetUserId == Guid.Empty)
            throw new HubException("Invalid StartCall payload.");

        if (!rateLimiter.TryAcquire($"startcall:{identity.UserId}", max: 10, window: TimeSpan.FromMinutes(1))
            || !rateLimiter.TryAcquire($"startcall_hour:{identity.UserId}", max: 60, window: TimeSpan.FromHours(1)))
            throw new HubException("Rate limit exceeded for call initiation.");

        return await callService.StartCallAsync(identity, req, Context.ConnectionId);
    }

    public async Task AcceptCall(Guid callId)
    {
        var identity = RequireIdentity();
        await callService.AcceptCallAsync(identity, callId, Context.ConnectionId);
    }

    public async Task RejectCall(Guid callId)
    {
        var identity = RequireIdentity();
        await callService.RejectCallAsync(identity, callId);
    }

    public async Task CancelCall(Guid callId)
    {
        var identity = RequireIdentity();
        await callService.CancelCallAsync(identity, callId);
    }

    public async Task SendOffer(SdpDto sdp)
    {
        var identity = RequireIdentity();
        if (sdp == null || sdp.Sdp.Length > MaxSdpBytes) throw new HubException("Invalid SDP payload.");
        await callService.SendOfferAsync(identity, sdp);
    }

    public async Task SendAnswer(SdpDto sdp)
    {
        var identity = RequireIdentity();
        if (sdp == null || sdp.Sdp.Length > MaxSdpBytes) throw new HubException("Invalid SDP payload.");
        await callService.SendAnswerAsync(identity, sdp);
    }

    public async Task SendIceCandidate(IceCandidateDto candidate)
    {
        var identity = RequireIdentity();
        if (candidate == null || candidate.Candidate.Length > MaxIceBytes) throw new HubException("Invalid ICE payload.");
        if (!rateLimiter.TryAcquire($"ice:{identity.UserId}", max: 30, window: TimeSpan.FromSeconds(1)))
            throw new HubException("Rate limit exceeded for ICE candidates.");
        await callService.SendIceCandidateAsync(identity, candidate);
    }

    public async Task EndCall(Guid callId)
    {
        var identity = RequireIdentity();
        await callService.EndCallAsync(identity, callId);
    }

    public async Task NotifyConnected(Guid callId)
    {
        var identity = RequireIdentity();
        await callService.NotifyConnectedAsync(identity, callId);
    }

    public async Task NotifyReconnecting(Guid callId)
    {
        var identity = RequireIdentity();
        await callService.NotifyReconnectingAsync(identity, callId);
    }

    public async Task NotifyReconnected(Guid callId)
    {
        var identity = RequireIdentity();
        await callService.NotifyReconnectedAsync(identity, callId, Context.ConnectionId);
    }

    public async Task SendMissedCallNotification(Guid callId)
    {
        var identity = RequireIdentity();
        if (!rateLimiter.TryAcquire($"missed:{identity.UserId}", max: 5, window: TimeSpan.FromHours(1)))
            throw new HubException("Rate limit exceeded for missed-call notifications.");
        await callService.SendMissedCallNotificationAsync(identity, callId);
    }

    private VoiceIdentity RequireIdentity()
        => Context.Items.TryGetValue(IdentityKey, out var value) && value is VoiceIdentity identity
            ? identity
            : throw new HubException("Not authenticated");
}