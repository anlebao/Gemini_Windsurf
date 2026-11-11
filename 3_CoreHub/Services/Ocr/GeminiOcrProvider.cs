using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Ocr;

/// <summary>
/// Gemini provider (P6 — 2026-11-11): Generative Language API generateContent với
/// inline image + prompt structured theo kind → parse JSON → OcrAssistResponse.
/// Trả null khi mọi attempt lỗi (service quyết định fallback/502). Không log nội dung ảnh.
/// </summary>
public sealed class GeminiOcrProvider(HttpClient httpClient, ILogger logger)
{
    private const string ApiBase = "https://generativelanguage.googleapis.com/v1beta";

    private static readonly Dictionary<OcrAssistKind, string> Prompts = new()
    {
        [OcrAssistKind.Gpdk] =
            "Ảnh là giấy phép kinh doanh (GPKD) của Việt Nam. Trích xuất JSON chính xác: " +
            "{\"mst\": \"mã số thuế 10 hoặc 13 số\", \"name\": \"tên doanh nghiệp\", \"address\": \"địa chỉ\", " +
            "\"legalForm\": \"TNHH hoặc Cổ phần hoặc HKD hoặc HTX hoặc DNTN\", \"owner\": \"người đại diện/chủ hộ\"}. " +
            "Nếu không có giá trị, để null. Chỉ trả JSON, không giải thích.",
        [OcrAssistKind.Sign] =
            "Ảnh là biển hiệu cửa hàng/quán tại Việt Nam (chụp ngoài trời). Trích xuất JSON: " +
            "{\"signName\": \"tên hiệu chính\", \"address\": \"địa chỉ nếu có trên biển\", \"phone\": \"số điện thoại nếu có\"}. " +
            "Nếu không có giá trị, để null. Chỉ trả JSON, không giải thích.",
        [OcrAssistKind.Menu] =
            "Ảnh là thực đơn (menu) quán tại Việt Nam. Trích xuất JSON: " +
            "{\"items\": [{\"name\": \"tên món\", \"price\": 0}]} — price là số nguyên VNĐ (null nếu không thấy giá). " +
            "Chỉ trả JSON, không giải thích."
    };

