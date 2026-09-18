using Microsoft.AspNetCore.SignalR;
using VanAn.Gateway.Realtime;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Hubs;

/// <summary>
/// Realtime Platform P3 (2026-09-17): subject-agnostic chat hub at <c>/hubs/messaging</c>.
///
/// Replaces the order-hardcoded <see cref="ChatHub"/> for new consumers: the same hub serves an
/// order, a shop profile, a shipment or a support ticket — the subject is a parameter, not a schema.
/// Messages are still sent over HTTP (<c>POST /api/realtime/conversations/messages</c>), which then
/// pushes <c>ReceiveMessage</c> into the group; this hub only carries the subscription.
///
/// Auth: any <see cref="IRealtimeTokenValidator"/> — customer token, guest device id, or staff JWT.
/// Guests were HTTP-only before (F3); reading <c>customerDeviceId</c> from the query string is what
/// lets them open a WebSocket at all.
/// Join: gated by the keyed <c>IRealtimeParticipantAuthorizer</c> for the subject type (default deny).
///
/// <see cref="ChatHub"/> (<c>/hubs/chat</c>) stays mapped for KhachLink builds that predate this hub.
/// </summary>
public class MessagingHub(
    RealtimeIdentityResolver identityResolver,
    IServiceProvider services,
    ILogger<MessagingHub> logger) : Hub
{
    internal const string IdentityItemKey = "RealtimeIdentity";

    private readonly RealtimeIdentityResolver _identityResolver = identityResolver;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<MessagingHub> _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var identity = httpContext == null
            ? null
            : await _identityResolver.ResolveAsync(httpContext, Context.ConnectionAborted);

        if (identity == null)
            throw new HubException("Unauthorized: no valid customerToken, customerDeviceId or access_token.");

        Context.Items[IdentityItemKey] = identity;
        _logger.LogInformation("MessagingHub: {Kind} {UserId} connected", identity.Kind, identity.UserId);

        await base.OnConnectedAsync();
    }

    /// <summary>Join the chat group for a subject. Throws when the caller may not see that subject.</summary>
    public async Task JoinConversation(string subjectType, string subjectId)
    {
        var (type, id) = RealtimeSubjectParser.Parse(subjectType, subjectId);
        var identity = RequireIdentity();

        if (!await RealtimeAuthorizerLookup.CanAccessAsync(_services, type, id, identity.UserId, identity.TenantId, Context.ConnectionAborted))
            throw new HubException($"Access denied: not a participant of {type}/{id}");

        // P6: the shop conversation is a shared thread — staff join the shared group (inbox),
        // customers join their OWN per-user group so live pushes never cross customers.
        var group = type == RealtimeSubjectType.Shop && identity.Kind != RealtimeIdentityKind.Staff
            ? RealtimeGroups.MessagingUser(id, identity.UserId)
            : RealtimeGroups.Messaging(type, id);

        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _logger.LogInformation("MessagingHub: {Kind} {UserId} joined {Subject}/{SubjectId}",
            identity.Kind, identity.UserId, type, id);
    }

    public Task LeaveConversation(string subjectType, string subjectId)
    {
        var identity = RequireIdentity();
        var group = subjectType == RealtimeSubjectType.Shop.ToString()
                    && identity.Kind != RealtimeIdentityKind.Staff
            ? RealtimeGroups.MessagingUser(Guid.TryParse(subjectId, out var id) ? id : Guid.Empty, identity.UserId)
            : RealtimeGroups.Messaging(subjectType, subjectId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
    }

    private RealtimeIdentity RequireIdentity()
        => Context.Items.TryGetValue(IdentityItemKey, out var value) && value is RealtimeIdentity identity
            ? identity
            : throw new HubException("Not authenticated");
}
