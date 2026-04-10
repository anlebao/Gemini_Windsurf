using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services;

public interface IDashboardService
{
    Task<DashboardMetrics> GetPostgreSQLMetricsAsync();
    Task<SQLiteMetrics> GetSQLiteMetricsAsync(string nodeType);
    Task<SyncStatus> GetSyncStatusAsync();
    Task<SystemHealth> GetSystemHealthAsync();
}

public class DashboardService : IDashboardService
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<DashboardService> _logger;
    private readonly IConfiguration _configuration;

    public DashboardService(VanAnDbContext context, ILogger<DashboardService> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<DashboardMetrics> GetPostgreSQLMetricsAsync()
    {
        try
        {
            var metrics = new DashboardMetrics();

            // Tenant Count
            metrics.TenantCount = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty)
                .Select(o => o.TenantId)
                .Distinct()
                .CountAsync();

            // Total Orders
            metrics.TotalOrders = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty)
                .CountAsync();

            // Total Revenue - Use client-side evaluation for SQLite compatibility
            var orders = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty)
                .ToListAsync();
            metrics.TotalRevenue = orders.Sum(o => o.TotalAmount);

            // Growth calculations (compare with previous periods)
            var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
            var yesterday = DateTime.UtcNow.AddDays(-1);
            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);

            // Tenant growth
            var tenantsLastMonth = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty && o.CreatedAt >= oneMonthAgo)
                .Select(o => o.TenantId)
                .Distinct()
                .CountAsync();
            
            var tenantsPreviousMonth = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty && o.CreatedAt >= oneMonthAgo.AddMonths(-1) && o.CreatedAt < oneMonthAgo)
                .Select(o => o.TenantId)
                .Distinct()
                .CountAsync();

            metrics.TenantGrowth = tenantsPreviousMonth > 0 
                ? ((double)(tenantsLastMonth - tenantsPreviousMonth) / tenantsPreviousMonth) * 100 
                : 0;

            // Order growth (daily)
            var ordersToday = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty && o.CreatedAt >= DateTime.UtcNow.Date)
                .CountAsync();

            var ordersYesterday = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty && o.CreatedAt >= yesterday.Date && o.CreatedAt < DateTime.UtcNow.Date)
                .CountAsync();

            metrics.OrderGrowth = ordersYesterday > 0 
                ? ((double)(ordersToday - ordersYesterday) / ordersYesterday) * 100 
                : 0;

            // Revenue growth (weekly) - Use client-side evaluation for SQLite compatibility
            var revenueThisWeekOrders = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty && o.CreatedAt >= oneWeekAgo)
                .ToListAsync();
            var revenueThisWeek = revenueThisWeekOrders.Sum(o => o.TotalAmount);

            var revenueLastWeekOrders = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty && o.CreatedAt >= oneWeekAgo.AddDays(-7) && o.CreatedAt < oneWeekAgo)
                .ToListAsync();
            var revenueLastWeek = revenueLastWeekOrders.Sum(o => o.TotalAmount);

            metrics.RevenueGrowth = revenueLastWeek > 0 
                ? ((double)(revenueThisWeek - revenueLastWeek) / (double)revenueLastWeek) * 100 
                : 0;

            // Sync Rate
            var totalOrdersWithSync = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty)
                .CountAsync();

            var syncedOrders = await _context.Orders
                .Where(o => o.TenantId != Guid.Empty && o.LastSyncedAt != null && o.LastSyncedAt > default(DateTime))
                .CountAsync();

            metrics.SyncRate = totalOrdersWithSync > 0 
                ? ((double)syncedOrders / totalOrdersWithSync) * 100 
                : 0;

            metrics.LastUpdated = DateTime.UtcNow;

            _logger.LogInformation("PostgreSQL metrics loaded: {Tenants} tenants, {Orders} orders, {Revenue:C2} revenue", 
                metrics.TenantCount, metrics.TotalOrders, metrics.TotalRevenue);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading PostgreSQL metrics");
            throw;
        }
    }

    public async Task<SQLiteMetrics> GetSQLiteMetricsAsync(string nodeType)
    {
        try
        {
            var metrics = new SQLiteMetrics { NodeType = nodeType };

            // Determine database path based on node type
            var dbPath = nodeType.ToLower() switch
            {
                "khachlink" => GetKhachLinkDbPath(),
                "shoperp" => GetShopErpDbPath(),
                _ => throw new ArgumentException($"Unknown node type: {nodeType}")
            };

            if (!File.Exists(dbPath))
            {
                _logger.LogWarning("SQLite database not found: {DbPath}", dbPath);
                return metrics;
            }

            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            // Local Orders
            var orderCommand = connection.CreateCommand();
            orderCommand.CommandText = "SELECT COUNT(*) FROM Orders";
            metrics.LocalOrders = Convert.ToInt32(await orderCommand.ExecuteScalarAsync());

            // Sync status
            var syncCommand = connection.CreateCommand();
            syncCommand.CommandText = @"
                SELECT 
                    COUNT(*) as Total,
                    COUNT(CASE WHEN LastSyncedAt IS NOT NULL THEN 1 END) as Synced
                FROM Orders";
            
            using var reader = await syncCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var total = reader.GetInt32(0);
                var synced = reader.GetInt32(1);
                metrics.SyncPercentage = total > 0 ? (double)synced / total * 100 : 0;
                metrics.PendingSync = total - synced;
            }

            // WAL Mode check
            var walCommand = connection.CreateCommand();
            walCommand.CommandText = "PRAGMA journal_mode";
            var walMode = await walCommand.ExecuteScalarAsync() as string;
            metrics.IsWalModeEnabled = walMode?.ToLower() == "wal";

            // Last sync time
            var lastSyncCommand = connection.CreateCommand();
            lastSyncCommand.CommandText = "SELECT MAX(LastSyncedAt) FROM Orders WHERE LastSyncedAt IS NOT NULL";
            var lastSync = await lastSyncCommand.ExecuteScalarAsync();
            
            if (lastSync != null && lastSync != DBNull.Value)
            {
                metrics.LastSyncTime = Convert.ToDateTime(lastSync);
                metrics.LastSyncDescription = GetRelativeTimeString(metrics.LastSyncTime.Value);
            }
            else
            {
                metrics.LastSyncDescription = "Never synced";
            }

            metrics.LastUpdated = DateTime.UtcNow;

            _logger.LogInformation("SQLite metrics loaded for {NodeType}: {Orders} orders, {SyncPercentage:F1}% synced", 
                nodeType, metrics.LocalOrders, metrics.SyncPercentage);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading SQLite metrics for {NodeType}", nodeType);
            throw;
        }
    }

    public async Task<SyncStatus> GetSyncStatusAsync()
    {
        try
        {
            var status = new SyncStatus();

            // Get KhachLink metrics
            var khachLinkMetrics = await GetSQLiteMetricsAsync("KhachLink");
            status.KhachLinkSyncRate = khachLinkMetrics.SyncPercentage;
            status.KhachLinkPendingSync = khachLinkMetrics.PendingSync;
            status.KhachLinkLastSync = khachLinkMetrics.LastSyncDescription;

            // Get ShopERP metrics
            var shopErpMetrics = await GetSQLiteMetricsAsync("ShopERP");
            status.ShopErpSyncRate = shopErpMetrics.SyncPercentage;
            status.ShopErpPendingSync = shopErpMetrics.PendingSync;
            status.ShopErpLastSync = shopErpMetrics.LastSyncDescription;

            // Overall sync status
            status.TotalPendingSync = status.KhachLinkPendingSync + status.ShopErpPendingSync;
            status.OverallSyncRate = (khachLinkMetrics.SyncPercentage + shopErpMetrics.SyncPercentage) / 2;
            status.IsHealthy = status.TotalPendingSync < 50 && status.OverallSyncRate >= 80;

            status.LastUpdated = DateTime.UtcNow;

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync status");
            throw;
        }
    }

    public async Task<SystemHealth> GetSystemHealthAsync()
    {
        try
        {
            var health = new SystemHealth();

            // Check PostgreSQL connectivity
            try
            {
                await _context.Database.CanConnectAsync();
                health.IsPostgresOnline = true;
            }
            catch
            {
                health.IsPostgresOnline = false;
            }

            // Check SQLite connectivity
            health.IsKhachLinkOnline = File.Exists(GetKhachLinkDbPath());
            health.IsShopErpOnline = File.Exists(GetShopErpDbPath());

            // Get sync status
            var syncStatus = await GetSyncStatusAsync();
            health.SyncIssues = syncStatus.TotalPendingSync;
            health.SyncRate = syncStatus.OverallSyncRate;

            // Calculate health level based on metrics
            HealthStatus level;
            
            if (health.SyncRate >= 95 && health.SyncIssues == 0)
                level = HealthStatus.Excellent;
            else if (health.SyncRate >= 85)
                level = HealthStatus.Good;
            else if (health.SyncRate >= 70)
                level = HealthStatus.Warning;
            else
                level = HealthStatus.Critical;

            health.StatusLevel = level;
            health.IsHealthy = level >= HealthStatus.Good;
            health.Status = level.ToString();
            health.LastUpdated = DateTime.UtcNow;

            return health;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system health");
            return new SystemHealth
            {
                IsHealthy = false,
                StatusLevel = HealthStatus.Critical,
                Status = "Critical",
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    private string GetKhachLinkDbPath()
    {
        var basePath = _configuration["KhachLink:DatabasePath"] ?? 
                      Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "5_WebApps", "KhachLink");
        return Path.Combine(basePath, "vanan_khachlink.db");
    }

    private string GetShopErpDbPath()
    {
        var basePath = _configuration["ShopERP:DatabasePath"] ?? 
                      Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "5_WebApps", "ShopERP");
        return Path.Combine(basePath, "vanan_shoperp.db");
    }

    private string GetRelativeTimeString(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime;
        
        if (span.TotalMinutes < 1)
            return "Just now";
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes} minute{((int)span.TotalMinutes != 1 ? "s" : "")} ago";
        if (span.TotalHours < 24)
            return $"{(int)span.TotalHours} hour{((int)span.TotalHours != 1 ? "s" : "")} ago";
        if (span.TotalDays < 7)
            return $"{(int)span.TotalDays} day{((int)span.TotalDays != 1 ? "s" : "")} ago";
        
        return dateTime.ToString("yyyy-MM-dd");
    }
}

