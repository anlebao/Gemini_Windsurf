using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Ocr;

/// <summary>
/// OcrAssistService (P6 — 2026-11-11): orchestrate provider theo config.
/// - Resolve ảnh: ImageUrl (chỉ host AllowedUrlHost — chống SSRF) hoặc ImageBase64 inline.
/// - Provider: gemini (default) → lỗi thì fallback vision (nếu có key); vision (nếu config).
/// - Thiếu key / ảnh không tải được / mọi provider lỗi → Provider="none" + Warning (controller 503).
/// - Không log nội dung ảnh / extraction PII — chỉ kind/status.
/// </summary>
public sealed class OcrAssistService : IOcrAssistService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<OcrAssistService> _logger;
    private readonly GeminiOcrProvider _gemini;
    private readonly VisionOcrProvider _vision;

    public OcrAssistService(HttpClient httpClient, IConfiguration configuration, ILogger<OcrAssistService> logger)
    {
        _http = httpClient;
        _config = configuration;
        _logger = logger;
        _gemini = new GeminiOcrProvider(httpClient, logger);
        _vision = new VisionOcrProvider(httpClient, logger);
    }

    public async Task<OcrAssistResponse> AssistAsync(OcrAssistRequest request, CancellationToken ct)
    {
        var opts = _config.GetSection(OcrAssistOptions.SectionName).Get<OcrAssistOptions>() ?? new OcrAssistOptions();
        var provider = opts.Provider?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(provider) || provider == "none")
            return new OcrAssistResponse("none", request.Kind, Warning: "OCR assist chưa cấu hình.");

        // 1. Resolve image bytes (ImageUrl allowlist / ImageBase64)
        byte[] imageBytes;
        string contentType;
        try
        {
            (imageBytes, contentType) = await ResolveImageAsync(request, opts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR assist image resolve failed (kind {Kind})", request.Kind);
            return new OcrAssistResponse("none", request.Kind, Warning: "Không tải được ảnh. Vui lòng thử lại.");
        }

        // 2. Provider chain
        if (provider == "gemini")
        {
            if (string.IsNullOrWhiteSpace(opts.GeminiApiKey))
                return new OcrAssistResponse("none", request.Kind, Warning: "OCR assist chưa cấu hình.");

            var geminiResult = await _gemini.ExtractAsync(request.Kind, imageBytes, contentType, opts.GeminiApiKey, opts.GeminiModel, ct);
            if (geminiResult is not null)
                return geminiResult;

            // Fallback vision (escape hatch — paid, không train)
            if (!string.IsNullOrWhiteSpace(opts.VisionApiKey))
            {
                var visionResult = await _vision.ExtractAsync(request.Kind, imageBytes, contentType, opts.VisionApiKey, ct);
                if (visionResult is not null)
                    return visionResult with { Warning = "Dùng Google Cloud Vision (Gemini lỗi)." };
            }
            return new OcrAssistResponse("none", request.Kind, Warning: "Dịch vụ nhận diện tạm thời lỗi. Vui lòng thử lại sau.");
        }

        if (provider == "vision")
        {
            if (string.IsNullOrWhiteSpace(opts.VisionApiKey))
                return new OcrAssistResponse("none", request.Kind, Warning: "OCR assist chưa cấu hình.");

            var visionResult = await _vision.ExtractAsync(request.Kind, imageBytes, contentType, opts.VisionApiKey, ct);
            if (visionResult is not null)
                return visionResult;
            return new OcrAssistResponse("none", request.Kind, Warning: "Dịch vụ nhận diện tạm thời lỗi. Vui lòng thử lại sau.");
        }

        return new OcrAssistResponse("none", request.Kind, Warning: "OCR assist chưa cấu hình.");
    }

    /// <summary>
    /// ImageUrl → download bytes (chỉ host allowlist). ImageBase64 → decode inline.
    /// Ném exception khi không lấy được — caller map sang Provider="none".
    /// </summary>
    internal async Task<(byte[] Bytes, string ContentType)> ResolveImageAsync(
        OcrAssistRequest request, OcrAssistOptions opts, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            if (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("ImageUrl không hợp lệ.");
            if (!string.Equals(uri.Host, opts.AllowedUrlHost, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Host ảnh không được phép ({uri.Host}).");
            if (uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("ImageUrl phải dùng HTTPS.");

            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Tải ảnh thất bại (HTTP {(int)response.StatusCode}).");
            if (response.Content.Headers.ContentLength is { } cl && cl > opts.MaxImageBytes)
                throw new InvalidOperationException("Ảnh quá lớn.");

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0 || bytes.Length > opts.MaxImageBytes)
                throw new InvalidOperationException("Ảnh rỗng hoặc quá lớn.");
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return (bytes, contentType);
        }

        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            var b64 = request.ImageBase64;
            var commaIdx = b64.IndexOf(',');
            if (commaIdx >= 0 && b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                b64 = b64[(commaIdx + 1)..];
            var bytes = Convert.FromBase64String(b64);
            if (bytes.Length == 0 || bytes.Length > opts.MaxImageBytes)
                throw new InvalidOperationException("Ảnh rỗng hoặc quá lớn.");
            return (bytes, string.IsNullOrWhiteSpace(request.ImageContentType) ? "image/jpeg" : request.ImageContentType!);
        }

        throw new InvalidOperationException("Thiếu ảnh (ImageUrl hoặc ImageBase64).");
    }
}