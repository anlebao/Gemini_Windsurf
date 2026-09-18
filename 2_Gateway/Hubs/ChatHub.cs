using Microsoft.AspNetCore.SignalR;
using VanAn.Gateway.Realtime;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Hubs;

/// <summary>
/// CC-S3 (Sprint 3): SignalR hub for real-time shipper ↔ customer chat.
/// UC-07 (Chat) — send message → push ReceiveMessage to chat_{orderId} group.
///
/// Realtime Platform P3 (2026-09-17): kept as the order-scoped adapter for KhachLink builds that
/// predate <see cref="MessagingHub"/> (<c>/hubs/messaging</c>). The group name and the client-facing
/// method names are unchanged; the token validation and the access rules now come from the shared
/// realtime layer (<see cref="RealtimeIdentityResolver"/> + the keyed
/// <c>IRealtimeParticipantAuthorizer</c>) instead of a private copy, so this hub and the generic one
/// can no longer disagree about who may join. Reading the device id from the query string also lets
/// guest connections authenticate here (F3).
/// </summary>
public class ChatHub(
    RealtimeIdentityResolver identityResolver,
    IServiceProvider services,
    ILogger<ChatHub> logger) : Hub
{
    private readonly RealtimeIdentityResolver _identityResolver = identityResolver;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<ChatHub> _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var identity = httpContext == null
            ? null
            : await _identityResolver.ResolveAsync(httpContext, Context.ConnectionAborted);

        if (identity == null)
            throw new HubException("Unauthorized: no valid customerToken or customerDeviceId.");

        Context.Items[MessagingHub.IdentityItemKey] = identity;
        _logger.LogInformation("ChatHub: {Kind} {UserId} connected", identity.Kind, identity.UserId);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Join conversation group — shipper or customer subscribes to chat messages.
    /// Verifies the caller may access the order (assigned shipper, conversation party, order owner
    /// or the guest device that placed it).
    /// </summary>
    public async Task JoinConversation(string orderId)
    {
        if (!Guid.TryParse(orderId, out var orderGuid) || orderGuid == Guid.Empty)
            throw new HubException("Invalid orderId");

        var identity = RequireIdentity();

        if (!await RealtimeAuthorizerLookup.CanAccessAsync(_services, RealtimeSubjectType.Order, orderGuid, identity.UserId, Context.ConnectionAborted))
            throw new HubException("Access denied: not shipper or customer of this order");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{orderId}");
        _logger.LogInformation("ChatHub: {Kind} {UserId} joined chat_{OrderId}", identity.Kind, identity.UserId, orderId);
    }

    public Task LeaveConversation(string orderId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{orderId}");

    private RealtimeIdentity RequireIdentity()
        => Context.Items.TryGetValue(MessagingHub.IdentityItemKey, out var value) && value is RealtimeIdentity identity
            ? identity
            : throw new HubException("Not authenticated");
}
