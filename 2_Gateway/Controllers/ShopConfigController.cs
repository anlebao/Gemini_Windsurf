using Microsoft.AspNetCore.Mvc;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ShopConfigController(IShopConfigService shopConfigService, ILogger<ShopConfigController> logger) : ControllerBase
    {
        private readonly IShopConfigService _shopConfigService = shopConfigService;
        private readonly ILogger<ShopConfigController> _logger = logger;

        [HttpGet("shops/{shopId}/config")]
        public ActionResult<ShopConfig> GetShopConfig(Guid shopId)
        {
            try
            {
                Task<ShopConfig?> config = _shopConfigService.GetShopConfigAsync(shopId);
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shop config for shop: {ShopId}", shopId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPut("shops/{shopId}/config")]
        public ActionResult<bool> UpdateShopConfig(Guid shopId, [FromBody] ShopConfig config)
        {
            try
            {
                if (config.ShopId != shopId)
                {
                    return BadRequest(new { error = "Shop ID mismatch" });
                }

                Task<ShopConfig> updated = _shopConfigService.UpdateShopConfigAsync(config);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shop config for shop: {ShopId}", shopId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("themes")]
        public ActionResult<IEnumerable<object>> GetAvailableThemes()
        {
            var themes = new[]
            {
                new
                {
                    Name = "cafe",
                    DisplayName = "Quán Cafe",
                    PrimaryColor = "#8B4513",
                    SecondaryColor = "#FFA500",
                    Description = "Theme cho quán cà phê và trà sữa"
                },
                new
                {
                    Name = "beauty",
                    DisplayName = "Spa & Beauty",
                    PrimaryColor = "#FF69B4",
                    SecondaryColor = "#FFFFFF",
                    Description = "Theme cho spa và salon làm đẹp"
                },
                new
                {
                    Name = "retail",
                    DisplayName = "Cửa hàng",
                    PrimaryColor = "#1E90FF",
                    SecondaryColor = "#32CD32",
                    Description = "Theme cho cửa hàng bán lẻ"
                }
            };

            return Ok(themes);
        }
    }
}
