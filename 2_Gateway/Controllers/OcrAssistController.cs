using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VanAn.CoreHub.Services.Ocr;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// P6 (2026-11-11): Cloud OCR assist cho onboarding self-serve (FR-17).
    /// KhachLink/ShopERP wizard gọi khi Tesseract confidence thấp → Gemini/Cloud Vision
    /// → trả extraction structured (gemini) hoặc raw lines (vision — client tự parse).
    /// Anonymous + rate-limit 20/h/IP. Không log nội dung ảnh / extraction PII.
    /// </summary>
    [ApiController]
    [Route("api/v1")]
    public class OcrAssistController(
        IOcrAssistService ocrAssist,
        ILogger<OcrAssistController> logger) : ControllerBase
    {
        [HttpPost("ocr-assist")]
        [AllowAnonymous]
        [EnableRateLimiting("ocr-assist")]
        public async Task<ActionResult<OcrAssistResponse>> Assist(
            [FromBody] OcrAssistRequestDto? dto,
            CancellationToken ct)
        {
            if (dto is null || !TryParseKind(dto.Kind, out var kind))
                return BadRequest(new { error = "kind không hợp lệ. Chỉ chấp nhận gpdk | sign | menu." });
            if (string.IsNullOrWhiteSpace(dto.ImageUrl) && string.IsNullOrWhiteSpace(dto.ImageBase64))
                return BadRequest(new { error = "Thiếu ảnh (ImageUrl hoặc ImageBase64)." });

            var response = await ocrAssist.AssistAsync(
                new OcrAssistRequest(kind, dto.ImageUrl, dto.ImageBase64, dto.ImageContentType), ct);

            if (response.Provider == "none")
            {
                logger.LogDebug("OCR assist unavailable: {Warning} (kind {Kind})", response.Warning, kind);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = response.Warning ?? "OCR assist chưa cấu hình." });
            }

            logger.LogInformation("OCR assist OK: provider {Provider} kind {Kind}", response.Provider, kind);
            return Ok(response);
        }

        private static bool TryParseKind(string? kind, out OcrAssistKind parsed)
        {
            switch (kind?.Trim().ToLowerInvariant())
            {
                case "gpdk": parsed = OcrAssistKind.Gpdk; return true;
                case "sign": parsed = OcrAssistKind.Sign; return true;
                case "menu": parsed = OcrAssistKind.Menu; return true;
                default: parsed = OcrAssistKind.Gpdk; return false;
            }
        }
    }

    /// <summary>Request DTO public — kind: gpdk | sign | menu; ảnh qua ImageUrl (R2 presigned) hoặc ImageBase64.</summary>
    public sealed record OcrAssistRequestDto(
        string Kind,
        string? ImageUrl = null,
        string? ImageBase64 = null,
        string? ImageContentType = null);
}