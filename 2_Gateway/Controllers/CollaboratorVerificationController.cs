using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// CC-S6-T5: Collaborator SMS OTP verification + deposit wallet endpoints.
    /// Admin endpoints (settings): SystemAdmin Bearer JWT.
    /// Collaborator endpoints (init/verify/deposit): X-Customer-Token header (same as CommunityController).
    /// </summary>
    [ApiController]
    [Route("api")]
    public class CollaboratorVerificationController(
        ICollaboratorVerificationService verificationService,
        IVanAnDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<CollaboratorVerificationController> logger) : ControllerBase
    {
        private readonly ICollaboratorVerificationService _verificationService = verificationService;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<CollaboratorVerificationController> _logger = logger;

        // === Admin endpoints (SystemAdmin JWT) ===

        /// <summary>
        /// GET /api/admin/collaborator-verification/settings
        /// Returns current toggle state + fee + min deposit.
        /// </summary>
        [HttpGet("admin/collaborator-verification/settings")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _verificationService.GetSettingsAsync();
            return Ok(settings);
        }

        /// <summary>
        /// POST /api/admin/collaborator-verification/settings
        /// Update toggle + fee + min deposit. SystemAdmin only.
        /// </summary>
        [HttpPost("admin/collaborator-verification/settings")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateCollaboratorSettingsRequest request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });
            if (request.FeePerVerification < 0)
                return BadRequest(new { error = "FeePerVerification cannot be negative." });
            if (request.MinDeposit < 0)
                return BadRequest(new { error = "MinDeposit cannot be negative." });

            var adminId = GetAdminUserId();
            await _verificationService.SetSettingsAsync(request.Enabled, request.FeePerVerification, request.MinDeposit, adminId);

            _logger.LogInformation("Collaborator verification settings updated: Enabled={Enabled} Fee={Fee} MinDeposit={MinDeposit} by {AdminId}",
                request.Enabled, request.FeePerVerification, request.MinDeposit, adminId);

            return Ok(new { enabled = request.Enabled, feePerVerification = request.FeePerVerification, minDeposit = request.MinDeposit });
        }

        // === Collaborator endpoints (X-Customer-Token) ===

        /// <summary>
        /// POST /api/collaborator-verification/init
        /// Initiate SMS OTP verification. Checks toggle + deposit balance → sends OTP → deducts fee.
        /// </summary>
        [HttpPost("collaborator-verification/init")]
        public async Task<IActionResult> InitVerification([FromBody] InitVerificationRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || string.IsNullOrWhiteSpace(body.PhoneNumber))
                return BadRequest(new { error = "PhoneNumber is required." });

            try
            {
                var result = await _verificationService.InitVerificationAsync(customerId.Value, body.PhoneNumber);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/collaborator-verification/verify
        /// Verify OTP code. On success, marks CommunityRole.IsPhoneVerified = true.
        /// </summary>
        [HttpPost("collaborator-verification/verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || string.IsNullOrWhiteSpace(body.OtpCode))
                return BadRequest(new { error = "OtpCode is required." });

            try
            {
                await _verificationService.VerifyOtpAsync(customerId.Value, body.OtpCode);
                return Ok(new { message = "Xác minh số điện thoại thành công." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/collaborator-verification/deposit
        /// Deposit money into collaborator's wallet (for SMS OTP fees).
        /// </summary>
        [HttpPost("collaborator-verification/deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositRequest body)
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            if (body == null || body.Amount <= 0)
                return BadRequest(new { error = "Amount must be positive." });

            try
            {
                await _verificationService.DepositAsync(customerId.Value, body.Amount);
                return Ok(new { message = "Nạp tiền thành công.", amount = body.Amount });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/collaborator-verification/status
        /// Check if SMS verification is required for the caller.
        /// </summary>
        [HttpGet("collaborator-verification/status")]
        public async Task<IActionResult> GetStatus()
        {
            var (customerId, error) = await ValidateTokenAndGetCustomerIdAsync();
            if (customerId == null) return error!;

            var required = await _verificationService.IsVerificationRequiredAsync(customerId.Value);
            var settings = await _verificationService.GetSettingsAsync();

            return Ok(new
            {
                verificationRequired = required,
                smsVerificationEnabled = settings.Enabled,
                feePerVerification = settings.FeePerVerification,
                minDeposit = settings.MinDeposit
            });
        }

        // === Private helpers ===

        private Guid GetAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Validate X-Customer-Token by forwarding to ShopERP /api/customer-identity/me.
        /// Returns CustomerId if valid, or an IActionResult error if invalid.
        /// </summary>
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

                var meContent = await meResp.Content.ReadFromJsonAsync<MeResponse>();
                if (meContent?.CustomerId == null || meContent.CustomerId == Guid.Empty)
                    return (null, Unauthorized(new { error = "Không tìm thấy khách hàng." }));

                return (meContent.CustomerId.Value, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating customer token for collaborator verification endpoint");
                return (null, StatusCode(500, new { error = "Lỗi xác thực token." }));
            }
        }

        private class MeResponse
        {
            public Guid? CustomerId { get; set; }
        }
    }

    // Request DTOs
    public class UpdateCollaboratorSettingsRequest
    {
        public bool Enabled { get; set; }
        public decimal FeePerVerification { get; set; }
        public decimal MinDeposit { get; set; }
    }

    public class InitVerificationRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class VerifyOtpRequest
    {
        public string OtpCode { get; set; } = string.Empty;
    }

    public class DepositRequest
    {
        public decimal Amount { get; set; }
    }
}
