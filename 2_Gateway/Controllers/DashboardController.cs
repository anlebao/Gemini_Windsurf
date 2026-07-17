using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// FIX-BATCH-4: Gateway forward for dashboard shop-metrics endpoint.
    /// KhachLink RealTimeDashboard polls this endpoint (replaces SignalR DashboardHub).
    /// W12-G7: Class-level [Authorize] (auth-on-by-default); public forwarding endpoint
    /// opts out via method-level [AllowAnonymous]. Mirrors ShopERP DashboardController pattern.
    /// </summary>
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardController(IHttpClientFactory httpClientFactory, ILogger<DashboardController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<DashboardController> _logger = logger;

        /// <summary>
        /// Forward GET /api/dashboard/shop-metrics/{shopId} → ShopERP. Returns ShopDashboardMetrics JSON.
        /// Public: KhachLink PWA polls this without JWT (FIX-BATCH-4 forwarding contract).
        /// </summary>
        [HttpGet("shop-metrics/{shopId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetShopMetrics(Guid shopId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var response = await client.GetAsync($"/api/dashboard/shop-metrics/{shopId}");
                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = content,
                    ContentType = contentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetShopMetrics to ShopERP for ShopId: {ShopId}", shopId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
