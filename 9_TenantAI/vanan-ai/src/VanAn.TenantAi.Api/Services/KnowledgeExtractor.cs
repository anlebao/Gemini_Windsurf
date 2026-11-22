using System.Text.Json;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Contracts;

namespace VanAn.TenantAi.Api.Services;

/// <summary>
/// W3a Auto-Builder extraction — one LLM call turns raw pasted text (menu copy,
/// bảng giá, FAQ, chính sách) into structured candidates the owner reviews before
/// anything reaches the knowledge pipeline. Never throws: every failure path
/// returns an empty-items response the UI can render as "không trích xuất được".
/// Output is preview-only — ingest still goes through DocumentIngestRequest +
/// approval gate, so nothing here bypasses merchant confirm.
/// </summary>
public sealed class KnowledgeExtractor(ILlmChatClient llm, ILogger<KnowledgeExtractor> logger)
{
    public const int MaxItems = 50;
    private const int MaxContentLength = 500;

    private static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal)
        { "product", "service", "faq", "policy", "hours", "other" };

    private const string SchemaHint = """
        {"type":"object","properties":{"summary":{"type":"string"},"items":{"type":"array","items":{"type":"object","properties":{"kind":{"type":"string"},"title":{"type":"string"},"content":{"type":"string"}},"required":["kind","title","content"]}}},"required":["summary","items"]}
        """;

    public async Task<KnowledgeExtractResponse> ExtractAsync(KnowledgeExtractRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return new KnowledgeExtractResponse("", []);

        if (!llm.Enabled)
            return new KnowledgeExtractResponse("Trợ lý AI chưa sẵn sàng.", []);

        try
        {
            var result = await llm.GenerateAsync(BuildPrompt(request), SchemaHint, ct);
            if (result is null)
                return new KnowledgeExtractResponse("Không trích xuất được — thử lại sau.", []);

            return Parse(result.Text);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Knowledge extraction failed");
            return new KnowledgeExtractResponse("Không trích xuất được — thử lại sau.", []);
        }
    }

    private static string BuildPrompt(KnowledgeExtractRequest request)
    {
        var hint = string.IsNullOrWhiteSpace(request.Hint) ? "" : $"Gợi ý của chủ shop: {request.Hint}\n";
        return """
            Bạn là bộ trích xuất tri thức cho trợ lý AI của một cửa hàng Việt Nam.
            Nhiệm vụ: đọc văn bản thô chủ shop dán vào (menu, bảng giá, FAQ, chính sách, giờ mở cửa)
            và tách thành các mục tri thức có cấu trúc. TUYỆT ĐỐI chỉ trả về JSON hợp lệ, không markdown, không giải thích.

            kind chỉ được là một trong: product | service | faq | policy | hours | other.
            - product/service: món/dịch vụ có tên + giá nếu có (giữ nguyên đơn vị tiền).
            - faq: câu hỏi khách thường hỏi + câu trả lời ngắn.
            - policy: chính sách (đổi trả, đặt cọc, bảo hành...).
            - hours: giờ mở cửa / lịch hoạt động.
            - other: thông tin khác hữu ích cho trợ lý.

            Quy tắc: giữ nguyên tiếng Việt và con số giá; không bịa thông tin không có trong văn bản;
            mỗi mục content ngắn gọn, tự đủ nghĩa; tối đa 50 mục, ưu tiên mục có giá và FAQ.

            """ + hint + "Văn bản nguồn:\n" + request.Text.Trim();
    }

    private static KnowledgeExtractResponse Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new KnowledgeExtractResponse("", []);

        // LLM đôi khi bọc JSON trong ```json fence hoặc thêm text thừa — lấy object ngoài cùng.
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return new KnowledgeExtractResponse("Không đọc được kết quả trích xuất.", []);

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            var root = doc.RootElement;

            var summary = root.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? "" : "";

            var items = new List<KnowledgeExtractItem>();
            if (root.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    if (items.Count >= MaxItems) break;
                    var kind = el.TryGetProperty("kind", out var k) ? k.GetString() : null;
                    var title = el.TryGetProperty("title", out var t) ? t.GetString() : null;
                    var content = el.TryGetProperty("content", out var c) ? c.GetString() : null;
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
                        continue;
                    var normalizedKind = kind is not null && AllowedKinds.Contains(kind) ? kind : "other";
                    items.Add(new KnowledgeExtractItem(normalizedKind, title.Trim(),
                        content.Trim()[..Math.Min(content.Trim().Length, MaxContentLength)]));
                }
            }

            return new KnowledgeExtractResponse(summary, items);
        }
        catch (JsonException)
        {
            return new KnowledgeExtractResponse("Không đọc được kết quả trích xuất.", []);
        }
    }
}
