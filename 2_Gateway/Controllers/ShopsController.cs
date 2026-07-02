using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// W17-T5: Gateway forward controller for Shop/Store Finder endpoints.
    /// KhachLink calls this; YARP routes to ShopERP via shoperp-cluster when prefixed.
    /// This controller provides AllowAnonymous access for the customer-facing Store Finder.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class ShopsController(IHttpClientFactory httpClientFactory, ILogger<ShopsController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<ShopsController> _logger = logger;

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearbyShops(
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] double radiusKm = 10.0)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var url = $"/api/shops/nearby?lat={lat}&lng={lng}&radiusKm={radiusKm}";
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetNearbyShops to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchShops([FromQuery] string? name)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var url = $"/api/shops/search?name={Uri.EscapeDataString(name ?? "")}";
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding SearchShops to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // ShopConfig Product→Tenant Refactor (Phase 2): forward by-tenant lookup to ShopERP.
        // KhachLink calls this with TenantId derived from products to load real Shop data.
        [HttpGet("by-tenant/{tenantId:guid}")]
        public async Task<IActionResult> GetShopByTenant(Guid tenantId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var url = $"/api/shops/by-tenant/{tenantId}";
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetShopByTenant to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
