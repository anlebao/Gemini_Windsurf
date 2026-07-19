using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Phase 2 (Multi-VPS Checkout): Admin API for managing ShopERP hosting instances.
    /// Platform-level CRUD + health check. SystemAdmin Bearer JWT only.
    /// </summary>
    [ApiController]
    [Route("api/v1/shop-instances")]
    public class ShopInstancesController(
        IShopInstanceService shopInstanceService,
        ILogger<ShopInstancesController> logger) : ControllerBase
    {
        private readonly IShopInstanceService _shopInstanceService = shopInstanceService;
        private readonly ILogger<ShopInstancesController> _logger = logger;

        /// <summary>Create a new ShopInstance.</summary>
        [HttpPost]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ShopInstanceDto>> Create(
            [FromBody] CreateShopInstanceRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var instance = await _shopInstanceService.CreateAsync(
                    request.BaseUrl, request.Label, request.MaxTenants, request.HealthCheckUrl, ct);
                var dto = await ToDtoAsync(instance, ct);
                return CreatedAtAction(nameof(GetById), new { id = instance.Id }, dto);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid ShopInstance create request");
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "ShopInstance conflict");
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>List all ShopInstances.</summary>
        [HttpGet]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<List<ShopInstanceDto>>> List(CancellationToken ct = default)
        {
            var instances = await _shopInstanceService.GetAllAsync(ct);
            var dtos = new List<ShopInstanceDto>();
            foreach (var i in instances)
                dtos.Add(await ToDtoAsync(i, ct));
            return Ok(dtos);
        }

        /// <summary>Get a ShopInstance by Id.</summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ShopInstanceDto>> GetById(Guid id, CancellationToken ct = default)
        {
            var instance = await _shopInstanceService.GetByIdAsync(id, ct);
            if (instance is null)
                return NotFound();
            return Ok(await ToDtoAsync(instance, ct));
        }

        /// <summary>Update label + maxTenants.</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateShopInstanceRequest request,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var updated = await _shopInstanceService.UpdateAsync(id, request.Label, request.MaxTenants, ct);
                if (!updated)
                    return NotFound();
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Activate a ShopInstance.</summary>
        [HttpPut("{id:guid}/activate")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Activate(Guid id, CancellationToken ct = default)
        {
            var result = await _shopInstanceService.SetActiveAsync(id, true, ct);
            return result ? NoContent() : NotFound();
        }

        /// <summary>Deactivate a ShopInstance.</summary>
        [HttpPut("{id:guid}/deactivate")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
        {
            var result = await _shopInstanceService.SetActiveAsync(id, false, ct);
            return result ? NoContent() : NotFound();
        }

        /// <summary>Trigger a health check probe.</summary>
        [HttpPost("{id:guid}/health-check")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ShopInstanceHealthResult>> HealthCheck(Guid id, CancellationToken ct = default)
        {
            try
            {
                var result = await _shopInstanceService.CheckHealthAsync(id, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        private async Task<ShopInstanceDto> ToDtoAsync(ShopInstance instance, CancellationToken ct)
        {
            int tenantCount = await _shopInstanceService.CountTenantsAsync(instance.Id, ct);
            return new ShopInstanceDto
            {
                Id = instance.Id,
                BaseUrl = instance.BaseUrl,
                Label = instance.Label,
                MaxTenants = instance.MaxTenants,
                IsActive = instance.IsActive,
                HealthCheckUrl = instance.HealthCheckUrl,
                LastHealthCheck = instance.LastHealthCheck,
                HealthStatus = instance.HealthStatus,
                TenantCount = tenantCount,
                CreatedAt = instance.CreatedAt
            };
        }
    }

    public sealed class CreateShopInstanceRequest
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int MaxTenants { get; set; } = 50;
        public string? HealthCheckUrl { get; set; }
    }

    public sealed class UpdateShopInstanceRequest
    {
        public string Label { get; set; } = string.Empty;
        public int MaxTenants { get; set; } = 50;
    }

    public sealed class ShopInstanceDto
    {
        public Guid Id { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int MaxTenants { get; set; }
        public bool IsActive { get; set; }
        public string? HealthCheckUrl { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public string HealthStatus { get; set; } = "Unknown";
        public int TenantCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
