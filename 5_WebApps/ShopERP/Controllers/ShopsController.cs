using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.ShopERP.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    /// <summary>
    /// API surface for shop management operations.
    /// Hosted in ShopERP so that KhachLink and other edge clients can access shop data
    /// without directly referencing CoreHub infrastructure.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ShopsController(ShopERPDbContext dbContext, ILogger<ShopsController> logger) : ControllerBase
    {
        private readonly ShopERPDbContext _dbContext = dbContext;
        private readonly ILogger<ShopsController> _logger = logger;

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<Shop>> GetShop(Guid id)
        {
            try
            {
                Shop? shop = await _dbContext.Shops.FindAsync(id);
                return shop == null ? NotFound() : Ok(shop);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shop {ShopId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        [Authorize(Policy = "RequireTenantAccess")]
        public async Task<ActionResult<Shop>> CreateShop([FromBody] CreateShopRequest request)
        {
            try
            {
                var shop = new Shop(
                    new TenantId(request.TenantId),
                    request.Name,
                    request.Address,
                    request.Phone,
                    request.Email);

                _ = _dbContext.Shops.Add(shop);
                _ = await _dbContext.SaveChangesAsync();

                return CreatedAtAction(nameof(GetShop), new { id = shop.Id }, shop);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shop");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "RequireTenantAccess")]
        public async Task<ActionResult<Shop>> UpdateShop(Guid id, [FromBody] UpdateShopRequest request)
        {
            try
            {
                Shop? shop = await _dbContext.Shops.FindAsync(id);
                if (shop == null)
                {
                    return NotFound();
                }

                shop.UpdateShopDetails(request.Name, request.Address, request.Phone, request.Email, shop.IsActive);
                _ = await _dbContext.SaveChangesAsync();

                return Ok(shop);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shop {ShopId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "RequireTenantAccess")]
        public async Task<ActionResult> DeleteShop(Guid id)
        {
            try
            {
                Shop? shop = await _dbContext.Shops.FindAsync(id);
                if (shop == null)
                {
                    return NotFound();
                }

                _dbContext.Shops.Remove(shop);
                _ = await _dbContext.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shop {ShopId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id:guid}/orders")]
        [AllowAnonymous]
        public async Task<ActionResult<List<Order>>> GetShopOrders(Guid id)
        {
            try
            {
                List<Order> orders = await _dbContext.Orders
                    .Where(o => o.TenantId == new TenantId(id))
                    .ToListAsync();
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders for shop {ShopId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("statistics")]
        [AllowAnonymous]
        public async Task<ActionResult> GetShopStatistics()
        {
            try
            {
                int totalShops = await _dbContext.Shops.CountAsync();
                int activeShops = await _dbContext.Shops.CountAsync(s => !s.IsDeleted);
                return Ok(new { totalShops, activeShops });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shop statistics");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<List<Shop>>> SearchShops([FromQuery] string? name)
        {
            try
            {
                IQueryable<Shop> query = _dbContext.Shops.Where(s => !s.IsDeleted);
                if (!string.IsNullOrEmpty(name))
                {
                    query = query.Where(s => s.Name.Contains(name));
                }

                List<Shop> shops = await query.ToListAsync();
                return Ok(shops);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching shops");
                return StatusCode(500, "Internal server error");
            }
        }

        // W17-T5: Store Finder — find shops near a GPS location
        [HttpGet("nearby")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ShopNearbyDto>>> GetNearbyShops(
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] double radiusKm = 10.0)
        {
            try
            {
                IQueryable<Shop> query = _dbContext.Shops.Where(s => !s.IsDeleted && s.IsActive);

                if (lat.HasValue && lng.HasValue)
                {
                    // Filter only shops that have coordinates, then sort by distance client-side
                    // (SQLite doesn't support spatial queries natively)
                    query = query.Where(s => s.Latitude != null && s.Longitude != null);
                }

                List<Shop> shops = await query.ToListAsync();

                IEnumerable<ShopNearbyDto> result = shops.Select(s => new ShopNearbyDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Address = s.Address,
                    Phone = s.Phone,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    DistanceKm = lat.HasValue && lng.HasValue && s.Latitude.HasValue && s.Longitude.HasValue
                        ? CalculateDistanceKm(lat.Value, lng.Value, s.Latitude.Value, s.Longitude.Value)
                        : null
                });

                if (lat.HasValue && lng.HasValue)
                {
                    result = result
                        .Where(s => s.DistanceKm == null || s.DistanceKm <= radiusKm)
                        .OrderBy(s => s.DistanceKm);
                }

                return Ok(result.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby shops");
                return StatusCode(500, "Internal server error");
            }
        }

        // Haversine formula for distance in km
        private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }

    public class ShopNearbyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? DistanceKm { get; set; }
    }

    public class CreateShopRequest
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class UpdateShopRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
