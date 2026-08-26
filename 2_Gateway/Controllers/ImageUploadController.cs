using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Crawl-to-Onboard Phase 6 (O1): Anonymous GPKD image upload for KhachLink Claim form.
    /// KhachLink uploads GPKD image here → Cloudinary → returns URL → submitted with claim request.
    /// Rate-limited (10/hour/IP) to prevent abuse. AllowAnonymous because claim form is unauthenticated.
    /// Cloudinary no-ops if config missing (dev/test) → returns 503 with helpful message.
    /// </summary>
    [ApiController]
    [Route("api/v1/images")]
    public class ImageUploadController(
        IImageStorageService imageStorage,
        ILogger<ImageUploadController> logger) : ControllerBase
    {
        private readonly IImageStorageService _imageStorage = imageStorage;
        private readonly ILogger<ImageUploadController> _logger = logger;

        /// <summary>
        /// Upload a single image (GPKD photo). Returns the public Cloudinary URL.
        /// Allowed: .jpg/.jpeg/.png/.webp, max 5MB (enforced by CloudinaryImageStorageService).
        /// </summary>
        [HttpPost("upload")]
        [AllowAnonymous]
        [EnableRateLimiting("image-upload")]
        public async Task<ActionResult<ImageUploadResult>> Upload(
            IFormFile file,
            [FromQuery] string folder = "gpkd-claims",
            CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Không có file được chọn hoặc file rỗng." });

            try
            {
                var url = await _imageStorage.UploadAsync(file, folder, ct);
                if (string.IsNullOrEmpty(url))
                {
                    // Cloudinary not configured OR upload rejected (bad ext/size) — service logs the reason.
                    _logger.LogWarning("Image upload failed (rejected or Cloudinary not configured) — file {Name} {Size}B",
                        file.FileName, file.Length);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        new { error = "Upload tạm thời không khả dụng. Vui lòng thử lại sau hoặc liên hệ hỗ trợ." });
                }

                _logger.LogInformation("Image uploaded: {Name} → {Url}", file.FileName, url);
                return Ok(new ImageUploadResult(url));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image upload exception for file {Name}", file.FileName);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "Lỗi server khi upload. Vui lòng thử lại." });
            }
        }
    }

    /// <summary>Result DTO: the uploaded image's public URL.</summary>
    public record ImageUploadResult(string Url);
}
