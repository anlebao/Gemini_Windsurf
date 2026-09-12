using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// CC-S9: Device Registration admin endpoints.
    /// List, deactivate, verify, update risk score, query by fingerprint.
    /// Auth: SystemAdmin Bearer JWT (platform-level, cross-tenant).
    /// </summary>
    [ApiController]
    [Route("api/admin/device-registrations")]
    public class DeviceRegistrationAdminController(
        IDeviceRegistrationService deviceRegistrationService,
        ILogger<DeviceRegistrationAdminController> logger) : ControllerBase
    {
        private readonly IDeviceRegistrationService _deviceRegistrationService = deviceRegistrationService;
        private readonly ILogger<DeviceRegistrationAdminController> _logger = logger;

        /// <summary>
        /// GET /api/admin/device-registrations?page=1&pageSize=20&customerId=&fingerprintHash=&isActive=
        /// List devices with optional filters. Cross-tenant.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ListDevices(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? customerId = null,
            [FromQuery] string? fingerprintHash = null,
            [FromQuery] bool? isActive = null)
        {
            var result = await _deviceRegistrationService.ListDevicesAsync(page, pageSize, customerId, fingerprintHash, isActive);
            return Ok(result);
        }

        /// <summary>
        /// GET /api/admin/device-registrations/{id}
        /// Get a single device by ID. Cross-tenant.
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetDevice(Guid id)
        {
            var device = await _deviceRegistrationService.GetDeviceByIdAsync(id);
            if (device == null)
                return NotFound(new { error = $"Device {id} not found." });
            return Ok(device);
        }

        /// <summary>
        /// POST /api/admin/device-registrations/{id}/deactivate
        /// Deactivate a device (IsActive = false — customer logged out of that device).
        /// </summary>
        [HttpPost("{id:guid}/deactivate")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeactivateDevice(Guid id)
        {
            var ok = await _deviceRegistrationService.DeactivateDeviceAsync(id);
            if (!ok)
                return NotFound(new { error = $"Device {id} not found." });

            _logger.LogInformation("Device {DeviceId} deactivated by admin {AdminId}", id, GetAdminUserId());
            return Ok(new { message = "Device deactivated." });
        }

        /// <summary>
        /// POST /api/admin/device-registrations/{id}/verify
        /// Verify a device (IsVerified = true — whitelist, RiskScore reduced).
        /// </summary>
        [HttpPost("{id:guid}/verify")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> VerifyDevice(Guid id)
        {
            var ok = await _deviceRegistrationService.VerifyDeviceAsync(id);
            if (!ok)
                return NotFound(new { error = $"Device {id} not found." });

            _logger.LogInformation("Device {DeviceId} verified by admin {AdminId}", id, GetAdminUserId());
            return Ok(new { message = "Device verified (whitelisted)." });
        }

        /// <summary>
        /// POST /api/admin/device-registrations/{id}/risk-score
        /// Update device-level risk score.
        /// </summary>
        [HttpPost("{id:guid}/risk-score")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateRiskScore(Guid id, [FromBody] UpdateRiskScoreRequest request)
        {
            if (request.Score < 0 || request.Score > 100)
                return BadRequest(new { error = "Score must be between 0 and 100." });

            var ok = await _deviceRegistrationService.UpdateRiskScoreAsync(id, request.Score);
            if (!ok)
                return NotFound(new { error = $"Device {id} not found." });

            _logger.LogInformation("Device {DeviceId} risk score updated to {Score} by admin {AdminId}", id, request.Score, GetAdminUserId());
            return Ok(new { message = "Risk score updated." });
        }

        /// <summary>
        /// GET /api/admin/device-registrations/by-fingerprint/{fingerprintHash}
        /// Query devices by fingerprint hash (self-deal detection — who else uses this fingerprint?).
        /// </summary>
        [HttpGet("by-fingerprint/{fingerprintHash}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetByFingerprint(string fingerprintHash)
        {
            if (string.IsNullOrWhiteSpace(fingerprintHash))
                return BadRequest(new { error = "FingerprintHash is required." });

            var devices = await _deviceRegistrationService.GetDevicesByFingerprintAsync(fingerprintHash);
            return Ok(devices);
        }

        private Guid GetAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }
    }

    public class UpdateRiskScoreRequest
    {
        public int Score { get; set; }
    }
}
