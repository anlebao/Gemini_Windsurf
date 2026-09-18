using Microsoft.AspNetCore.SignalR;
using VanAn.Gateway.Realtime;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Hubs;

/// <summary>
/// CC-S2 (Sprint 2): SignalR hub for real-time GPS delivery tracking.
/// UC-06 (GPS tracking) — shipper pushes location → customer subscribes to order group.
///
/// Realtime Platform P3 (2026-09-17): kept as the order-scoped adapter for KhachLink builds that
/// predate <see cref="TrackingHub"/> (<c>/hubs/tracking</c>). Group name and method names unchanged;
/// auth + access rules delegate to the shared realtime layer (see <see cref="ChatHub"/>).
/// </summary>
public class LocationHub(
    RealtimeIdentityResolver identityResolver,
    IServiceProvider services,
    ILogger<LocationHub> logger) : Hub
{
    private readonly RealtimeIdentityResolver _identityResolver = identityResolver;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<LocationHub> _logger = logger;

    /// <summary>
    /// Validate identity on connection. Credentials arrive via query string (SignalR handshakes
    /// cannot set custom headers).
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var identity = httpContext == null
            ? null
            : await _identityResolver.ResolveAsync(httpContext, Context.ConnectionAborted);

        if (identity == null)
            throw new HubException("Unauthorized: no valid customerToken or customerDeviceId.");

        Context.Items[MessagingHub.IdentityItemKey] = identity;
        _logger.LogInformation("LocationHub: {Kind} {UserId} connected", identity.Kind, identity.UserId);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Join order tracking group — shipper or customer subscribes to location updates.
    /// Verifies the caller may access the order (assigned shipper, conversation party, order owner
    /// or the guest device that placed it).
    /// </summary>
    public async Task JoinOrderTracking(string orderId)
    {
        if (!Guid.TryParse(orderId, out var orderGuid) || orderGuid == Guid.Empty)
            throw new HubException("Invalid orderId");

        var identity = RequireIdentity();

        if (!await RealtimeAuthorizerLookup.CanAccessAsync(_services, RealtimeSubjectType.Order, orderGuid, identity.UserId, Context.ConnectionAborted))
            throw new HubException("Access denied: not shipper or customer of this order");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
        _logger.LogInformation("LocationHub: {Kind} {UserId} joined order_{OrderId}", identity.Kind, identity.UserId, orderId);
    }

    /// <summary>Leave order tracking group.</summary>
    public Task LeaveOrderTracking(string orderId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order_{orderId}");

    private RealtimeIdentity RequireIdentity()
        => Context.Items.TryGetValue(MessagingHub.IdentityItemKey, out var value) && value is RealtimeIdentity identity
            ? identity
            : throw new HubException("Not authenticated");
}
