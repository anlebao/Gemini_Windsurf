using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Crawl-to-Onboard Phase 6 (O1): Anonymous GPKD image upload cho KhachLink Claim form.
    /// KhachLink uploads GPKD image here → Cloudflare R2 → presigned GET URL → submitted với claim request.
    /// Rate-limited (10/hour/IP) để chống abuse. AllowAnonymous vì claim form không cần đăng nhập.
    ///
    /// Fix #189 (2026-10-14, user directive "copy cách lưu ảnh bảo vệ giữ xe"): chuyển từ Cloudinary
    /// sang Cloudflare R2 — CÙNG cơ chế ảnh biển số + ảnh khách của GuardService (R2StorageService.
    /// UploadObjectAsync server-side). Cloudinary chưa bao giờ được config trên production → mọi upload
    /// trả 503 "Upload tạm thời không khả dụng". R2 đã config trên gateway (bucket vanan-guard-photos).
    /// </summary>
    [ApiController]
    [Route("api/v1/images")]
    public class ImageUploadController(
        IR2StorageService r2Storage,
        ILogger<ImageUploadController> logger) : ControllerBase
    {
        private readonly IR2StorageService _r2Storage = r2Storage;
        private readonly ILogger<ImageUploadController> _logger = logger;

        // Giới hạn khớp KhachLink ImageUploadService (5MB, .jpg/.jpeg/.png/.webp).
        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        // P6 (2026-11-11): kind cho onboarding — key R2 onboarding/{kind}/{guid}.{ext}
        // (album phân loại review — parse kind từ URL, không đổi DB). Thumb → onboarding-thumbs/.
        private static readonly HashSet<string> AllowedKinds =
            new(StringComparer.OrdinalIgnoreCase) { "gpdk", "sign", "menu", "doc" };
        private const long MaxFileSize = 5 * 1024 * 1024;
        // Presigned GET URL — TTL 7 ngày (claim được review sớm; key nằm trong URL → có thể
        // regenerate presigned URL mới từ key nếu cần xem lại sau).
        private const int PresignedGetTtlMinutes = 7 * 24 * 60;

        /// <summary>
        /// Upload a single image (GPKD photo). Returns a presigned GET URL (R2).
        /// </summary>
        [HttpPost("upload")]
        [AllowAnonymous]
        [EnableRateLimiting("image-upload")]
        public async Task<ActionResult<ImageUploadResult>> Upload(
            IFormFile file,
            [FromQuery] string folder = "gpkd-claims",
            [FromQuery] string? kind = null,
            [FromQuery] bool thumb = false,
            CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Không có file được chọn hoặc file rỗng." });
            if (file.Length > MaxFileSize)
                return BadRequest(new { error = "File quá lớn. Kích thước tối đa 5MB." });
            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext))
                return BadRequest(new { error = "Định dạng không hợp lệ. Chỉ chấp nhận .jpg, .jpeg, .png, .webp." });
            // P6: kind chỉ áp dụng cho folder onboarding (album phân loại); thumb cần kind.
            if (kind != null && !AllowedKinds.Contains(kind))
                return BadRequest(new { error = "kind không hợp lệ. Chỉ chấp nhận gpdk | sign | menu | doc." });
            if (kind != null && !string.Equals(folder, "onboarding", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "kind chỉ áp dụng cho folder onboarding." });
            if (thumb && kind == null)
                return BadRequest(new { error = "Thiếu kind khi upload thumbnail." });

            try
            {
                // R2 key: {folder}/{kind}/{guid}.{ext} — kind=null giữ pattern legacy (plates/customers/{tenantId}/...).
                // Thumb luôn JPEG 320px từ client → key cố định .jpg.
                var key = thumb
                    ? $"onboarding-thumbs/{kind}/{Guid.NewGuid():N}.jpg"
                    : kind != null
                        ? $"{folder}/{kind}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}"
                        : $"{folder}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
                await using var stream = file.OpenReadStream();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                var base64 = Convert.ToBase64String(ms.ToArray());
                var contentType = thumb ? "image/jpeg" : ext.ToLowerInvariant() switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };

                var ok = await _r2Storage.UploadObjectAsync(key, base64, contentType);
                if (!ok)
                {
                    _logger.LogWarning("R2 upload rejected — file {Name} {Size}B", file.FileName, file.Length);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        new { error = "Upload tạm thời không khả dụng. Vui lòng thử lại sau hoặc liên hệ hỗ trợ." });
                }

                var url = _r2Storage.GetPresignedDownloadUrl(key, PresignedGetTtlMinutes);
                _logger.LogInformation("Image uploaded to R2: {Key} → {Url}", key, url);
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

    /// <summary>Result DTO: the uploaded image's presigned URL.</summary>
    public record ImageUploadResult(string Url);
}
