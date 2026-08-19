using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// OCR Hub S2: OCR engine config API — SystemAdmin only.
/// GET /api/ocr/config — get current OCR engine config
/// PUT /api/ocr/config — update OCR engine config
/// Default: Tesseract for both plate + menu (backward compat).
/// Pattern: copied from FeatureFlagsController.
/// </summary>
[ApiController]
[Route("api/ocr/config")]
[Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class OcrConfigController(
    IOcrConfigService configService,
    ILogger<OcrConfigController> logger) : ControllerBase
{
    private readonly IOcrConfigService _configService = configService;
    private readonly ILogger<OcrConfigController> _logger = logger;

    [HttpGet]
    public async Task<ActionResult<OcrEngineConfig>> Get(CancellationToken ct)
    {
        var config = await _configService.GetConfigAsync(ct);
        return Ok(config);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateOcrConfigRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest("Request body is required.");

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                          ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized();

        try
        {
            var config = new OcrEngineConfig
            {
                PlateEngine = request.PlateEngine ?? "Tesseract",
                MenuEngine = request.MenuEngine ?? "Tesseract"
            };
            await _configService.UpdateConfigAsync(config, userId, ct);
            _logger.LogInformation("OCR config updated: PlateEngine={Plate}, MenuEngine={Menu} by user {UserId}",
                config.PlateEngine, config.MenuEngine, userId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public record UpdateOcrConfigRequest(string? PlateEngine, string? MenuEngine);
}
