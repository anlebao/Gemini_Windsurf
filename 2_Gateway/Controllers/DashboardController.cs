using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// FIX-BATCH-4: Gateway forward for dashboard shop-metrics endpoint.
    /// KhachLink RealTimeDashboard polls this endpoint (replaces SignalR DashboardHub).
    /// </summary>
    [ApiController]
    [Route("api/dashboard")]
    [AllowAnonymous]
    public class DashboardController(IHttpClientFactory httpClientFactory, ILogger<DashboardController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<DashboardController> _logger = logger;

        /// <summary>
        /// Forward GET /api/dashboard/shop-metrics/{shopId} → ShopERP. Returns ShopDashboardMetrics JSON.
        /// </summary>
        [HttpGet("shop-metrics/{shopId:guid}")]
        public async Task<IActionResult> GetShopMetrics(Guid shopId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var response = await client.GetAsync($"/api/dashboard/shop-metrics/{shopId}");
                var content = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetShopMetrics to ShopERP for ShopId: {ShopId}", shopId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
