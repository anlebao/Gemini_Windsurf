using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// CC-S4 (Sprint 4): Admin ProductReferralConfig CRUD — per-product commission rate + app-install bonus.
    /// Auth: SystemAdmin policy (JWT).
    /// Validation: CommissionRate 0.02-0.05, AppInstallBonus >= 0, ProductShortCode unique.
    /// </summary>
    [ApiController]
    [Route("api/admin/products")]
    [Authorize(Policy = "SystemAdmin")]
    public class ProductReferralConfigController(
        IProductReferralConfigService configService,
        ILogger<ProductReferralConfigController> logger) : ControllerBase
    {
        private readonly IProductReferralConfigService _configService = configService;
        private readonly ILogger<ProductReferralConfigController> _logger = logger;

        /// <summary>
        /// GET /api/admin/products/{productId}/referral-config
        /// </summary>
        [HttpGet("{productId}/referral-config")]
        public async Task<IActionResult> GetByProductId(Guid productId)
        {
            var config = await _configService.GetByProductIdAsync(productId);
            if (config == null)
                return NotFound(new { error = "Không tìm thấy cấu hình referral." });
            return Ok(config);
        }

        /// <summary>
        /// POST /api/admin/products/{productId}/referral-config
        /// </summary>
        [HttpPost("{productId}/referral-config")]
        public async Task<IActionResult> Create(Guid productId, [FromBody] CreateReferralConfigRequest body)
        {
            if (body == null)
                return BadRequest(new { error = "Body không được để trống." });

            try
            {
                var config = await _configService.CreateAsync(productId, body.CommissionRate, body.AppInstallBonus, body.ProductShortCode);
                return CreatedAtAction(nameof(GetByProductId), new { productId }, config);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating referral config for product {ProductId}", productId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// PUT /api/admin/products/{productId}/referral-config
        /// </summary>
        [HttpPut("{productId}/referral-config")]
        public async Task<IActionResult> Update(Guid productId, [FromBody] UpdateReferralConfigRequest body)
        {
            if (body == null)
                return BadRequest(new { error = "Body không được để trống." });

            try
            {
                var config = await _configService.UpdateAsync(productId, body.CommissionRate, body.AppInstallBonus, body.ProductShortCode, body.IsActive);
                return Ok(config);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating referral config for product {ProductId}", productId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// DELETE /api/admin/products/{productId}/referral-config (soft delete — IsActive=false)
        /// </summary>
        [HttpDelete("{productId}/referral-config")]
        public async Task<IActionResult> Deactivate(Guid productId)
        {
            try
            {
                await _configService.DeactivateAsync(productId);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating referral config for product {ProductId}", productId);
                return StatusCode(500, new { error = "Lỗi server." });
            }
        }

        /// <summary>
        /// GET /api/admin/products/referral-configs — list all configs (admin dashboard)
        /// </summary>
        [HttpGet("referral-configs")]
        public async Task<IActionResult> ListAll()
        {
            var configs = await _configService.ListAllAsync();
            return Ok(configs);
        }

        public class CreateReferralConfigRequest
        {
            public decimal CommissionRate { get; set; }
            public decimal AppInstallBonus { get; set; }
            public string? ProductShortCode { get; set; }
        }

        public class UpdateReferralConfigRequest
        {
            public decimal CommissionRate { get; set; }
            public decimal AppInstallBonus { get; set; }
            public string? ProductShortCode { get; set; }
            public bool IsActive { get; set; } = true;
        }
    }
}
