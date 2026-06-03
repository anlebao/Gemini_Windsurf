using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using VanAn.Shared.Domain;
using System.Collections.Concurrent;

namespace VanAn.KhachLink.Hubs;

/// <summary>
/// Real-time Dashboard Hub - Phase 2.5.2
/// Handles real-time updates for orders, inventory, and dashboard metrics
/// </summary>
[Authorize]
public class DashboardHub : Hub
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> _tenantConnections = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> _shopConnections = new();
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Join tenant-specific dashboard group
    /// </summary>
    public async Task JoinTenantGroup(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("JoinTenantGroup called with null/empty tenantId");
            return;
        }

        var connectionId = Context.ConnectionId;
        var groupName = $"tenant_{tenantId}";

        await Groups.AddToGroupAsync(connectionId, groupName);
        
        _tenantConnections.AddOrUpdate(tenantId, 
            new HashSet<string> { connectionId }, 
            (key, existing) => { existing.Add(connectionId); return existing; });

        _logger.LogInformation("Connection {ConnectionId} joined tenant group {TenantId}", 
            connectionId, tenantId);

        // Send current dashboard state to the new connection
        await SendCurrentDashboardState(tenantId);
    }

    /// <summary>
    /// Join shop-specific dashboard group
    /// </summary>
    public async Task JoinShopGroup(string shopId)
    {
        if (string.IsNullOrEmpty(shopId))
        {
            _logger.LogWarning("JoinShopGroup called with null/empty shopId");
            return;
        }

        var connectionId = Context.ConnectionId;
        var groupName = $"shop_{shopId}";

        await Groups.AddToGroupAsync(connectionId, groupName);
        
        _shopConnections.AddOrUpdate(shopId, 
            new HashSet<string> { connectionId }, 
            (key, existing) => { existing.Add(connectionId); return existing; });

        _logger.LogInformation("Connection {ConnectionId} joined shop group {ShopId}", 
            connectionId, shopId);

        // Send current shop dashboard state
        await SendCurrentShopDashboardState(shopId);
    }

    /// <summary>
    /// Leave tenant group
    /// </summary>
    public async Task LeaveTenantGroup(string tenantId)
    {
        var connectionId = Context.ConnectionId;
        var groupName = $"tenant_{tenantId}";

        await Groups.RemoveFromGroupAsync(connectionId, groupName);
        
        if (_tenantConnections.TryGetValue(tenantId, out var connections))
        {
            connections.Remove(connectionId);
            if (connections.Count == 0)
            {
                _tenantConnections.TryRemove(tenantId, out _);
            }
        }

        _logger.LogInformation("Connection {ConnectionId} left tenant group {TenantId}", 
            connectionId, tenantId);
    }

    /// <summary>
    /// Leave shop group
    /// </summary>
    public async Task LeaveShopGroup(string shopId)
    {
        var connectionId = Context.ConnectionId;
        var groupName = $"shop_{shopId}";

        await Groups.RemoveFromGroupAsync(connectionId, groupName);
        
        if (_shopConnections.TryGetValue(shopId, out var connections))
        {
            connections.Remove(connectionId);
            if (connections.Count == 0)
            {
                _shopConnections.TryRemove(shopId, out _);
            }
        }

        _logger.LogInformation("Connection {ConnectionId} left shop group {ShopId}", 
            connectionId, shopId);
    }

    /// <summary>
    /// Send real-time order update
    /// </summary>
    public async Task SendOrderUpdate(string tenantId, object orderData)
    {
        var groupName = $"tenant_{tenantId}";
        await Clients.Group(groupName).SendAsync("OrderUpdate", orderData);
        
        _logger.LogDebug("Order update sent to tenant group {TenantId}", tenantId);
    }

    /// <summary>
    /// Send real-time shop order update
    /// </summary>
    public async Task SendShopOrderUpdate(string shopId, object orderData)
    {
        var groupName = $"shop_{shopId}";
        await Clients.Group(groupName).SendAsync("ShopOrderUpdate", orderData);
        
        _logger.LogDebug("Shop order update sent to shop group {ShopId}", shopId);
    }

    /// <summary>
    /// Send inventory update
    /// </summary>
    public async Task SendInventoryUpdate(string shopId, object inventoryData)
    {
        var groupName = $"shop_{shopId}";
        await Clients.Group(groupName).SendAsync("InventoryUpdate", inventoryData);
        
        _logger.LogDebug("Inventory update sent to shop group {ShopId}", shopId);
    }

    /// <summary>
    /// Send dashboard metrics update
    /// </summary>
    public async Task SendMetricsUpdate(string tenantId, object metricsData)
    {
        var groupName = $"tenant_{tenantId}";
        await Clients.Group(groupName).SendAsync("MetricsUpdate", metricsData);
        
        _logger.LogDebug("Metrics update sent to tenant group {TenantId}", tenantId);
    }

    /// <summary>
    /// Send shop metrics update
    /// </summary>
    public async Task SendShopMetricsUpdate(string shopId, object shopMetricsData)
    {
        var groupName = $"shop_{shopId}";
        await Clients.Group(groupName).SendAsync("ShopMetricsUpdate", shopMetricsData);
        
        _logger.LogDebug("Shop metrics update sent to shop group {ShopId}", shopId);
    }

    /// <summary>
    /// Send notification to specific connection
    /// </summary>
    public async Task SendNotification(string connectionId, object notificationData)
    {
        await Clients.Client(connectionId).SendAsync("Notification", notificationData);
        
        _logger.LogDebug("Notification sent to connection {ConnectionId}", connectionId);
    }

    /// <summary>
    /// Send notification to tenant group
    /// </summary>
    public async Task SendTenantNotification(string tenantId, object notificationData)
    {
        var groupName = $"tenant_{tenantId}";
        await Clients.Group(groupName).SendAsync("Notification", notificationData);
        
        _logger.LogDebug("Notification sent to tenant group {TenantId}", tenantId);
    }

    /// <summary>
    /// Send notification to shop group
    /// </summary>
    public async Task SendShopNotification(string shopId, object notificationData)
    {
        var groupName = $"shop_{shopId}";
        await Clients.Group(groupName).SendAsync("Notification", notificationData);
        
        _logger.LogDebug("Notification sent to shop group {ShopId}", shopId);
    }

    /// <summary>
    /// Broadcast system-wide announcement
    /// </summary>
    public async Task SendSystemAnnouncement(object announcementData)
    {
        await Clients.All.SendAsync("SystemAnnouncement", announcementData);
        
        _logger.LogInformation("System announcement broadcast to all clients");
    }

    /// <summary>
    /// Get connection statistics
    /// </summary>
    public object GetConnectionStats()
    {
        return new
        {
            TenantConnections = _tenantConnections.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value.Count),
            ShopConnections = _shopConnections.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value.Count),
            TotalConnections = _tenantConnections.Values.Sum(set => set.Count) + 
                             _shopConnections.Values.Sum(set => set.Count)
        };
    }

    /// <summary>
    /// Handle connection lifecycle
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var user = Context.User?.Identity?.Name;
        var connectionId = Context.ConnectionId;
        
        _logger.LogInformation("Client connected: {ConnectionId} (User: {User})", 
            connectionId, user);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var user = Context.User?.Identity?.Name;
        
        // Remove from all groups
        foreach (var kvp in _tenantConnections)
        {
            if (kvp.Value.Contains(connectionId))
            {
                kvp.Value.Remove(connectionId);
                if (kvp.Value.Count == 0)
                {
                    _tenantConnections.TryRemove(kvp.Key, out _);
                }
                break;
            }
        }

        foreach (var kvp in _shopConnections)
        {
            if (kvp.Value.Contains(connectionId))
            {
                kvp.Value.Remove(connectionId);
                if (kvp.Value.Count == 0)
                {
                    _shopConnections.TryRemove(kvp.Key, out _);
                }
                break;
            }
        }

        _logger.LogInformation("Client disconnected: {ConnectionId} (User: {User}, Exception: {Exception})", 
            connectionId, user, exception?.Message);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Send current dashboard state to new connection
    /// </summary>
    private async Task SendCurrentDashboardState(string tenantId)
    {
        try
        {
            // This would typically fetch current dashboard data from services
            // For now, send a placeholder
            var dashboardState = new
            {
                TenantId = tenantId,
                LastUpdated = DateTime.UtcNow,
                OrderCount = 0,
                Revenue = 0m,
                ActiveShops = 0
            };

            await Clients.Caller.SendAsync("DashboardState", dashboardState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send dashboard state for tenant {TenantId}", tenantId);
        }
    }

    /// <summary>
    /// Send current shop dashboard state to new connection
    /// </summary>
    private async Task SendCurrentShopDashboardState(string shopId)
    {
        try
        {
            // This would typically fetch current shop dashboard data from services
            // For now, send a placeholder
            var shopDashboardState = new
            {
                ShopId = shopId,
                LastUpdated = DateTime.UtcNow,
                TodayOrders = 0,
                TodayRevenue = 0m,
                PendingOrders = 0,
                InventoryAlerts = 0
            };

            await Clients.Caller.SendAsync("ShopDashboardState", shopDashboardState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send shop dashboard state for shop {ShopId}", shopId);
        }
    }
}
