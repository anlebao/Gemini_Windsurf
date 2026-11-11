using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Ocr;

/// <summary>
/// Cloud Vision provider (P6 — 2026-11-11): DOCUMENT_TEXT_DETECTION → raw lines.
/// Client giữ heuristic parse (sign/menu) — vision chỉ cung cấp text. Escape hatch
/// khi Gemini không cấu hình/lỗi. Trả null khi lỗi (không log nội dung ảnh).
/// </summary>
public sealed class VisionOcrProvider(HttpClient httpClient, ILogger logger)
{
    private const string ApiBase = "https://vision.googleapis.com/v1";

    public async Task<OcrAssistResponse?> ExtractAsync(
        OcrAssistKind kind, byte[] imageBytes, string contentType, string apiKey, CancellationToken ct)
    {
        var payload = new
        {
            requests = new[]
            {
                new
                {
                    image = new { content = Convert.ToBase64String(imageBytes) },
                    features = new[] { new { type = "DOCUMENT_TEXT_DETECTION", maxResults = 1 } }
                }
            }
        };

        var url = $"{ApiBase}/images:annotate?key={apiKey}";
        try
        {
            using var response = await httpClient.PostAsJsonAsync(url, payload, ct);
            // Retry 1 lần (backoff 2s) cho lỗi transient (429/5xx).
            if (response.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning("Vision OCR {Status} (kind {Kind}) — retry 1 lần", (int)response.StatusCode, kind);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                using var retry = await httpClient.PostAsJsonAsync(url, payload, ct);
                if (!retry.IsSuccessStatusCode)
                {
                    logger.LogWarning("Vision OCR retry {Status} (kind {Kind})", (int)retry.StatusCode, kind);
                    return null;
                }
                var retryBody = await retry.Content.ReadAsStringAsync(ct);
                var retryLines = ExtractLines(retryBody);
                if (retryLines is null || retryLines.Count == 0)
                {
                    logger.LogWarning("Vision OCR retry no text (kind {Kind})", kind);
                    return null;
                }
                return new OcrAssistResponse("vision", kind, Lines: retryLines);
            }
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Vision OCR {Status} (kind {Kind})", (int)response.StatusCode, kind);
                return null;
            }
            var body = await response.Content.ReadAsStringAsync(ct);
            var lines = ExtractLines(body);
            if (lines is null || lines.Count == 0)
            {
                logger.LogWarning("Vision OCR no text (kind {Kind})", kind);
                return null;
            }
            return new OcrAssistResponse("vision", kind, Lines: lines);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Vision OCR exception (kind {Kind})", kind);
            return null;
        }
    }

    /// <summary>fullTextAnnotation.text → dòng text (non-empty), confidence null (word-level phức tạp).</summary>
    internal List<OcrAssistLine>? ExtractLines(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("responses", out var responses) || responses.GetArrayLength() == 0)
                return null;
            var first = responses[0];
            if (!first.TryGetProperty("fullTextAnnotation", out var fta))
                return null;
            var text = fta.TryGetProperty("text", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(text)) return null;

            return text!
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .Select(l => new OcrAssistLine(l))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Vision response parse failed");
            return null;
        }
    }
}