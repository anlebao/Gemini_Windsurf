using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Public Store Finder endpoints — replaced ShopsController (Shop entity removed 2026-07-21).
    /// Queries Tenant.Settings.Latitude/Longitude for Store Finder functionality.
    /// All endpoints are anonymous (KhachLink customer app is unauthenticated).
    /// </summary>
    [ApiController]
    [Route("api/tenants")]
    public class TenantStoreController(
        IVanAnDbContext dbContext,
        ILogger<TenantStoreController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ILogger<TenantStoreController> _logger = logger;

        /// <summary>
        /// Get tenant store info (name, address, phone, lat/lng) by TenantId.
        /// Replaces GET /api/shops/by-tenant/{tenantId}.
        /// </summary>
        [HttpGet("{tenantId:guid}/store-info")]
        [AllowAnonymous]
        public async Task<ActionResult<TenantStoreDto>> GetStoreInfo(Guid tenantId)
        {
            try
            {
                var tenant = await _dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId));

                if (tenant == null)
                    return NotFound();

                return Ok(MapToStoreDto(tenant));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting store info for tenant {TenantId}", tenantId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Store Finder — find tenants near a GPS location.
        /// Replaces GET /api/shops/nearby.
        /// </summary>
        [HttpGet("nearby")]
        [AllowAnonymous]
        public async Task<ActionResult<List<TenantStoreDto>>> GetNearby(
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] double radiusKm = 10.0)
        {
            if (!lat.HasValue || !lng.HasValue)
                return BadRequest("lat and lng query parameters are required");

            try
            {
                var tenants = await _dbContext.Tenants
                    .AsNoTracking()
                    .Where(t => t.Status == TenantStatus.Active)
                    .ToListAsync();

                var nearby = tenants
                    .Where(t => t.Settings?.Latitude.HasValue == true && t.Settings?.Longitude.HasValue == true)
                    .Select(t => new { Tenant = t, Distance = HaversineKm(lat.Value, lng.Value, t.Settings!.Latitude!.Value, t.Settings!.Longitude!.Value) })
                    .Where(x => x.Distance <= radiusKm)
                    .OrderBy(x => x.Distance)
                    .Select(x => MapToStoreDto(x.Tenant))
                    .ToList();

                return Ok(nearby);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding nearby tenants");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Search tenants by name.
        /// Replaces GET /api/shops/search.
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<List<TenantStoreDto>>> Search([FromQuery] string? name)
        {
            try
            {
                var query = _dbContext.Tenants.AsNoTracking().Where(t => t.Status == TenantStatus.Active);
                if (!string.IsNullOrWhiteSpace(name))
                    query = query.Where(t => EF.Functions.ILike(t.Name, $"%{name}%"));

                var tenants = await query.Take(50).ToListAsync();
                return Ok(tenants.Select(MapToStoreDto).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tenants by name");
                return StatusCode(500, "Internal server error");
            }
        }

        private static TenantStoreDto MapToStoreDto(Tenant t) => new()
        {
            Id = t.Id.Value,
            Name = t.Name,
            Address = t.Settings?.Address ?? string.Empty,
            Phone = t.Settings?.ContactPhone ?? string.Empty,
            Email = t.Settings?.ContactEmail ?? string.Empty,
            Latitude = t.Settings?.Latitude,
            Longitude = t.Settings?.Longitude
        };

        private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371.0; // Earth radius km
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLng = (lng2 - lng1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }

    public record TenantStoreDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }
}
