using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VanAn.ShopERP.Services;

/// <summary>
/// W3b — menu image → product lines qua Gateway OCR assist (kind=menu, anonymous
/// endpoint /api/v1/ocr-assist, provider Gemini vision). Tái dùng infra OCR có
/// sẵn từ self-serve onboarding (FR-17) — KHÔNG tạo OCR engine mới.
/// Fail-safe: mọi lỗi → null (UI báo "không đọc được, nhập tay").
/// </summary>
public interface IMenuOcrService
{
    Task<MenuOcrResult?> ExtractMenuAsync(string imageBase64, string contentType, CancellationToken ct = default);
}

public sealed record MenuOcrLine(string Name, decimal? Price);
public sealed record MenuOcrResult(List<MenuOcrLine> Items, string? Warning);

public sealed class MenuOcrService : IMenuOcrService
{
    private const int MaxImageBytes = 8 * 1024 * 1024; // mirror OcrAssistOptions.MaxImageBytes
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<MenuOcrService> _logger;

    public MenuOcrService(IHttpClientFactory httpClientFactory, IConfiguration configuration,
        ILogger<MenuOcrService> logger)
    {
        _http = httpClientFactory.CreateClient("GatewayClient");
        _http.BaseAddress ??= new Uri(configuration["Gateway:BaseUrl"] ?? "http://localhost:5001");
        if (!_http.BaseAddress!.OriginalString.EndsWith('/'))
            _http.BaseAddress = new Uri(_http.BaseAddress.OriginalString + "/");
        _logger = logger;
    }

    public async Task<MenuOcrResult?> ExtractMenuAsync(string imageBase64, string contentType, CancellationToken ct = default)
    {
        // base64 inflates ~4/3 — chặn trước khi tốn băng thông (server cap 8MB binary).
        if (string.IsNullOrWhiteSpace(imageBase64)
            || imageBase64.Length > (MaxImageBytes * 4 / 3) + 1024)
            return null;

        try
        {
            var resp = await _http.PostAsJsonAsync("api/v1/ocr-assist",
                new { kind = "menu", imageBase64, imageContentType = contentType }, Json, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogDebug("Menu OCR assist HTTP {Status}", (int)resp.StatusCode);
                return null;
            }
            var dto = await resp.Content.ReadFromJsonAsync<OcrAssistResponseDto>(Json, ct);
            if (dto is null) return null;

            var items = (dto.ProductLines ?? [])
                .Where(l => !string.IsNullOrWhiteSpace(l.Name))
                .Select(l => new MenuOcrLine(l.Name!.Trim(), l.Price))
                .ToList();
            return new MenuOcrResult(items, dto.Warning);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Menu OCR assist failed");
            return null;
        }
    }

    private sealed record OcrAssistResponseDto(
        string? Provider,
        List<OcrProductLineDto>? ProductLines,
        string? Warning);

    private sealed record OcrProductLineDto(string? Name, decimal? Price);
}
