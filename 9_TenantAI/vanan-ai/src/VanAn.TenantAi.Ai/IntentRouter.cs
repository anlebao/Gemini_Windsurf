using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace VanAn.TenantAi.Ai;

/// <summary>
/// task_ai_p3 / SRS FR-2 — rule-first intent router. Keyword match (diacritics-
/// insensitive vi-VN) in a fixed precedence order → LLM classify fallback
/// (1 label) → still unsure = OutOfScope. PolicyViolation short-circuits before
/// any other rule and never reaches the LLM (0 LLM call — gate test).
/// </summary>
public sealed class IntentRouter(ILlmChatClient llm, ILogger<IntentRouter> logger)
{
    /// <summary>
    /// Ordered rules — first hit wins. Keywords are matched against the
    /// accent-stripped lowercase message. Order matters: specific intents
    /// (Nutrition/Availability) precede the broad "bao nhiêu" price bucket and
    /// the catch-all MenuQuery; ChitChat is last so a greeting embedded in a
    /// real question still routes to the real intent.
    /// </summary>
    private static readonly (string Intent, string[] Keywords)[] Rules =
    [
        (ChatIntents.PolicyViolation,
        [
            "ignore all", "ignore previous", "forget all", "system prompt",
            "reveal your", "reveal the prompt", "new instructions", "you are now",
            "bo qua luat", "bo qua moi", "hay quen", "hien prompt", "lo prompt",
            "prompt cua ban", "prompt cua may", "dong vai", "jailbreak"
        ]),
        (ChatIntents.ExternalKnowledge,
        [
            "thoi tiet", "tin tuc", "tong thong", "chatgpt", "gemini",
            "kien thuc chung", "ben ngoai quan", "ngoai menu", "ngoai thuc don"
        ]),
        (ChatIntents.NutritionQuery,
        [
            "calo", "kcal", "calorie", "dinh duong", "di ung", "allerg",
            "it beo", "giam can", "an kieng", "kieng", "healthy", "map "
        ]),
        (ChatIntents.AvailabilityQuery,
        [
            "con khong", "con hang", "con mon", "het chua", "het hang",
            "het mon", "co san", "con ban", "con phuc vu"
        ]),
        (ChatIntents.RecipeQuery,
        [
            "cong thuc", "cach lam", "cach nau", "cach pha", "recipe",
            "lam the nao", "lam mon", "pha che", "nguyen lieu"
        ]),
        (ChatIntents.BrandStory,
        [
            "cau chuyen", "thuong hieu", "ve quan", "gioi thieu quan",
            "quan ban", "quan cua ban", "ra doi", "lich su quan", "brand",
            "ten quan", "sang lap"
        ]),
        (ChatIntents.OrderHelp,
        [
            "dat hang", "cach dat", "dat nhu the nao", "dat the nao",
            "thanh toan", "gio hang", "order", "mua hang", "mua the nao",
            "dat mon nhu the nao", "thanh toan the nao"
        ]),
        (ChatIntents.Faq,
        [
            "gio mo", "may gio", "mo cua", "dong cua", "dia chi", "o dau",
            "giao hang", "ship", "doi tra", "chinh sach", "wifi", "dau xe",
            "giu xe", "khuyen mai", "giam gia", "voucher", "ban giao"
        ]),
        (ChatIntents.PriceQuery,
        [
            "gia", "bao nhieu", "mac khong", "re khong", "price", "chi phi"
        ]),
        (ChatIntents.MenuQuery,
        [
            "menu", "thuc don", "mon gi", "co mon", "mon an", "do uong",
            "mon nao", "goi y mon", "xem mon", "nen an gi", "nen uong gi",
            "mon chay", "mon man", "trang mieng", "dessert"
        ]),
        (ChatIntents.ChitChat,
        [
            "xin chao", "chao", "hello", "hi", "hey", "cam on", "tam biet",
            "ban la ai", "ban ten gi", "khoe khong", "nguoi that"
        ])
    ];

    /// <summary>Route a raw user message to an FR-2 intent label.</summary>
    public async Task<string> RouteAsync(string text, CancellationToken ct = default)
    {
        var matched = MatchRules(text);
        if (matched != null) return matched;
        if (!llm.Enabled) return ChatIntents.OutOfScope;

        var label = await ClassifyWithLlmAsync(text, ct);
        return label ?? ChatIntents.OutOfScope;
    }

    /// <summary>Rule-only match — exposed for the intent-matrix test batch.</summary>
    public string? MatchRules(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = Normalize(text);
        foreach (var (intent, keywords) in Rules)
            if (keywords.Any(k => Matches(normalized, k, intent)))
                return intent;
        return null;
    }

    /// <summary>
    /// ChitChat greetings use word-boundary matching — "hi"/"hey" are substrings of
    /// common words (ghi, thích→thich…); everything else is substring matching.
    /// </summary>
    private static bool Matches(string normalized, string keyword, string intent)
    {
        if (intent == ChatIntents.ChitChat)
            return normalized == keyword
                || normalized.StartsWith(keyword + " ", StringComparison.Ordinal)
                || normalized.Contains(" " + keyword + " ", StringComparison.Ordinal)
                || normalized.EndsWith(" " + keyword, StringComparison.Ordinal);
        return normalized.Contains(keyword, StringComparison.Ordinal);
    }

    /// <summary>Classify-only LLM fallback: prompt returns exactly one label [FR-2].</summary>
    private async Task<string?> ClassifyWithLlmAsync(string text, CancellationToken ct)
    {
        var prompt =
            "Phân loại câu hỏi của khách hàng vào ĐÚNG MỘT nhãn sau:\n"
            + string.Join(", ", ChatIntents.All)
            + "\nChỉ trả về JSON {\"intent\": \"<nhãn>\"}. Không giải thích.\n"
            + $"Câu hỏi: {text.Trim()}";
        try
        {
            var result = await llm.GenerateAsync(prompt, schemaHint: "intent_label", ct);
            if (result == null) return null;
            return ChatIntents.All.FirstOrDefault(i =>
                result.Text.Contains(i, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "LLM classify fallback failed — routing OutOfScope");
            return null;
        }
    }

    /// <summary>Lowercase + strip accents so keywords match both typed styles.</summary>
    public static string Normalize(string text)
    {
        var lowered = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(lowered.Length);
        foreach (var c in lowered)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd');
    }
}
