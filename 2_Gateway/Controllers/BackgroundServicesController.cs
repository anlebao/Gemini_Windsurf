using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// REQ-1.2: Background service toggle API — SystemAdmin only.
    /// GET /api/admin/background-services — list all toggles
    /// PUT /api/admin/background-services/{serviceName} — { isEnabled: true/false }
    /// </summary>
    [ApiController]
    [Route("api/admin/background-services")]
    public class BackgroundServicesController(
        IBackgroundServiceToggleService toggleService,
        ILogger<BackgroundServicesController> logger) : ControllerBase
    {
        private readonly IBackgroundServiceToggleService _toggleService = toggleService;
        private readonly ILogger<BackgroundServicesController> _logger = logger;

        [HttpGet]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<IReadOnlyList<BackgroundServiceToggleDto>>> GetAll(CancellationToken ct)
        {
            var toggles = await _toggleService.GetAllAsync(ct);
            return Ok(toggles);
        }

        [HttpPut("{serviceName}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult> Update(string serviceName, [FromBody] UpdateToggleRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                return BadRequest(new { error = "Service name is required." });

            // Extract user ID from JWT for audit
            Guid updatedBy = Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value, out var uid) ? uid : Guid.Empty;

            try
            {
                await _toggleService.SetEnabledAsync(serviceName, request.IsEnabled, updatedBy, ct);
                _logger.LogInformation("Background service {ServiceName} toggled to {Enabled} by {UserId}", serviceName, request.IsEnabled, updatedBy);
                return Ok(new { serviceName, isEnabled = request.IsEnabled });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling background service {ServiceName}", serviceName);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public record UpdateToggleRequest(bool IsEnabled);
}
