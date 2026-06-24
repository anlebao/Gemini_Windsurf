using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// API surface for dashboard metrics exposed to KhachLink via Gateway.
    /// Business logic remains in CoreHub DashboardService; this controller is a thin adapter.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController(
        IDashboardService dashboardService,
        ILogger<DashboardController> logger) : ControllerBase
    {
        private readonly IDashboardService _dashboardService = dashboardService;
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
    }
}