    public async Task<OcrAssistResponse?> ExtractAsync(
        OcrAssistKind kind, byte[] imageBytes, string contentType, string apiKey, string model, CancellationToken ct)
    {
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { inline_data = new { mime_type = contentType, data = Convert.ToBase64String(imageBytes) } },
                        new { text = Prompts[kind] }
                    }
                }
            },
            generationConfig = new { temperature = 0.1, maxOutputTokens = 1024 }
        };

        var url = $"{ApiBase}/models/{model}:generateContent?key={apiKey}";
        try
        {
            using var response = await httpClient.PostAsJsonAsync(url, payload, ct);
            // Retry 1 lần (backoff 2s) cho lỗi transient (429/5xx) — RV 2026-11-11 bắt Gemini 503 upstream.
            if (response.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning("Gemini OCR {Status} (kind {Kind}) — retry 1 lần", (int)response.StatusCode, kind);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                using var retry = await httpClient.PostAsJsonAsync(url, payload, ct);
                if (!retry.IsSuccessStatusCode)
                {
                    logger.LogWarning("Gemini OCR retry {Status} (kind {Kind})", (int)retry.StatusCode, kind);
                    return null;
                }
                var retryBody = await retry.Content.ReadAsStringAsync(ct);
                var retryText = ExtractText(retryBody);
                if (string.IsNullOrWhiteSpace(retryText))
                {
                    logger.LogWarning("Gemini OCR retry empty text (kind {Kind})", kind);
                    return null;
                }
                return ParseResponse(kind, retryText);
            }
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini OCR {Status} (kind {Kind})", (int)response.StatusCode, kind);
                return null;
            }
            var body = await response.Content.ReadAsStringAsync(ct);
            var text = ExtractText(body);
            if (string.IsNullOrWhiteSpace(text))
            {
                logger.LogWarning("Gemini OCR empty text (kind {Kind})", kind);
                return null;
            }
            return ParseResponse(kind, text);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gemini OCR exception (kind {Kind})", kind);
            return null;
        }
    }

    /// <summary>Lấy text từ candidates[0].content.parts[].text (JSON response Gemini).</summary>
    internal string? ExtractText(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var first = candidates[0];
                if (first.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var t)) sb.Append(t.GetString());
                    }
                    return sb.ToString();
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Gemini response parse failed");
            return null;
        }
    }

    /// <summary>Parse text (có thể bọc markdown fence) → OcrAssistResponse theo kind.</summary>
    internal OcrAssistResponse? ParseResponse(OcrAssistKind kind, string text)
    {
        var json = TryParseJson(text);
        if (json is null)
        {
            logger.LogWarning("Gemini OCR no JSON in response (kind {Kind})", kind);
            return null;
        }

        using var doc = json;
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            logger.LogWarning("Gemini OCR unexpected root {Kind}", root.ValueKind);
            return null;
        }

        return kind switch
        {
            OcrAssistKind.Gpdk => new OcrAssistResponse(
                "gemini", kind,
                Mst: GetString(root, "mst"),
                Name: GetString(root, "name"),
                Address: GetString(root, "address"),
                LegalForm: GetString(root, "legalForm"),
                Owner: GetString(root, "owner")),
            OcrAssistKind.Sign => new OcrAssistResponse(
                "gemini", kind,
                SignName: GetString(root, "signName"),
                Address: GetString(root, "address"),
                Phone: GetString(root, "phone")),
            OcrAssistKind.Menu => new OcrAssistResponse(
                "gemini", kind,
                ProductLines: ParseMenuItems(root)),
            _ => null
        };
    }

    private static List<OcrAssistProductLine>? ParseMenuItems(JsonElement root)
    {
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<OcrAssistProductLine>();
        var lineNo = 0;
        foreach (var item in items.EnumerateArray())
        {
            var name = GetString(item, "name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            list.Add(new OcrAssistProductLine(name.Trim(), ParsePrice(item, "price"), ++lineNo));
        }
        return list.Count > 0 ? list : null;
    }

    /// <summary>Parse price linh hoạt: number hoặc string "35.000đ"/"25k"/"25 nghìn" → decimal VNĐ.</summary>
    internal static decimal? ParsePrice(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var el) || el.ValueKind == JsonValueKind.Null)
            return null;
        if (el.ValueKind == JsonValueKind.Number)
        {
            return el.TryGetDecimal(out var d) ? d : null;
        }
        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString() ?? "";
            var digits = new string(s.Where(char.IsAsciiDigit).ToArray());
            if (digits.Length == 0 || !decimal.TryParse(digits, out var v)) return null;
            if (System.Text.RegularExpressions.Regex.IsMatch(s, @"(k|nghìn)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                v *= 1000;
            return v;
        }
        return null;
    }

    private static string? GetString(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.String)
            return null;
        var v = el.GetString();
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    /// <summary>Lấy object JSON đầu tiên (strip markdown fences) hoặc array — null nếu không có.</summary>
    internal JsonDocument? TryParseJson(string text)
    {
        var cleaned = text.Trim();
        var fence = cleaned.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            var start = cleaned.IndexOf('{', fence);
            var end = cleaned.LastIndexOf('}');
            if (start >= 0 && end > start) cleaned = cleaned[start..(end + 1)];
        }

        var braceStart = cleaned.IndexOf('{');
        var braceEnd = cleaned.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
        {
            try { return JsonDocument.Parse(cleaned[braceStart..(braceEnd + 1)]); }
            catch (JsonException) { /* fallthrough */ }
        }
        var arrStart = cleaned.IndexOf('[');
        var arrEnd = cleaned.LastIndexOf(']');
        if (arrStart >= 0 && arrEnd > arrStart)
        {
            try { return JsonDocument.Parse(cleaned[arrStart..(arrEnd + 1)]); }
            catch (JsonException) { /* fallthrough */ }
        }
        return null;
    }
}