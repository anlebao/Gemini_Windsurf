using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Filters;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// VALCN v2.0 Phase 7 — Network Dashboard API (investor-facing cross-tenant metrics).
/// SystemAdmin-only. Called by ShopERP admin UI via NetworkDashboardHttpService.
///
/// Auth: [InternalApiKey] (ShopERP → Gateway service-to-service pattern, same as LoyaltyBudgetController).
/// Access control at ShopERP page level: [Authorize(Policy = "SystemAdmin")] on NetworkDashboard.razor.
/// </summary>
[ApiController]
[Route("api/internal/network-dashboard")]
[AllowAnonymous]
[InternalApiKey]
public class NetworkDashboardController(
    INetworkDashboardService dashboardService,
    ILogger<NetworkDashboardController> logger) : ControllerBase
{
    private readonly INetworkDashboardService _dashboardService = dashboardService;
    private readonly ILogger<NetworkDashboardController> _logger = logger;

    /// <summary>
    /// GET /api/internal/network-dashboard?from=...&amp;to=...
    /// Returns 8 cross-tenant aggregate metrics. Default range: last 30 days.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<NetworkDashboardMetrics>> GetMetrics(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        try
        {
            var startDate = from ?? DateTime.UtcNow.AddDays(-30);
            var endDate = to ?? DateTime.UtcNow;
            var metrics = await _dashboardService.GetMetricsAsync(startDate, endDate, ct);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NetworkDashboard: failed to get metrics");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
