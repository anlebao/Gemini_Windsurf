using Microsoft.AspNetCore.Mvc;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
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
            var supportedBanks = new[]
            {
                new { Id = "970422", Name = "Vietcombank", Logo = "https://img.vietqr.io/bank/970422.png" },
                new { Id = "970436", Name = "VietinBank", Logo = "https://img.vietqr.io/bank/970436.png" },
                new { Id = "970418", Name = "Agribank", Logo = "https://img.vietqr.io/bank/970418.png" },
                new { Id = "970449", Name = "MB Bank", Logo = "https://img.vietqr.io/bank/970449.png" },
                new { Id = "970423", Name = "Sacombank", Logo = "https://img.vietqr.io/bank/970423.png" },
                new { Id = "970405", Name = "Timo Digital Bank", Logo = "https://img.vietqr.io/bank/970405.png" }
            };

            return Ok(supportedBanks);
        }
    }
}
