using VanAn.CoreHub.Models;
using VanAn.CoreHub.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace VanAn.CoreHub.Services
{
    public interface IDashboardService
    {
        Task<DashboardMetrics> GetPostgreSQLMetricsAsync();
        Task<SQLiteMetrics> GetSQLiteMetricsAsync(string nodeType);
        Task<SyncStatus> GetSyncStatusAsync();
        Task<SystemHealth> GetSystemHealthAsync();
    }

    public class DashboardService(ISystemMetricsRepository metricsRepository, ILogger<DashboardService> logger, IConfiguration configuration) : IDashboardService
    {
        private readonly ISystemMetricsRepository _metricsRepository = metricsRepository;
        private readonly ILogger<DashboardService> _logger = logger;
        private readonly IConfiguration _configuration = configuration;

        public async Task<DashboardMetrics> GetPostgreSQLMetricsAsync()
        {
            try
            {
                DashboardMetrics metrics = new()
                {
                    // Get basic metrics from repository
                    TenantCount = await _metricsRepository.GetTenantCountAsync(),
                    TotalOrders = await _metricsRepository.GetTotalOrdersCountAsync(),
                    TotalRevenue = await _metricsRepository.GetTotalRevenueAsync()
                };

                // Growth calculations (compare with previous periods)
                DateTime oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
                DateTime yesterday = DateTime.UtcNow.AddDays(-1);
                DateTime oneWeekAgo = DateTime.UtcNow.AddDays(-7);

                // Tenant growth
                int tenantsLastMonth = await _metricsRepository.GetTenantCountByDateRangeAsync(oneMonthAgo, DateTime.UtcNow);
                int tenantsPreviousMonth = await _metricsRepository.GetTenantCountByDateRangeAsync(oneMonthAgo.AddMonths(-1), oneMonthAgo);

                metrics.TenantGrowth = tenantsPreviousMonth > 0
                    ? (double)(tenantsLastMonth - tenantsPreviousMonth) / tenantsPreviousMonth * 100
                    : 0;

                // Order growth (daily)
                int ordersToday = await _metricsRepository.GetOrderCountByDateRangeAsync(DateTime.UtcNow.Date, DateTime.UtcNow);
                int ordersYesterday = await _metricsRepository.GetOrderCountByDateRangeAsync(yesterday.Date, DateTime.UtcNow.Date);

                metrics.OrderGrowth = ordersYesterday > 0
                    ? (double)(ordersToday - ordersYesterday) / ordersYesterday * 100
                    : 0;

                // Revenue growth (weekly)
                decimal revenueThisWeek = await _metricsRepository.GetRevenueByDateRangeAsync(oneWeekAgo, DateTime.UtcNow);
                decimal revenueLastWeek = await _metricsRepository.GetRevenueByDateRangeAsync(oneWeekAgo.AddDays(-7), oneWeekAgo);

                metrics.RevenueGrowth = revenueLastWeek > 0
                    ? (double)(revenueThisWeek - revenueLastWeek) / (double)revenueLastWeek * 100
                    : 0;

                // Sync rate - calculate from LastSyncedAt field
                int totalOrders = await _metricsRepository.GetTotalOrdersCountAsync();
                int syncedOrders = await _metricsRepository.GetSyncedOrdersCountAsync();

                metrics.SyncRate = totalOrders > 0
                    ? (double)syncedOrders / totalOrders * 100
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
                SQLiteMetrics metrics = new() { NodeType = nodeType };

                // Determine database path based on node type
                string dbPath = nodeType.ToLower(CultureInfo.InvariantCulture) switch
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

                using Microsoft.Data.Sqlite.SqliteConnection connection = new($"Data Source={dbPath}");
                await connection.OpenAsync();

                // Local Orders
                Microsoft.Data.Sqlite.SqliteCommand orderCommand = connection.CreateCommand();
                orderCommand.CommandText = "SELECT COUNT(*) FROM Orders";
                metrics.LocalOrders = Convert.ToInt32(await orderCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

                // Sync status
                Microsoft.Data.Sqlite.SqliteCommand syncCommand = connection.CreateCommand();
                syncCommand.CommandText = @"
                SELECT 
                    COUNT(*) as Total,
                    COUNT(CASE WHEN LastSyncedAt IS NOT NULL THEN 1 END) as Synced
                FROM Orders";

                using Microsoft.Data.Sqlite.SqliteDataReader reader = await syncCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int total = reader.GetInt32(0);
                    int synced = reader.GetInt32(1);
                    metrics.SyncPercentage = total > 0 ? (double)synced / total * 100 : 0;
                    metrics.PendingSync = total - synced;
                }

                // WAL Mode check
                Microsoft.Data.Sqlite.SqliteCommand walCommand = connection.CreateCommand();
                walCommand.CommandText = "PRAGMA journal_mode";
                string? walMode = await walCommand.ExecuteScalarAsync() as string;
                metrics.IsWalModeEnabled = walMode?.ToLower(CultureInfo.InvariantCulture) == "wal";

                // Last sync time
                Microsoft.Data.Sqlite.SqliteCommand lastSyncCommand = connection.CreateCommand();
                lastSyncCommand.CommandText = "SELECT MAX(LastSyncedAt) FROM Orders WHERE LastSyncedAt IS NOT NULL";
                object? lastSync = await lastSyncCommand.ExecuteScalarAsync();

                if (lastSync != null && lastSync != DBNull.Value)
                {
                    metrics.LastSyncTime = Convert.ToDateTime(lastSync, CultureInfo.InvariantCulture);
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
                SyncStatus status = new();

                // Get KhachLink metrics
                SQLiteMetrics khachLinkMetrics = await GetSQLiteMetricsAsync("KhachLink");
                status.KhachLinkSyncRate = khachLinkMetrics.SyncPercentage;
                status.KhachLinkPendingSync = khachLinkMetrics.PendingSync;
                status.KhachLinkLastSync = khachLinkMetrics.LastSyncDescription;

                // Get ShopERP metrics
                SQLiteMetrics shopErpMetrics = await GetSQLiteMetricsAsync("ShopERP");
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
                SystemHealth health = new();

                // Check PostgreSQL connectivity using repository
                try
                {
                    health.IsPostgresOnline = await _metricsRepository.CanConnectAsync();
                }
                catch
                {
                    health.IsPostgresOnline = false;
                }

                // Check SQLite connectivity
                health.IsKhachLinkOnline = File.Exists(GetKhachLinkDbPath());
                health.IsShopErpOnline = File.Exists(GetShopErpDbPath());

                // Get sync status
                SyncStatus syncStatus = await GetSyncStatusAsync();
                health.SyncIssues = syncStatus.TotalPendingSync;
                health.SyncRate = syncStatus.OverallSyncRate;

                // Calculate health level based on metrics
                HealthStatus level;

                if (health.SyncRate >= 95 && health.SyncIssues == 0)
                {
                    level = HealthStatus.Excellent;
                }
                else
                {
                    level = health.SyncRate >= 85 ? HealthStatus.Good : health.SyncRate >= 70 ? HealthStatus.Warning : HealthStatus.Critical;
                }

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
            string basePath = _configuration["KhachLink:DatabasePath"] ??
                          Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "5_WebApps", "KhachLink");
            return Path.Combine(basePath, "vanan_khachlink.db");
        }

        private string GetShopErpDbPath()
        {
            string basePath = _configuration["ShopERP:DatabasePath"] ??
                          Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "5_WebApps", "ShopERP");
            return Path.Combine(basePath, "vanan_shoperp.db");
        }

        private static string GetRelativeTimeString(DateTime dateTime)
        {
            TimeSpan span = DateTime.UtcNow - dateTime;

            if (span.TotalMinutes < 1)
            {
                return "Just now";
            }

            if (span.TotalMinutes < 60)
            {
                return $"{(int)span.TotalMinutes} minute{((int)span.TotalMinutes != 1 ? "s" : "")} ago";
            }

            return span.TotalHours < 24
                ? $"{(int)span.TotalHours} hour{((int)span.TotalHours != 1 ? "s" : "")} ago"
                : span.TotalDays < 7
                ? $"{(int)span.TotalDays} day{((int)span.TotalDays != 1 ? "s" : "")} ago"
                : dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
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
}
