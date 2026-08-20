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

        /// <summary>
        /// #130-fix3 (2026-08-18, Bug 1): Extract JSON payload from URL format.
        /// QR codes now wrap JSON in URL: https://app.khachvip.online/qr/claim?data={base64(json)}
        /// This method extracts the JSON from the URL so hash matching works correctly.
        /// Returns the original payload if it's not a URL (backward compat with raw JSON).
        /// </summary>
        private static string ExtractPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return payload;
            if (!payload.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return payload;
            try
            {
                var uri = new Uri(payload);
                var query = uri.Query.TrimStart('?');
                foreach (var pair in query.Split('&'))
                {
                    var eq = pair.IndexOf('=');
                    if (eq > 0 && pair[..eq] == "data")
                    {
                        var base64 = Uri.UnescapeDataString(pair[(eq + 1)..]);
                        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                    }
                }
            }
            catch { }
            return payload;
        }

        // === Guard endpoints (require Guard role) ===

        /// <summary>#130: Get photo compression config (anonymous — only returns 3 numbers).
        ///  Browser JS fetches this on page load to know max dimension, quality, max size. */
        [HttpGet("photo-config")]
        [AllowAnonymous]
        public IActionResult GetPhotoConfig()
        {
            var maxDimension = _configuration.GetValue<int>("Guard:PhotoMaxDimension", 1024);
            var jpegQuality = _configuration.GetValue<double>("Guard:PhotoJpegQuality", 0.7);
            var maxSizeKB = _configuration.GetValue<int>("Guard:MaxPhotoSizeKB", 100);
            return Ok(new { maxDimension, jpegQuality, maxSizeKB });
        }

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

        /// <summary>
        /// #130: Upload a single photo server-side (Gateway → R2). Replaces direct browser→R2
        /// presigned URL upload which fails without R2 CORS config. Browser sends base64 photo
        /// to Gateway via HTTP fetch (Gateway CORS already configured for app2.khachvip.online).
        /// Gateway uploads to R2 server-side — no CORS needed on R2 bucket.
        /// </summary>
        [HttpPost("upload-photo")]
        [Authorize(Roles = "Guard", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UploadPhoto([FromBody] UploadPhotoRequest? request, CancellationToken ct)
        {
            if (!IsFeatureEnabled) return FeatureDisabled();
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized(new { error = "Tenant ID not found in token." });

            if (string.IsNullOrWhiteSpace(request?.Base64Data))
                return BadRequest(new { error = "Photo data is required." });
            if (request.Slot != "plate" && request.Slot != "customer")
                return BadRequest(new { error = "Slot must be 'plate' or 'customer'." });

            // #130: Server-side size validation — reject if photo > configured max size.
            // Base64 ~1.37x binary size. Configurable via Guard:MaxPhotoSizeKB (default 100KB).
            var maxSizeKB = _configuration.GetValue<int>("Guard:MaxPhotoSizeKB", 100);
            var maxBase64Length = maxSizeKB * 1024 * 2; // generous — base64 + JSON overhead
            if (request.Base64Data.Length > maxBase64Length)
                return StatusCode(413, new { error = $"Ảnh quá lớn ({request.Base64Data.Length / 1024}KB base64). Tối đa {maxSizeKB}KB sau nén." });

            var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "image/jpeg" : request.ContentType;
            var key = _guardService.GeneratePhotoKey(tenantId, request.Slot);
            try
            {
                var ok = await _guardService.UploadPhotoAsync(key, request.Base64Data, contentType);
                if (!ok)
                    return StatusCode(500, new { error = "Upload ảnh lên R2 thất bại — HTTP status không OK." });

                return Ok(new { key });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "R2 upload failed for key {Key} (slot {Slot}, {Size} bytes base64)",
                    key, request.Slot, request.Base64Data.Length);
                return StatusCode(500, new { error = $"R2 upload lỗi: {ex.Message}" });
            }
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

            // PHASE-1: PlateNumber optional — photo is primary verifier, plate is optional metadata for stats
            if (string.IsNullOrWhiteSpace(request.PlatePhotoKey))
                return BadRequest(new { error = "Plate photo key is required." });
            // #130: Ảnh khách là TÙY CHỌN — không còn required. Client có thể gửi null.

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
                // #130-fix3: Extract JSON from URL wrapper if needed (Zalo/external scanner compat)
                var payload = ExtractPayload(request.QrPayload);
                var result = await _guardService.VerifyAsync(tenantId, guardId, payload);
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
            // #130-fix3: Extract JSON from URL wrapper if needed (Zalo/external scanner compat)
            Guid tenantId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(request!.QrPayload))
            {
                var extractedPayload = ExtractPayload(request.QrPayload);
                tenantId = ExtractTenantIdFromPayload(extractedPayload);
                // Use extracted payload for downstream lookup (hash matching)
                if (extractedPayload != request.QrPayload)
                    request = request with { QrPayload = extractedPayload };

                // #130-fix2 (2026-08-18): {sc,sid} payload (PrintTicket format) has no "tn" field.
                // Previous code rejected here → service fallback TryLookupByAlternativePayloadAsync
                // was unreachable → KhachLink app could not claim printed-ticket QR codes.
                // Resolve tenantId by looking up the session by sid, then proceed to ClaimAsync.
                if (tenantId == Guid.Empty)
                {
                    tenantId = await ResolveTenantIdFromSidPayloadAsync(request.QrPayload);
                }
            }

            // Issue #147: Short code claim — customer enters 6-digit code (no QR payload).
            // Short code has no tenantId embedded → resolve tenantId from short code lookup.
            // Short codes are unique per tenant per day; use the most recent matching session.
            if (tenantId == Guid.Empty && !string.IsNullOrWhiteSpace(request.ShortCode))
            {
                tenantId = await _guardService.GetTenantIdByShortCodeAsync(request.ShortCode);
            }

            // If still no tenantId, reject — ClaimAsync requires tenant scoping for security.
            if (tenantId == Guid.Empty)
                return BadRequest(new { error = "Could not determine tenant from QR code or short code. Please check your code and try again." });

            try
            {
                _logger.LogInformation("Claim: tenant={TenantId}, customer={CustomerId}, hasQrPayload={HasQr}, hasShortCode={HasSc}, qrPayloadLen={QrLen}",
                    tenantId, customerId, !string.IsNullOrWhiteSpace(request.QrPayload), !string.IsNullOrWhiteSpace(request.ShortCode),
                    request.QrPayload?.Length ?? 0);
                var result = await _guardService.ClaimAsync(tenantId, customerId!.Value, request);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("Claim KeyNotFound: tenant={TenantId}, customer={CustomerId}, error={Error}", tenantId, customerId, ex.Message);
                return NotFound(new { error = "QR session not found. Please check your QR code or short code." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Claim InvalidOperation: tenant={TenantId}, customer={CustomerId}, error={Error}", tenantId, customerId, ex.Message);
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
            catch (HttpRequestException ex)
            {
                // Fail-closed: if ShopERP is unreachable, the token cannot be validated → 401.
                // Returning 500 would leak infrastructure status; 401 is the secure default.
                _logger.LogWarning(ex, "ShopERP unreachable while validating customer token for guard endpoint");
                return (null, Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." }));
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

        /// <summary>
        /// #130-fix2 (2026-08-18): Resolve tenantId from a {sc,sid} QR payload (PrintTicket format).
        /// PrintTicket.razor generates QR as {"sc":"<shortCode>","sid":"<sessionId>"} — no "tn" field.
        /// ExtractTenantIdFromPayload returns Guid.Empty for this format. This helper parses "sid",
        /// looks up the session without tenant filter, and returns session.TenantId.
        /// Returns Guid.Empty if payload is not {sc,sid} format or session not found.
        /// </summary>
        private async Task<Guid> ResolveTenantIdFromSidPayloadAsync(string payload)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("sid", out var sidEl) && sidEl.TryGetGuid(out var sessionId))
                {
                    return await _guardService.GetTenantIdBySessionIdAsync(sessionId);
                }
            }
            catch
            {
                // Ignore parse errors — payload may be in another format
            }
            return Guid.Empty;
        }

        // === Request DTOs (controller-specific) ===

        public record PresignUploadRequest(string? ContentType);

        /// <summary>#130: Upload photo server-side (Gateway → R2, no CORS needed).</summary>
        public record UploadPhotoRequest(string Slot, string Base64Data, string? ContentType);

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
