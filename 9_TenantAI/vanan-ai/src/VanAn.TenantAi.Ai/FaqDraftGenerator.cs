using System.Text.Json;

namespace VanAn.TenantAi.Ai;

/// <summary>W3f — FAQ draft cho 1 câu khách hỏi nhiều lần (title + content).</summary>
public sealed record FaqDraft(string Title, string Content);

/// <summary>
/// W3f — chat learning suggest: prompt + JSON parse cho việc LLM soạn 1 mục FAQ
/// từ câu hỏi đại diện của cụm. Pure + never-throw: parse fail → null (caller bỏ qua).
/// Không đổi nội dung nguồn; draft vẫn qua approval gate [G14] của chủ shop.
/// </summary>
public static class FaqDraftGenerator
{
    public const int MaxTitleLength = 120;
    public const int MaxContentLength = 500;

    public static string BuildPrompt(string question)
        => $$"""
            Bạn là trợ lý soạn tri thức cho trợ lý AI của một cửa hàng Việt Nam.
            Khách hàng hỏi nhiều lần câu sau:
            {{question}}

            Viết 1 mục FAQ:
            - title: câu hỏi gọn gàng bằng tiếng Việt, tối đa {{MaxTitleLength}} ký tự.
            - content: câu trả lời ngắn gọn, tối đa {{MaxContentLength}} ký tự.

            Quy tắc bắt buộc: TUYỆT ĐỐI không bịa giá cả, địa chỉ, số điện thoại,
            giờ mở cửa hay chính sách cụ thể của quán. Nếu không chắc chắn câu trả lời,
            ghi rõ cần chủ shop xác nhận. Giữ nguyên tiếng Việt.
            Chỉ trả về JSON hợp lệ: {"title": "...", "content": "..."}
            — không markdown, không giải thích.
            """;

    /// <summary>Parse JSON draft (chịu fence/markdown bọc ngoài). Null khi không hợp lệ.</summary>
    public static FaqDraft? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var root = doc.RootElement;
            var title = root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()?.Trim() : null;
            var content = root.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content)) return null;
            if (title.Length > MaxTitleLength || content.Length > MaxContentLength) return null;
            return new FaqDraft(title, content);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}