using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// W17-T5: Gateway forward controller for Shop/Store Finder endpoints.
    /// KhachLink calls this; YARP routes to ShopERP via shoperp-cluster when prefixed.
    /// GET endpoints: AllowAnonymous (customer-facing Store Finder).
    /// POST/PUT/DELETE: SystemAdmin only (admin operations from ShopERP admin UI).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ShopsController(IHttpClientFactory httpClientFactory, ILogger<ShopsController> logger) : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<ShopsController> _logger = logger;

        [HttpGet("nearby")]
        [AllowAnonymous]
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
        [AllowAnonymous]
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
        [AllowAnonymous]
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

        // ===== Admin endpoints: SystemAdmin only =====

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetShop(Guid id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var response = await client.GetAsync($"/api/shops/{id}");
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetShop to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("statistics")]
        [AllowAnonymous]
        public async Task<IActionResult> GetShopStatistics()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var response = await client.GetAsync("/api/shops/statistics");
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding GetShopStatistics to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> CreateShop([FromBody] object request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                // Forward Authorization header so ShopERP can authenticate the request
                var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
                }

                var response = await client.PostAsJsonAsync("/api/shops", request);
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding CreateShop to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> UpdateShop(Guid id, [FromBody] object request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
                }

                var response = await client.PutAsJsonAsync($"/api/shops/{id}", request);
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding UpdateShop to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "SystemAdmin")]
        public async Task<IActionResult> DeleteShop(Guid id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
                }

                var response = await client.DeleteAsync($"/api/shops/{id}");
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding DeleteShop to ShopERP");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
