using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// CC-S0-T3 (Sprint 0.5): Device fingerprint registration endpoint.
    /// Gateway-native (NOT forwarded to ShopERP) — DeviceRegistration is a community entity
    /// on Gateway PG (v1.3: community entities PG ONLY). DeviceRegistrationService is registered
    /// in Gateway DI and uses IVanAnDbContext → VanAnDbContext (PostgreSQL).
    ///
    /// Auth: X-Customer-Token header (validated by forwarding to ShopERP /api/customer-identity/me).
    /// KhachLink calls window.fingerprint.collect() after login success, then POSTs here.
    /// </summary>
    [ApiController]
    [Route("api/customer-identity/device")]
    [AllowAnonymous]
    public class DeviceRegistrationController(
        IDeviceRegistrationService deviceRegistrationService,
        IHttpClientFactory httpClientFactory,
        ILogger<DeviceRegistrationController> logger) : ControllerBase
    {
        private readonly IDeviceRegistrationService _deviceRegistrationService = deviceRegistrationService;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<DeviceRegistrationController> _logger = logger;

        /// <summary>
        /// Register a device fingerprint for the authenticated customer.
        /// Validates X-Customer-Token via ShopERP /me, then calls DeviceRegistrationService.
        /// Enforces max 3 active devices per customer (DeviceRegistrationService).
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
        {
            // 1. Validate X-Customer-Token by forwarding to ShopERP /me
            if (!Request.Headers.TryGetValue("X-Customer-Token", out var token) || string.IsNullOrEmpty(token))
                return Unauthorized(new { error = "X-Customer-Token header is required." });

            var (status, customerId) = await CustomerTokenValidationHelper.ValidateAsync(
                _httpClientFactory, token.ToString(), HttpContext.RequestAborted);

            if (status == CustomerTokenValidationStatus.InvalidToken)
                return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });
            if (status != CustomerTokenValidationStatus.Valid || customerId == null)
                return StatusCode(503, new { error = "Dịch vụ đang bảo trì, vui lòng thử lại sau." });

            // 2. Validate request
            if (string.IsNullOrWhiteSpace(request.FingerprintHash))
                return BadRequest(new { error = "FingerprintHash không được để trống." });
            if (string.IsNullOrWhiteSpace(request.DeviceToken))
                return BadRequest(new { error = "DeviceToken không được để trống." });

            // 3. Register device via DeviceRegistrationService (enforces max 3 active)
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = Request.Headers.UserAgent.ToString() ?? request.UserAgent ?? "";
                var platform = request.Platform ?? "";

                var result = await _deviceRegistrationService.RegisterDeviceAsync(
                    customerId.Value,
                    request.DeviceToken,
                    request.FingerprintHash,
                    request.FingerprintSignals ?? "{}",
                    userAgent,
                    platform,
                    ipAddress);

                _logger.LogInformation(
                    "Device {DeviceId} registered for Customer={CustomerId} (active={IsActive}, fraudFlag={HasFraudFlag})",
                    result.DeviceRegistration.Id, customerId,
                    result.DeviceRegistration.IsActive, result.FraudFlag != null);

                return Ok(new RegisterDeviceResponse
                {
                    DeviceId = result.DeviceRegistration.Id,
                    IsActive = result.DeviceRegistration.IsActive,
                    FraudFlagRaised = result.FraudFlag != null,
                    Message = result.FraudFlag != null
                        ? "Device registered as inactive (max 3 active devices exceeded). Admin review required."
                        : "Device registered successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering device for Customer={CustomerId}", customerId);
                return StatusCode(500, new { error = "Lỗi đăng ký device." });
            }
        }

        // DTOs
        public class RegisterDeviceRequest
        {
            public string DeviceToken { get; set; } = string.Empty;
            public string FingerprintHash { get; set; } = string.Empty;
            public string? FingerprintSignals { get; set; }
            public string? UserAgent { get; set; }
            public string? Platform { get; set; }
        }

        public class RegisterDeviceResponse
        {
            public Guid DeviceId { get; set; }
            public bool IsActive { get; set; }
            public bool FraudFlagRaised { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}
