using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// API surface for dashboard metrics exposed to KhachLink via Gateway.
    /// Business logic remains in CoreHub DashboardService; this controller is a thin adapter.
    /// FIX-BATCH-4: Added /shop-metrics/{shopId} endpoint to replace SignalR DashboardHub.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController(
        IDashboardService dashboardService,
        IVanAnDbContext dbContext,
        ILogger<DashboardController> logger) : ControllerBase
    {
        private readonly IDashboardService _dashboardService = dashboardService;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<DashboardController> _logger = logger;

        [HttpGet("postgresql-metrics")]
        [AllowAnonymous]
        public async Task<ActionResult<DashboardMetrics>> GetPostgreSQLMetrics()
        {
            try
            {
                DashboardMetrics metrics = await _dashboardService.GetPostgreSQLMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting PostgreSQL metrics");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("sqlite-metrics/{nodeType}")]
        [AllowAnonymous]
        public async Task<ActionResult<SQLiteMetrics>> GetSQLiteMetrics(string nodeType)
        {
            try
            {
                SQLiteMetrics metrics = await _dashboardService.GetSQLiteMetricsAsync(nodeType);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SQLite metrics for {NodeType}", nodeType);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("sync-status")]
        [AllowAnonymous]
        public async Task<ActionResult<SyncStatus>> GetSyncStatus()
        {
            try
            {
                SyncStatus status = await _dashboardService.GetSyncStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sync status");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("system-health")]
        [AllowAnonymous]
        public async Task<ActionResult<SystemHealth>> GetSystemHealth()
        {
            try
            {
                SystemHealth health = await _dashboardService.GetSystemHealthAsync();
                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system health");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// FIX-BATCH-4: Get shop-specific dashboard metrics for KhachLink staff dashboard.
        /// Replaces the SignalR DashboardHub.RequestShopMetrics method.
        /// Returns today's orders, revenue, pending count, average order value, growth rate.
        /// </summary>
        [HttpGet("shop-metrics/{shopId:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<ShopDashboardMetrics>> GetShopMetrics(Guid shopId)
        {
            try
            {
                var tenantId = new TenantId(shopId);
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                var todayOrders = await _dbContext.Orders
                    .Where(o => o.TenantId == tenantId && o.CreatedAt >= today && o.CreatedAt < tomorrow)
                    .ToListAsync();

                int orderCount = todayOrders.Count;
                decimal totalRevenue = todayOrders.Sum(o => o.TotalAmount);
                int pendingOrders = todayOrders.Count(o => o.Status == OrderStatusId.Pending);
                decimal avgOrderValue = orderCount > 0 ? totalRevenue / orderCount : 0;

                // Compare with yesterday for growth rate
                var yesterday = today.AddDays(-1);
                var yesterdayOrders = await _dbContext.Orders
                    .Where(o => o.TenantId == tenantId && o.CreatedAt >= yesterday && o.CreatedAt < today)
                    .ToListAsync();
                decimal yesterdayRevenue = yesterdayOrders.Sum(o => o.TotalAmount);
                decimal growthRate = yesterdayRevenue > 0
                    ? ((totalRevenue - yesterdayRevenue) / yesterdayRevenue) * 100m
                    : 0m;

                var metrics = new ShopDashboardMetrics
                {
                    ShopId = shopId,
                    LastUpdated = DateTime.UtcNow,
                    TodayOrders = orderCount,
                    TodayRevenue = totalRevenue,
                    PendingOrders = pendingOrders,
                    AverageOrderValue = avgOrderValue,
                    GrowthRate = (double)growthRate,
                    ProcessingTimeMinutes = 0 // Placeholder — would require order timeline tracking
                };

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shop metrics for {ShopId}", shopId);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    /// <summary>
    /// FIX-BATCH-4: Shop-specific dashboard metrics DTO.
    /// Shape matches what RealTimeDashboard.razor expects (replaces SignalR dynamic payload).
    /// Fields not yet populated from real data are nullable/defaulted — UI handles nulls gracefully.
    /// </summary>
    public class ShopDashboardMetrics
    {
        public Guid ShopId { get; set; }
        public DateTime LastUpdated { get; set; }
        public int TodayOrders { get; set; }
        public decimal TodayRevenue { get; set; }
        public int PendingOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public double GrowthRate { get; set; }
        public int ProcessingTimeMinutes { get; set; }
        // Placeholder fields — UI bindings reference these, return defaults until wired
        public double? CustomerSatisfaction { get; set; }
        public List<HourlyStat>? HourlyStats { get; set; }
        public List<TopSellingItem>? TopSellingItems { get; set; }
        public List<RecentActivityEntry>? RecentActivity { get; set; }
        public int InventoryAlerts { get; set; }
    }

    public class HourlyStat
    {
        public int Hour { get; set; }
        public int Orders { get; set; }
    }

    public class TopSellingItem
    {
        public string Name { get; set; } = string.Empty;
        public int Sales { get; set; }
        public decimal Revenue { get; set; }
    }

    public class RecentActivityEntry
    {
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Time { get; set; }
    }
}