// Data models for dashboard
public class DashboardMetrics
{
    public int TenantCount { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public double SyncRate { get; set; }
    public double TenantGrowth { get; set; }
    public double OrderGrowth { get; set; }
    public double RevenueGrowth { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class SQLiteMetrics
{
    public string NodeType { get; set; } = string.Empty;
    public int LocalOrders { get; set; }
    public double SyncPercentage { get; set; }
    public int PendingSync { get; set; }
    public bool IsWalModeEnabled { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public string LastSyncDescription { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

public class SyncStatus
{
    public double KhachLinkSyncRate { get; set; }
    public double ShopErpSyncRate { get; set; }
    public int KhachLinkPendingSync { get; set; }
    public int ShopErpPendingSync { get; set; }
    public int TotalPendingSync { get; set; }
    public double OverallSyncRate { get; set; }
    public string KhachLinkLastSync { get; set; } = string.Empty;
    public string ShopErpLastSync { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class SystemHealth
{
    public bool IsHealthy { get; set; }
    public HealthStatus StatusLevel { get; set; } = HealthStatus.Unknown;
    public bool IsPostgresOnline { get; set; }
    public bool IsKhachLinkOnline { get; set; }
    public bool IsShopErpOnline { get; set; }
    public int SyncIssues { get; set; }
    public double SyncRate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}
