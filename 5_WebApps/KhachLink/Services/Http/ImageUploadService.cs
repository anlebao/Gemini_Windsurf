using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using System.Net;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Crawl-to-Onboard Phase 6 (O1): Upload GPKD image to Gateway → Cloudinary → returns public URL.
/// Used by Claim.razor form. KhachLink is Blazor WASM — uses IBrowserFile (InputFile component).
/// Gateway endpoint: POST /api/v1/images/upload (AllowAnonymous, rate-limited 10/hour/IP).
/// </summary>
public class ImageUploadService(IHttpClientFactory httpClientFactory, ILogger<ImageUploadService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<ImageUploadService> _logger = logger;

    // Match CloudinaryImageStorageService limits (5MB, .jpg/.jpeg/.png/.webp).
    private const long MaxFileSize = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>
    /// Upload a GPKD image file. Returns the public URL on success, or an error message on failure.
    /// Client-side validation mirrors Gateway (size + extension) to fail fast before network round-trip.
    /// </summary>
    public async Task<ImageUploadOutcome> UploadGpkdAsync(IBrowserFile file, CancellationToken ct = default)
    {
        if (file == null)
            return ImageUploadOutcome.Failed("Vui lòng chọn file ảnh.");

        var ext = Path.GetExtension(file.Name);
        if (!AllowedExtensions.Contains(ext))
            return ImageUploadOutcome.Failed("Định dạng không hợp lệ. Chỉ chấp nhận .jpg, .jpeg, .png, .webp.");

        if (file.Size > MaxFileSize)
            return ImageUploadOutcome.Failed("File quá lớn. Kích thước tối đa 5MB.");

        try
        {
            // IBrowserFile.OpenReadStream enforces maxAllowedSize at the WASM runtime level.
            await using var stream = file.OpenReadStream(MaxFileSize, ct);

            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
                : ext.Equals(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
                : "image/jpeg");
            form.Add(fileContent, "file", file.Name);

            var response = await _httpClient.PostAsync("api/v1/images/upload?folder=gpkd-claims", form, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("GPKD upload rate-limited for file {Name}", file.Name);
                return ImageUploadOutcome.Failed("Bạn đã upload quá nhiều lần. Vui lòng thử lại sau 1 giờ.");
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var body503 = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: ct);
                return ImageUploadOutcome.Failed(body503?.Error ?? "Upload tạm thời không khả dụng. Vui lòng thử lại sau.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<ErrorBody>(cancellationToken: ct);
                _logger.LogWarning("GPKD upload failed: {Status} {Error}", response.StatusCode, body?.Error);
                return ImageUploadOutcome.Failed(body?.Error ?? $"Upload thất bại (HTTP {response.StatusCode}).");
            }

            var result = await response.Content.ReadFromJsonAsync<UploadResultBody>(cancellationToken: ct);
            if (string.IsNullOrEmpty(result?.Url))
                return ImageUploadOutcome.Failed("Server không trả về URL ảnh.");

            _logger.LogInformation("GPKD uploaded: {Name} → {Url}", file.Name, result.Url);
            return ImageUploadOutcome.Ok(result.Url);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GPKD upload exception for file {Name}", file.Name);
            return ImageUploadOutcome.Failed("Lỗi kết nối khi upload. Vui lòng thử lại.");
        }
    }

    // ── Local DTOs matching Gateway response bodies ─────────────────────────
    private sealed record UploadResultBody(string Url);
    private sealed record ErrorBody(string? Error);
}

/// <summary>Outcome of an upload attempt — either Ok (with URL) or Failed (with user-facing message).</summary>
public sealed record ImageUploadOutcome(bool Success, string? Url, string? Error)
{
    public static ImageUploadOutcome Ok(string url) => new(true, url, null);
    public static ImageUploadOutcome Failed(string error) => new(false, null, error);
}
