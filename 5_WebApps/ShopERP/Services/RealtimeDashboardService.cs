using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace VanAn.ShopERP.Services;

public interface IRealtimeDashboardService
{
    Task<OrderMetrics> GetCurrentMetricsAsync();
    Task<List<Order>> GetRecentOrdersAsync(int count = 10);
    Task<List<Order>> GetActiveOrdersAsync();
    Task<bool> BroadcastOrderUpdateAsync(Order order);
    Task<bool> BroadcastMetricsUpdateAsync();
    Task<bool> SubscribeToUpdatesAsync(string connectionId);
    Task<bool> UnsubscribeFromUpdatesAsync(string connectionId);
    Task<List<DashboardAlert>> GetActiveAlertsAsync();
    Task<bool> CreateAlertAsync(DashboardAlert alert);
}

public class DashboardAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public AlertType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsRead { get; set; }
    public string? ActionUrl { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum AlertType
{
    OrderCreated,
    OrderUpdated,
    OrderCancelled,
    HighValueOrder,
    SystemError,
    LowInventory,
    StaffAssignment
}

public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public class RealtimeDashboardService : IRealtimeDashboardService
{
    private readonly IOrderManagementService _orderManagementService;
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<RealtimeDashboardService> _logger;
    private readonly IMemoryCache _cache;
    private readonly List<string> _subscribedConnections = new();
    private readonly object _lock = new object();
    
    public RealtimeDashboardService(
        IOrderManagementService orderManagementService,
        IHubContext<DashboardHub> hubContext,
        ILogger<RealtimeDashboardService> logger,
        IMemoryCache cache)
    {
        _orderManagementService = orderManagementService;
        _hubContext = hubContext;
        _logger = logger;
        _cache = cache;
    }
    
    public async Task<OrderMetrics> GetCurrentMetricsAsync()
    {
        try
        {
            var cacheKey = "dashboard_metrics";
            if (_cache.TryGetValue(cacheKey, out OrderMetrics? cachedMetrics))
            {
                return cachedMetrics ?? new OrderMetrics();
            }
            
            var metrics = await _orderManagementService.GetOrderMetricsAsync();
            
            // Cache for 30 seconds
            _cache.Set(cacheKey, metrics, TimeSpan.FromSeconds(30));
            
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current dashboard metrics");
            return new OrderMetrics();
        }
    }
    
    public async Task<List<Order>> GetRecentOrdersAsync(int count = 10)
    {
        try
        {
            var cacheKey = $"recent_orders_{count}";
            if (_cache.TryGetValue(cacheKey, out List<Order>? cachedOrders))
            {
                return cachedOrders ?? new List<Order>();
            }
            
            var allOrders = await _orderManagementService.GetOrdersAsync();
            var recentOrders = allOrders
                .OrderByDescending(o => o.CreatedAt)
                .Take(count)
                .ToList();
            
            // Cache for 15 seconds
            _cache.Set(cacheKey, recentOrders, TimeSpan.FromSeconds(15));
            
            return recentOrders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent orders");
            return new List<Order>();
        }
    }
    
    public async Task<List<Order>> GetActiveOrdersAsync()
    {
        try
        {
            var cacheKey = "active_orders";
            if (_cache.TryGetValue(cacheKey, out List<Order>? cachedOrders))
            {
                return cachedOrders ?? new List<Order>();
            }
            
            var pendingOrders = await _orderManagementService.GetOrdersAsync(OrderStatusId.Pending);
            var processingOrders = await _orderManagementService.GetOrdersAsync(OrderStatusId.Processing);
            
            var activeOrders = pendingOrders.Concat(processingOrders)
                .OrderByDescending(o => o.CreatedAt)
                .ToList();
            
            // Cache for 10 seconds
            _cache.Set(cacheKey, activeOrders, TimeSpan.FromSeconds(10));
            
            return activeOrders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active orders");
            return new List<Order>();
        }
    }
    
    public async Task<bool> BroadcastOrderUpdateAsync(Order order)
    {
        try
        {
            lock (_lock)
            {
                if (!_subscribedConnections.Any())
                {
                    return false; // No subscribers
                }
            }
            
            // Invalidate relevant caches
            _cache.Remove("dashboard_metrics");
            _cache.Remove("recent_orders_10");
            _cache.Remove("active_orders");
            
            // Create alert for high-value orders
            if (order.TotalAmount > 1000000) // 1 million VND
            {
                await CreateAlertAsync(new DashboardAlert
                {
                    Type = AlertType.HighValueOrder,
                    Title = "High Value Order",
                    Message = $"Order {order.Id} with total {order.TotalAmount:N0} VND",
                    Severity = AlertSeverity.Info,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    Metadata = new Dictionary<string, object>
                    {
                        ["OrderId"] = order.Id,
                        ["TotalAmount"] = order.TotalAmount
                    }
                });
            }
            
            // Broadcast to all subscribed clients
            await _hubContext.Clients.All.SendAsync("OrderUpdated", new
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                ItemCount = order.Items.Count
            });
            
            // Also broadcast updated metrics
            await BroadcastMetricsUpdateAsync();
            
            _logger.LogInformation("Broadcasted order update: {OrderId}", order.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast order update: {OrderId}", order.Id);
            return false;
        }
    }
    
    public async Task<bool> BroadcastMetricsUpdateAsync()
    {
        try
        {
            lock (_lock)
            {
                if (!_subscribedConnections.Any())
                {
                    return false; // No subscribers
                }
            }
            
            var metrics = await GetCurrentMetricsAsync();
            
            await _hubContext.Clients.All.SendAsync("MetricsUpdated", new
            {
                TotalOrders = metrics.TotalOrders,
                PendingOrders = metrics.PendingOrders,
                ProcessingOrders = metrics.ProcessingOrders,
                CompletedOrders = metrics.CompletedOrders,
                CancelledOrders = metrics.CancelledOrders,
                TotalRevenue = metrics.TotalRevenue,
                AverageOrderValue = metrics.AverageOrderValue,
                OrdersPerHour = metrics.OrdersPerHour,
                RevenuePerHour = metrics.RevenuePerHour,
                StatusBreakdown = metrics.StatusBreakdown,
                Timestamp = DateTime.UtcNow
            });
            
            _logger.LogInformation("Broadcasted metrics update");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast metrics update");
            return false;
        }
    }
    
    public async Task<bool> SubscribeToUpdatesAsync(string connectionId)
    {
        try
        {
            lock (_lock)
            {
                if (!_subscribedConnections.Contains(connectionId))
                {
                    _subscribedConnections.Add(connectionId);
                }
            }
            
            // Send initial data
            await _hubContext.Clients.Client(connectionId).SendAsync("InitialData", new
            {
                Metrics = await GetCurrentMetricsAsync(),
                RecentOrders = await GetRecentOrdersAsync(10),
                ActiveOrders = await GetActiveOrdersAsync(),
                Alerts = await GetActiveAlertsAsync()
            });
            
            _logger.LogInformation("Client subscribed to dashboard updates: {ConnectionId}", connectionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe client to updates: {ConnectionId}", connectionId);
            return false;
        }
    }
    
    public async Task<bool> UnsubscribeFromUpdatesAsync(string connectionId)
    {
        try
        {
            lock (_lock)
            {
                _subscribedConnections.Remove(connectionId);
            }
            
            _logger.LogInformation("Client unsubscribed from dashboard updates: {ConnectionId}", connectionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe client from updates: {ConnectionId}", connectionId);
            return false;
        }
    }
    
    public async Task<List<DashboardAlert>> GetActiveAlertsAsync()
    {
        try
        {
            var cacheKey = "active_alerts";
            if (_cache.TryGetValue(cacheKey, out List<DashboardAlert>? cachedAlerts))
            {
                return cachedAlerts ?? new List<DashboardAlert>();
            }
            
            // In a real implementation, this would fetch from a database
            // For now, we'll return some sample alerts
            var alerts = new List<DashboardAlert>
            {
                new DashboardAlert
                {
                    Type = AlertType.SystemError,
                    Title = "System Performance",
                    Message = "Dashboard running normally",
                    Severity = AlertSeverity.Info,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                    IsRead = false
                }
            };
            
            // Cache for 1 minute
            _cache.Set(cacheKey, alerts, TimeSpan.FromMinutes(1));
            
            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active alerts");
            return new List<DashboardAlert>();
        }
    }
    
    public async Task<bool> CreateAlertAsync(DashboardAlert alert)
    {
        try
        {
            alert.CreatedAt = DateTime.UtcNow;
            
            // Invalidate alerts cache
            _cache.Remove("active_alerts");
            
            // Broadcast alert to all subscribed clients
            lock (_lock)
            {
                if (_subscribedConnections.Any())
                {
                    _hubContext.Clients.All.SendAsync("AlertCreated", new
                    {
                        alert.Id,
                        alert.Type,
                        alert.Title,
                        alert.Message,
                        alert.Severity,
                        alert.CreatedAt,
                        alert.ExpiresAt,
                        alert.ActionUrl,
                        alert.Metadata
                    });
                }
            }
            
            _logger.LogInformation("Created dashboard alert: {Type} - {Title}", alert.Type, alert.Title);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create dashboard alert: {Type}", alert.Type);
            return false;
        }
    }
}

// SignalR Hub for real-time dashboard communication
public class DashboardHub : Hub
{
    private readonly IRealtimeDashboardService _dashboardService;
    private readonly ILogger<DashboardHub> _logger;
    
    public DashboardHub(IRealtimeDashboardService dashboardService, ILogger<DashboardHub> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }
    
    public async Task SubscribeToUpdates()
    {
        await _dashboardService.SubscribeToUpdatesAsync(Context.ConnectionId);
    }
    
    public async Task UnsubscribeFromUpdates()
    {
        await _dashboardService.UnsubscribeFromUpdatesAsync(Context.ConnectionId);
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _dashboardService.UnsubscribeFromUpdatesAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
