using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using VietQrServiceConcrete = VanAn.Shared.Services.VietQrService;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    // KhachLink (anonymous customer app) calls this endpoint to generate VietQR URLs.
    // QR generation only builds a public img.vietqr.io URL — no tenant data accessed.
    [AllowAnonymous]
    public class VietQrController(IVietQrService vietQrService, ILogger<VietQrController> logger) : ControllerBase
    {
        private readonly IVietQrService _vietQrService = vietQrService;
        private readonly ILogger<VietQrController> _logger = logger;

        [HttpPost("generate")]
        public async Task<ActionResult<VietQrResponse>> GenerateQrCode([FromBody] VietQrRequest request)
        {
            try
            {
                _logger.LogInformation("Received VietQR generation request for order: {OrderDescription}",
                    request.OrderDescription);

                Shared.Domain.VietQrResponse response = await _vietQrService.GenerateQrCodeAsync(request);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid VietQR request: {Error}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating VietQR code");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("validate-bank")]
        public async Task<ActionResult<bool>> ValidateBankConfig([FromBody] BankConfig config)
        {
            try
            {
                bool isValid = await _vietQrService.ValidateBankConfigAsync(config);
                return Ok(isValid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating bank config");
                return StatusCode(500, false);
            }
        }

        [HttpGet("supported-banks")]
        public ActionResult<IEnumerable<object>> GetSupportedBanks()
        {
            // Use the shared single source of truth from VietQrService.SupportedBanks.
            // Returns the same shape (Id, Name, Logo) as before extraction.
            return Ok(VietQrService.SupportedBanks.Select(b => new
            {
                b.Id,
                b.Name,
                b.Logo
            }));
        }
    }
}
