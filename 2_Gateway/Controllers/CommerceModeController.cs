using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Sprint 7 — Commerce mode admin endpoints. Get/set global + tenant override.
    /// Auth: SystemAdmin Bearer JWT (platform-level, cross-tenant).
    /// </summary>
    [ApiController]
    [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/admin/commerce-mode")]
    public class CommerceModeController(
        ICommerceModeService commerceModeService,
        ILogger<CommerceModeController> logger) : ControllerBase
    {
        private readonly ICommerceModeService _commerceModeService = commerceModeService;
        private readonly ILogger<CommerceModeController> _logger = logger;

        /// <summary>
        /// GET /api/admin/commerce-mode
        /// Returns global mode + default rates + all tenant overrides.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _commerceModeService.GetSettingsAsync();
            return Ok(settings);
        }

        /// <summary>
        /// POST /api/admin/commerce-mode/global
        /// Set global commerce mode + default rates. Affects future orders only.
        /// </summary>
        [HttpPost("global")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> SetGlobalMode([FromBody] SetGlobalModeRequest request)
        {
            if (!Enum.TryParse<CommerceMode>(request.Mode, ignoreCase: true, out var mode))
                return BadRequest(new { error = $"Invalid mode: {request.Mode}. Must be 'Marketplace' or 'Reseller'." });

            try
            {
                var adminId = GetAdminUserId();
                await _commerceModeService.SetGlobalModeAsync(mode, request.PlatformFeeRate, request.CommunityFundRate, request.DeliveryFee, adminId);

                _logger.LogInformation("Global commerce mode set to {Mode} by admin {AdminId}", mode, adminId);

                return Ok(new { mode = mode.ToString(), platformFeeRate = request.PlatformFeeRate, communityFundRate = request.CommunityFundRate, deliveryFee = request.DeliveryFee });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/commerce-mode/tenant/{tenantId}
        /// Set tenant override. Inherit = use global. Affects future orders only.
        /// </summary>
        [HttpPost("tenant/{tenantId}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> SetTenantOverride(Guid tenantId, [FromBody] SetTenantOverrideRequest request)
        {
            if (!Enum.TryParse<CommerceMode>(request.OverrideMode, ignoreCase: true, out var overrideMode))
                return BadRequest(new { error = $"Invalid mode: {request.OverrideMode}. Must be 'Inherit', 'Marketplace', or 'Reseller'." });

            try
            {
                var adminId = GetAdminUserId();
                await _commerceModeService.SetTenantOverrideAsync(tenantId, overrideMode, adminId);

                _logger.LogInformation("Tenant {TenantId} commerce mode override set to {Mode} by admin {AdminId}", tenantId, overrideMode, adminId);

                return Ok(new { tenantId, overrideMode = overrideMode.ToString() });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/admin/commerce-mode/resolve/{tenantId}
        /// Resolve effective mode for a tenant (override ≠ Inherit → override; else global).
        /// </summary>
        [HttpGet("resolve/{tenantId}")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ResolveMode(Guid tenantId)
        {
            var mode = await _commerceModeService.ResolveModeForTenantAsync(tenantId);
            return Ok(new { tenantId, resolvedMode = mode.ToString() });
        }

        /// <summary>
        /// Sprint 7 Q5: POST /api/admin/commerce-mode/confirm-external-payment
        /// Vạn An confirms external payment (non-COD Reseller — VietQR/card) for an order.
        /// Creates 5-split: ExternalPayment + Settlement + DeliveryFee + Commission? + PlatformFee + CommunityFund.
        /// Auth: SystemAdmin Bearer JWT (Vạn An staff confirms after VietQR webhook or manual check).
        /// </summary>
        [HttpPost("confirm-external-payment")]
        [Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ConfirmExternalPayment([FromBody] ConfirmExternalPaymentRequest body,
            [FromServices] IWalletService walletService)
        {
            if (body == null || body.OrderId == Guid.Empty)
                return BadRequest(new { error = "OrderId không hợp lệ." });
            if (body.Amount <= 0)
                return BadRequest(new { error = "Amount phải lớn hơn 0." });
            if (string.IsNullOrWhiteSpace(body.PaymentRef))
                return BadRequest(new { error = "PaymentRef không được để trống." });

            try
            {
                var tx = await walletService.ConfirmExternalPaymentAsync(body.OrderId, body.Amount, body.PaymentRef);
                return Ok(new { transactionId = tx.Id, balanceAfter = tx.BalanceAfter });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming external payment for order {OrderId}", body.OrderId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        private Guid GetAdminUserId()
        {
            var userIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }
    }

    // Request DTOs
    public class SetGlobalModeRequest
    {
        public string Mode { get; set; } = string.Empty;
        public decimal PlatformFeeRate { get; set; }
        public decimal CommunityFundRate { get; set; }
        public decimal DeliveryFee { get; set; }
    }

    public class SetTenantOverrideRequest
    {
        public string OverrideMode { get; set; } = string.Empty;
    }

    /// <summary>Sprint 7 Q5: External payment confirmation request (non-COD Reseller).</summary>
    public class ConfirmExternalPaymentRequest
    {
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentRef { get; set; } = string.Empty;
    }
}
