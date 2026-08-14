using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Security.Claims;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// #126: Guard QR Verification API.
    /// Guard endpoints (issue/verify/checkout/flag/void/sessions) require Guard role JWT.
    /// Claim + my-sessions endpoints are anonymous (customer auth via X-Customer-Token — validated through ShopERP).
    /// Feature flag: Guard:QrVerifyEnabled (default false — graceful fallback to old hardcode page).
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/guard")]
    public class GuardController(
        IGuardService guardService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<GuardController> logger) : ControllerBase
    {
        private readonly IGuardService _guardService = guardService;
        private readonly IConfiguration _configuration = configuration;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<GuardController> _logger = logger;

        private bool IsFeatureEnabled =>
            _configuration.GetValue<bool>("Guard:QrVerifyEnabled");

        private Guid GetTenantId()
        {
            // Wave 1 Phase 2: Standardized claim name "tenant_id" (snake_case, OIDC standard)
            string? tenantClaim = User.FindFirst("tenant_id")?.Value
                ?? User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out Guid tenantId) ? tenantId : Guid.Empty;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                              ?? User.FindFirst("sub");
            return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId) ? userId : Guid.Empty;
        }

        private IActionResult FeatureDisabled()
        {
            _logger.LogWarning("Guard QR Verify feature is disabled (Guard:QrVerifyEnabled=false)");
            return StatusCode(503, new { error = "Guard QR Verify feature is disabled." });
        }

        // === Guard endpoints (require Guard role) ===

        /// <summary>Generate presigned PUT URLs for photo upload (plate + customer).</summary>
        [HttpPost("presign-upload")]
        [Authorize(Roles = "Guard", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> PresignUpload([FromBody] PresignUploadRequest? request, CancellationToken ct)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized(new { error = "Tenant ID not found in token." });

            var contentType = request?.ContentType ?? "image/jpeg";
            var result = await _guardService.PresignUploadAsync(tenantId, contentType);
            return Ok(result);
        }

        /// <summary>Issue a new QR session (guard creates QR with plate + customer photos).</summary>
        [HttpPost("issue")]
        [Authorize(Roles = "Guard", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Issue([FromBody] IssueRequest request, CancellationToken ct)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized(new { error = "Tenant ID not found in token." });
            var guardId = GetUserId();
            if (guardId == Guid.Empty) return Unauthorized(new { error = "User ID not found in token." });

            if (string.IsNullOrWhiteSpace(request.PlateNumber))
                return BadRequest(new { error = "Plate number is required." });
            if (string.IsNullOrWhiteSpace(request.PlatePhotoKey))
                return BadRequest(new { error = "Plate photo key is required." });
            if (string.IsNullOrWhiteSpace(request.CustomerPhotoKey))
                return BadRequest(new { error = "Customer photo key is required." });

            try
            {
                var result = await _guardService.IssueAsync(tenantId, guardId, request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error issuing QR session for plate {PlateNumber}", request.PlateNumber);
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Verify scanned QR (guard scans QR from KhachLink screen or paper ticket).</summary>
        [HttpPost("verify")]
        [Authorize(Roles = "Guard", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Verify([FromBody] VerifyRequest request, CancellationToken ct)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized(new { error = "Tenant ID not found in token." });
            var guardId = GetUserId();
            if (guardId == Guid.Empty) return Unauthorized(new { error = "User ID not found in token." });

            if (string.IsNullOrWhiteSpace(request.QrPayload))
                return BadRequest(new { error = "QR payload is required." });

            try
            {
                var result = await _guardService.VerifyAsync(tenantId, guardId, request.QrPayload);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "QR session not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying QR payload");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Check-out session (guard confirms match).</summary>
        [HttpPost("checkout/{sessionId:guid}")]
        [Authorize(Roles = "Guard", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Checkout(Guid sessionId, CancellationToken ct)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized(new { error = "Tenant ID not found in token." });
            var guardId = GetUserId();
            if (guardId == Guid.Empty) return Unauthorized(new { error = "User ID not found in token." });

            try
            {
                var result = await _guardService.CheckoutAsync(tenantId, guardId, sessionId);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "QR session not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>Flag session as suspicious (guard detects mismatch).</summary>
        [HttpPost("flag/{sessionId:guid}")]
        [Authorize(Roles = "Guard", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Flag(Guid sessionId, [FromBody] FlagRequest request, CancellationToken ct)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized(new { error = "Tenant ID not found in token." });
            var guardId = GetUserId();
            if (guardId == Guid.Empty) return Unauthorized(new { error = "User ID not found in token." });

            if (string.IsNullOrWhiteSpace(request?.Reason))
                return BadRequest(new { error = "Flag reason is required." });

            try
            {
                var result = await _guardService.FlagAsync(tenantId, guardId, sessionId, request.Reason);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "QR session not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>Void session (cancelled/expired).</summary>
        [HttpPost("void/{sessionId:guid}")]
        [Authorize(Roles = "Guard", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Void(Guid sessionId, CancellationToken ct)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized(new { error = "Tenant ID not found in token." });
            var guardId = GetUserId();
            if (guardId == Guid.Empty) return Unauthorized(new { error = "User ID not found in token." });

            try
            {
                var result = await _guardService.VoidAsync(tenantId, guardId, sessionId);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "QR session not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        /// <summary>Get today's sessions (paginated, optional status filter).</summary>
        [HttpGet("sessions/today")]
        [Authorize(Roles = "Guard", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetTodaySessions(
            [FromQuery] VehicleSessionStatus? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized(new { error = "Tenant ID not found in token." });

            var result = await _guardService.GetTodaySessionsAsync(tenantId, status, page, pageSize);
            return Ok(result);
        }

        /// <summary>Get session detail by ID (with presigned photo URLs).</summary>
        [HttpGet("sessions/{sessionId:guid}")]
        [Authorize(Roles = "Guard", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken ct)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized(new { error = "Tenant ID not found in token." });

            try
            {
                var result = await _guardService.GetSessionAsync(tenantId, sessionId);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "QR session not found." });
            }
        }

        // === Customer endpoints (anonymous — customer auth via X-Customer-Token, validated through ShopERP) ===

        /// <summary>Claim QR session by customer (Channel A/B/C→A migration).</summary>
        [HttpPost("claim")]
        [AllowAnonymous]
        public async Task<IActionResult> Claim([FromBody] ClaimRequest? request, CancellationToken ct)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();

            // R2 Sprint 4: validate X-Customer-Token → resolve customerId via ShopERP
            var (customerId, authError) = await ValidateTokenAndGetCustomerIdAsync();
            if (authError != null) return authError;

            if (string.IsNullOrWhiteSpace(request?.QrPayload) && string.IsNullOrWhiteSpace(request?.ShortCode))
                return BadRequest(new { error = "Either QR payload or short code is required." });

            // Extract tenantId from QR payload (JSON {"t":"...","tn":"<tenantId>"})
            Guid tenantId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(request!.QrPayload))
            {
                tenantId = ExtractTenantIdFromPayload(request.QrPayload);
            }

            // If no tenantId from payload, try short code lookup across tenants (not ideal but MVP)
            // For now, require tenantId in QR payload
            if (tenantId == Guid.Empty)
                return BadRequest(new { error = "Could not determine tenant from QR code. Please use short code with tenant context." });

            try
            {
                var result = await _guardService.ClaimAsync(tenantId, customerId!.Value, request);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { error = "QR session not found. Please check your QR code or short code." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error claiming QR session for customer {CustomerId}", customerId);
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Get session statuses for customer's claimed QR sessions (wallet sync — R2 Sprint 4).</summary>
        [HttpPost("my-sessions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMySessions([FromBody] MySessionsRequest? request, CancellationToken ct)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();

            var (customerId, authError) = await ValidateTokenAndGetCustomerIdAsync();
            if (authError != null) return authError;

            if (request?.SessionIds == null || request.SessionIds.Count == 0)
                return Ok(new { items = new List<SessionStatusResult>() });

            var statuses = await _guardService.GetSessionStatusesAsync(customerId!.Value, request.SessionIds);
            return Ok(new { items = statuses });
        }

        /// <summary>Validate X-Customer-Token by forwarding to ShopERP /api/customer-identity/me.</summary>
        private async Task<(Guid? CustomerId, IActionResult? Error)> ValidateTokenAndGetCustomerIdAsync()
        {
            if (!Request.Headers.TryGetValue("X-Customer-Token", out var token) || string.IsNullOrEmpty(token))
                return (null, Unauthorized(new { error = "X-Customer-Token header is required." }));

            try
            {
                var client = _httpClientFactory.CreateClient("shoperp");
                var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/customer-identity/me");
                meReq.Headers.Add("X-Customer-Token", token.ToString());

                var meResp = await client.SendAsync(meReq);
                if (!meResp.IsSuccessStatusCode)
                    return (null, Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." }));

                var meContent = await meResp.Content.ReadFromJsonAsync<MeResponse>(HttpContext.RequestAborted);
                if (meContent?.CustomerId == null || meContent.CustomerId == Guid.Empty)
                    return (null, Unauthorized(new { error = "Không tìm thấy khách hàng." }));

                return (meContent.CustomerId.Value, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating customer token for guard claim endpoint");
                return (null, StatusCode(500, new { error = "Lỗi xác thực token." }));
            }
        }

        private static Guid ExtractTenantIdFromPayload(string payload)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("tn", out var tnElement) &&
                    tnElement.TryGetGuid(out var tenantId))
                {
                    return tenantId;
                }
            }
            catch
            {
                // Ignore parse errors
            }
            return Guid.Empty;
        }

        // === Request DTOs (controller-specific) ===

        public record PresignUploadRequest(string? ContentType);

        public record VerifyRequest(string QrPayload);

        public record FlagRequest(string Reason);

        public record MySessionsRequest(List<Guid> SessionIds);

        // Response from ShopERP /api/customer-identity/me (subset)
        private class MeResponse
        {
            public Guid? CustomerId { get; set; }
        }
    }
}
