using Microsoft.AspNetCore.SignalR;
using VanAn.Shared.Domain;
using VanAn.KhachLink.Hubs;
using System.Collections.Concurrent;

namespace VanAn.KhachLink.Services.Dashboard;

/// <summary>
/// Real-time Dashboard Service - Phase 2.5.2
/// Provides real-time data aggregation and broadcasting for dashboard metrics
/// </summary>
public class RealTimeDashboardService : IAsyncDisposable
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<RealTimeDashboardService> _logger;
    private readonly Timer _metricsUpdateTimer;
    private readonly ConcurrentDictionary<string, DateTime> _lastUpdates = new();
    private readonly ConcurrentDictionary<string, object> _cachedMetrics = new();

    public RealTimeDashboardService(
        IHubContext<DashboardHub> hubContext,
        ILogger<RealTimeDashboardService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
        
        // Start periodic metrics update (every 30 seconds)
        _metricsUpdateTimer = new Timer(UpdateAllMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Broadcast order update to relevant groups
    /// </summary>
    public async Task BroadcastOrderUpdateAsync(string tenantId, string? shopId, object orderData)
    {
        try
        {
            // Send to tenant group
            await _hubContext.Clients.Group($"tenant_{tenantId}")
                .SendAsync("OrderUpdate", new
                {
                    TenantId = tenantId,
                    OrderData = orderData,
                    Timestamp = DateTime.UtcNow,
                    Type = "OrderUpdate"
                });

            // Send to specific shop group if provided
            if (!string.IsNullOrEmpty(shopId))
            {
                await _hubContext.Clients.Group($"shop_{shopId}")
                    .SendAsync("ShopOrderUpdate", new
                    {
                        ShopId = shopId,
                        OrderData = orderData,
                        Timestamp = DateTime.UtcNow,
                        Type = "ShopOrderUpdate"
                    });
            }

            // Update cached metrics
            await UpdateTenantMetricsAsync(tenantId);
            if (!string.IsNullOrEmpty(shopId))
            {
                await UpdateShopMetricsAsync(shopId);
            }

            _logger.LogDebug("Order update broadcasted for tenant {TenantId}, shop {ShopId}", tenantId, shopId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast order update for tenant {TenantId}, shop {ShopId}", tenantId, shopId);
        }
    }

    /// <summary>
    /// Broadcast inventory update
    /// </summary>
    public async Task BroadcastInventoryUpdateAsync(string shopId, object inventoryData)
    {
        try
        {
            await _hubContext.Clients.Group($"shop_{shopId}")
                .SendAsync("InventoryUpdate", new
                {
                    ShopId = shopId,
                    InventoryData = inventoryData,
                    Timestamp = DateTime.UtcNow,
                    Type = "InventoryUpdate"
                });

            // Update shop metrics
            await UpdateShopMetricsAsync(shopId);

            _logger.LogDebug("Inventory update broadcasted for shop {ShopId}", shopId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast inventory update for shop {ShopId}", shopId);
        }
    }

    /// <summary>
    /// Send notification to specific user or group
    /// </summary>
    public async Task SendNotificationAsync(string tenantId, string? shopId, string? connectionId, object notificationData)
    {
        try
        {
            var notification = new
            {
                TenantId = tenantId,
                ShopId = shopId,
                NotificationData = notificationData,
                Timestamp = DateTime.UtcNow,
                Type = "Notification"
            };

            if (!string.IsNullOrEmpty(connectionId))
            {
                // Send to specific connection
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("Notification", notification);
            }
            else if (!string.IsNullOrEmpty(shopId))
            {
                // Send to shop group
                await _hubContext.Clients.Group($"shop_{shopId}")
                    .SendAsync("Notification", notification);
            }
            else
            {
                // Send to tenant group
                await _hubContext.Clients.Group($"tenant_{tenantId}")
                    .SendAsync("Notification", notification);
            }

            _logger.LogDebug("Notification sent for tenant {TenantId}, shop {ShopId}, connection {ConnectionId}", 
                tenantId, shopId, connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for tenant {TenantId}, shop {ShopId}, connection {ConnectionId}", 
                tenantId, shopId, connectionId);
        }
    }

    /// <summary>
    /// Update and broadcast tenant metrics
    /// </summary>
    public async Task UpdateTenantMetricsAsync(string tenantId)
    {
        try
        {
            var metrics = await GenerateTenantMetricsAsync(tenantId);
            
            // Cache metrics
            _cachedMetrics.AddOrUpdate($"tenant_{tenantId}", metrics, (_, __) => metrics);
            _lastUpdates.AddOrUpdate($"tenant_{tenantId}", DateTime.UtcNow, (_, __) => DateTime.UtcNow);

            // Broadcast to tenant group
            await _hubContext.Clients.Group($"tenant_{tenantId}")
                .SendAsync("MetricsUpdate", new
                {
                    TenantId = tenantId,
                    MetricsData = metrics,
                    Timestamp = DateTime.UtcNow,
                    Type = "MetricsUpdate"
                });

            _logger.LogDebug("Tenant metrics updated for {TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tenant metrics for {TenantId}", tenantId);
        }
    }

    /// <summary>
    /// Update and broadcast shop metrics
    /// </summary>
    public async Task UpdateShopMetricsAsync(string shopId)
    {
        try
        {
            var metrics = await GenerateShopMetricsAsync(shopId);
            
            // Cache metrics
            _cachedMetrics.AddOrUpdate($"shop_{shopId}", metrics, (_, __) => metrics);
            _lastUpdates.AddOrUpdate($"shop_{shopId}", DateTime.UtcNow, (_, __) => DateTime.UtcNow);

            // Broadcast to shop group
            await _hubContext.Clients.Group($"shop_{shopId}")
                .SendAsync("ShopMetricsUpdate", new
                {
                    ShopId = shopId,
                    ShopMetricsData = metrics,
                    Timestamp = DateTime.UtcNow,
                    Type = "ShopMetricsUpdate"
                });

            _logger.LogDebug("Shop metrics updated for {ShopId}", shopId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update shop metrics for {ShopId}", shopId);
        }
    }

    /// <summary>
    /// Get cached metrics for tenant
    /// </summary>
    public object? GetTenantMetrics(string tenantId)
    {
        _cachedMetrics.TryGetValue($"tenant_{tenantId}", out var metrics);
        return metrics;
    }

    /// <summary>
    /// Get cached metrics for shop
    /// </summary>
    public object? GetShopMetrics(string shopId)
    {
        _cachedMetrics.TryGetValue($"shop_{shopId}", out var metrics);
        return metrics;
    }

    /// <summary>
    /// Get last update time for metrics
    /// </summary>
    public DateTime? GetLastUpdateTime(string key)
    {
        _lastUpdates.TryGetValue(key, out var lastUpdate);
        return lastUpdate;
    }

    /// <summary>
    /// Generate mock tenant metrics (would connect to actual services in production)
    /// </summary>
    private async Task<object> GenerateTenantMetricsAsync(string tenantId)
    {
        // In production, this would query actual data from services
        await Task.Delay(50); // Simulate async operation

        var random = new Random();
        var baseValue = random.Next(1000, 10000);

        return new
        {
            TenantId = tenantId,
            TotalOrders = baseValue,
            TodayOrders = baseValue / 10,
            TotalRevenue = baseValue * 25000m,
            TodayRevenue = (baseValue / 10) * 25000m,
            ActiveShops = random.Next(1, 10),
            PendingOrders = random.Next(0, 20),
            CompletedOrders = baseValue - random.Next(0, 20),
            AverageOrderValue = 25000m,
            GrowthRate = random.Next(-10, 25),
            CustomerCount = baseValue * 3,
            NewCustomersToday = random.Next(5, 50),
            TopProducts = GenerateMockTopProducts(),
            RecentActivity = GenerateMockRecentActivity()
        };
    }

    /// <summary>
    /// Generate mock shop metrics (would connect to actual services in production)
    /// </summary>
    private async Task<object> GenerateShopMetricsAsync(string shopId)
    {
        // In production, this would query actual data from services
        await Task.Delay(50); // Simulate async operation

        var random = new Random();
        var baseValue = random.Next(100, 1000);

        return new
        {
            ShopId = shopId,
            TodayOrders = baseValue,
            TodayRevenue = baseValue * 25000m,
            PendingOrders = random.Next(0, 10),
            CompletedOrders = baseValue - random.Next(0, 10),
            AverageOrderValue = 25000m,
            InventoryAlerts = random.Next(0, 5),
            LowStockItems = GenerateMockLowStockItems(),
            TopSellingItems = GenerateMockTopSellingItems(),
            StaffPerformance = GenerateMockStaffPerformance(),
            HourlyStats = GenerateMockHourlyStats(),
            CustomerSatisfaction = random.NextDouble() * (5.0 - 3.5) + 3.5,
            ProcessingTime = TimeSpan.FromMinutes(random.Next(5, 25))
        };
    }

    /// <summary>
    /// Periodic metrics update for all active connections
    /// </summary>
    private async void UpdateAllMetrics(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var keysToUpdate = new List<string>();

            // Find keys that need updating (older than 30 seconds)
            foreach (var kvp in _lastUpdates)
            {
                if (now - kvp.Value > TimeSpan.FromSeconds(30))
                {
                    keysToUpdate.Add(kvp.Key);
                }
            }

            // Update metrics for stale keys
            var updateTasks = new List<Task>();
            foreach (var key in keysToUpdate)
            {
                if (key.StartsWith("tenant_", StringComparison.Ordinal))
                {
                    var tenantId = key.Substring(7); // Remove "tenant_" prefix
                    updateTasks.Add(UpdateTenantMetricsAsync(tenantId));
                }
                else if (key.StartsWith("shop_", StringComparison.Ordinal))
                {
                    var shopId = key.Substring(5); // Remove "shop_" prefix
                    updateTasks.Add(UpdateShopMetricsAsync(shopId));
                }
            }

            if (updateTasks.Count > 0)
            {
                await Task.WhenAll(updateTasks);
                _logger.LogDebug("Updated metrics for {Count} keys", updateTasks.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update all metrics");
        }
    }

    /// <summary>
    /// Mock data generators (would be replaced with actual service calls)
    /// </summary>
    private object[] GenerateMockTopProducts()
    {
        var random = new Random();
        return new object[]
        {
            new { Name = "Trà sữa", Sales = random.Next(50, 200), Revenue = random.Next(50, 200) * 25000m },
            new { Name = "Cà phê", Sales = random.Next(30, 150), Revenue = random.Next(30, 150) * 20000m },
            new { Name = "Bánh ngọt", Sales = random.Next(20, 100), Revenue = random.Next(20, 100) * 15000m }
        };
    }

    private object[] GenerateMockRecentActivity()
    {
        var random = new Random();
        return new object[]
        {
            new { Action = "Đơn hàng mới", Time = DateTime.UtcNow.AddMinutes(-random.Next(1, 60)), Details = "Khách hàng A" },
            new { Action = "Hoàn thành đơn", Time = DateTime.UtcNow.AddMinutes(-random.Next(1, 120)), Details = "Đơn #123" },
            new { Action = "Cập nhật kho", Time = DateTime.UtcNow.AddMinutes(-random.Next(1, 30)), Details = "Trà sữa" }
        };
    }

    private object[] GenerateMockLowStockItems()
    {
        var random = new Random();
        return new object[]
        {
            new { Name = "Trà", CurrentStock = random.Next(1, 10), MinStock = 20 },
            new { Name = "Sữa", CurrentStock = random.Next(1, 5), MinStock = 15 }
        };
    }

    private object[] GenerateMockTopSellingItems()
    {
        var random = new Random();
        return new object[]
        {
            new { Name = "Trà sữa lớn", Sales = random.Next(20, 80), Revenue = random.Next(20, 80) * 35000m },
            new { Name = "Cà phê đá", Sales = random.Next(15, 60), Revenue = random.Next(15, 60) * 25000m }
        };
    }

    private object[] GenerateMockStaffPerformance()
    {
        var random = new Random();
        return new object[]
        {
            new { Name = "Nhân viên A", OrdersProcessed = random.Next(10, 50), AverageTime = TimeSpan.FromMinutes(random.Next(5, 15)) },
            new { Name = "Nhân viên B", OrdersProcessed = random.Next(8, 40), AverageTime = TimeSpan.FromMinutes(random.Next(6, 18)) }
        };
    }

    private object[] GenerateMockHourlyStats()
    {
        var random = new Random();
        var stats = new List<object>();
        var now = DateTime.UtcNow;
        
        for (int i = 0; i < 8; i++)
        {
            var hour = now.AddHours(-7 + i);
            stats.Add(new
            {
                Hour = hour.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                Orders = random.Next(0, 30),
                Revenue = random.Next(0, 30) * 25000m
            });
        }
        
        return stats.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        _metricsUpdateTimer?.Dispose();
    }
}
