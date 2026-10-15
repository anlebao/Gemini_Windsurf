using Microsoft.AspNetCore.SignalR;
using VanAn.Gateway.Realtime;

namespace VanAn.Gateway.Hubs;

/// <summary>
/// Notification Hub (2026-10-15, user directive): SignalR hub tại /hubs/notifications.
/// Chỉ mang subscription (join group) — dữ liệu đọc qua HTTP /api/v1/notifications/*.
///   JoinOwner(tenantId)     — staff JWT, tenant_id phải khớp → group "notify.owner.{tenantId}"
///   JoinCustomer(customerId)— customer token, id phải khớp → group "notify.customer.{customerId}"
/// Gateway NotificationHubNotifier (NATS subscriber) đẩy UnreadCountChanged vào group khi có thông báo mới.
/// </summary>
public class NotificationsHub(
    RealtimeIdentityResolver identityResolver,
    ILogger<NotificationsHub> logger) : Hub
{
    private readonly RealtimeIdentityResolver _identityResolver = identityResolver;
    private readonly ILogger<NotificationsHub> _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var identity = httpContext == null
            ? null
            : await _identityResolver.ResolveAsync(httpContext, Context.ConnectionAborted);

        if (identity == null)
            throw new HubException("Unauthorized: no valid customerToken, customerDeviceId or access_token.");

        Context.Items[MessagingHub.IdentityItemKey] = identity;
        await base.OnConnectedAsync();
    }

    /// <summary>Owner (staff JWT) join group thông báo của tenant mình.</summary>
    public async Task JoinOwner(Guid tenantId)
    {
        var identity = RequireIdentity();
        if (identity.Kind != RealtimeIdentityKind.Staff || !identity.TenantId.HasValue || identity.TenantId.Value != tenantId)
            throw new HubException("Access denied: tenant_id không khớp.");

        await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.Owner(tenantId));
        _logger.LogInformation("NotificationsHub: staff {UserId} joined owner group {TenantId}", identity.UserId, tenantId);
    }

    /// <summary>Khách join group thông báo của chính mình.</summary>
    public async Task JoinCustomer(Guid customerId)
    {
        var identity = RequireIdentity();
        if (identity.Kind != RealtimeIdentityKind.Customer || identity.UserId != customerId)
            throw new HubException("Access denied: customerId không khớp.");

        await Groups.AddToGroupAsync(Context.ConnectionId, NotificationGroups.Customer(customerId));
        _logger.LogInformation("NotificationsHub: customer {CustomerId} joined notification group", customerId);
    }

    public Task LeaveOwner(Guid tenantId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, NotificationGroups.Owner(tenantId));
    public Task LeaveCustomer(Guid customerId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, NotificationGroups.Customer(customerId));

    private RealtimeIdentity RequireIdentity()
        => Context.Items.TryGetValue(MessagingHub.IdentityItemKey, out var value) && value is RealtimeIdentity identity
            ? identity
            : throw new HubException("Not authenticated");
}

/// <summary>Group name helpers cho NotificationsHub.</summary>
public static class NotificationGroups
{
    public static string Owner(Guid tenantId) => $"notify.owner.{tenantId}";
    public static string Customer(Guid customerId) => $"notify.customer.{customerId}";
}
