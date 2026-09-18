using Microsoft.AspNetCore.SignalR;
using VanAn.Gateway.Realtime;

namespace VanAn.Gateway.Hubs;

/// <summary>
/// Realtime Platform P3 (2026-09-17): subject-agnostic live-location hub at <c>/hubs/tracking</c>.
///
/// Generic counterpart to <see cref="LocationHub"/> (which is hardcoded to <c>order_{orderId}</c>).
/// Pings are recorded over HTTP (<c>POST /api/realtime/location/ping</c>), which pushes
/// <c>LocationUpdate</c> into the group; this hub only carries the subscription.
///
/// Auth + join rules are identical to <see cref="MessagingHub"/> — same validators, same keyed
/// authorizer, default deny. <see cref="LocationHub"/> (<c>/hubs/location</c>) stays mapped.
/// </summary>
public class TrackingHub(
    RealtimeIdentityResolver identityResolver,
    IServiceProvider services,
    ILogger<TrackingHub> logger) : Hub
{
    private readonly RealtimeIdentityResolver _identityResolver = identityResolver;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<TrackingHub> _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var identity = httpContext == null
            ? null
            : await _identityResolver.ResolveAsync(httpContext, Context.ConnectionAborted);

        if (identity == null)
            throw new HubException("Unauthorized: no valid customerToken, customerDeviceId or access_token.");

        Context.Items[MessagingHub.IdentityItemKey] = identity;
        _logger.LogInformation("TrackingHub: {Kind} {UserId} connected", identity.Kind, identity.UserId);

        await base.OnConnectedAsync();
    }

    /// <summary>Join the location group for a subject. Throws when the caller may not track it.</summary>
    public async Task JoinTracking(string subjectType, string subjectId)
    {
        var (type, id) = RealtimeSubjectParser.Parse(subjectType, subjectId);
        var identity = RequireIdentity();

        if (!await RealtimeAuthorizerLookup.CanAccessAsync(_services, type, id, identity.UserId, identity.TenantId, Context.ConnectionAborted))
            throw new HubException($"Access denied: not a participant of {type}/{id}");

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Tracking(type, id));
        _logger.LogInformation("TrackingHub: {Kind} {UserId} joined {Subject}/{SubjectId}",
            identity.Kind, identity.UserId, type, id);
    }

    public Task LeaveTracking(string subjectType, string subjectId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Tracking(subjectType, subjectId));

    private RealtimeIdentity RequireIdentity()
        => Context.Items.TryGetValue(MessagingHub.IdentityItemKey, out var value) && value is RealtimeIdentity identity
            ? identity
            : throw new HubException("Not authenticated");
}
