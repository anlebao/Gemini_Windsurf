using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using VanAn.TenantAi.Domain;

namespace VanAn.TenantAi.Ai;

/// <summary>Parsed LLM payload — the grounding contract from PromptBuilder.</summary>
public sealed record LlmAnswer(string Answer, List<Guid> ProductRefs, List<string> SourceIds, double Confidence);

/// <summary>
/// Validator output. Answer == corrected answer when <see cref="UseFallback"/>
/// is false; refs are already pruned to the retrieval set. StaleData triggers
/// volatile-claim stripping upstream [G16].
/// </summary>
public sealed record ValidationOutcome(
    string? Answer,
    List<Guid> ProductRefs,
    List<string> SourceIds,
    double Confidence,
    bool StaleData,
    bool UseFallback);

/// <summary>
/// task_ai_p3 / SRS FR-4 — grounded-answer validator:
///   · productRefs ∉ tenant product_knowledge → strip
///   · sourceIds ∉ retrieval set → strip
///   · numeric claims (price/calories) not matching structured data →
///     đính chính khi 1 productRef rõ ràng, else fallback (T13)
///   · confidence &lt; threshold → ép "chưa có dữ liệu xác nhận"
///   · stale product → strip volatile claims + staleData [G16]
/// Parse failure is reported by TryParse → orchestrator retries once → fallback.
/// </summary>
public sealed class AnswerValidator(IConfiguration config)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // "50k" / "50.000đ" / "50 nghìn" — unit suffix disambiguates money from stray digits.
    private static readonly Regex PriceClaim = new(
        @"(\d[\d.,\s]*\d|\d)\s*(kđ|k|ngàn|nghìn|đ|đồng|vnd|vnđ)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CalorieClaim = new(
        @"(\d[\d.,]*)\s*(kcal|calo|calories)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private double MinConfidence => config.GetValue("TenantAiChat:MinConfidence", 0.4);

    /// <summary>Parse the LLM payload — tolerates ```json fences, null on garbage.</summary>
    public LlmAnswer? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            text = text[start..(end + 1)];
        }
        try
        {
            var dto = JsonSerializer.Deserialize<LlmAnswerWire>(text, Json);
            if (dto?.Answer == null) return null;
            var refs = (dto.ProductRefs ?? [])
                .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                .Where(g => g != null).Select(g => g!.Value).ToList();
            return new LlmAnswer(dto.Answer, refs, dto.SourceIds ?? [], dto.Confidence ?? 0.5);
        }
        catch (JsonException) { return null; }
    }

    public ValidationOutcome Validate(
        LlmAnswer parsed,
        IReadOnlyList<ContextItem> facts,
        Func<ProductKnowledge, bool> isStale)
    {
        var products = facts.Where(f => f.Product != null)
            .ToDictionary(f => f.Product!.ProductId, f => f.Product!);
        var sourceIds = facts.Select(f => f.SourceId).ToHashSet(StringComparer.Ordinal);

        // FR-4: refs outside the retrieval set are hallucinations — strip.
        var refs = parsed.ProductRefs.Where(products.ContainsKey).ToList();
        var sources = parsed.SourceIds.Where(sourceIds.Contains).ToList();

        var answer = parsed.Answer;
        var stale = refs.Any(r => isStale(products[r]));

        // [G16] stale → volatile claims (price/calories) are stripped, flag set.
        if (stale) return new(null, refs, sources, parsed.Confidence, StaleData: true, UseFallback: true);

        if (parsed.Confidence < MinConfidence)
            return new(null, refs, sources, parsed.Confidence, StaleData: false, UseFallback: true);

        // Numeric grounding — correct single-ref mismatches, else drop to fallback.
        foreach (var claim in ExtractClaims(answer))
        {
            var expectsMatch = refs.Count > 0 && refs.Any(r => claim.Kind == ClaimKind.Price
                ? NearlyEqual(claim.Value, products[r].Price)
                : products[r].Calories is int cal && NearlyEqual(claim.Value, cal));
            if (expectsMatch) continue;
            // Claim matches no structured fact — an unsupported number.
            if (refs.Count != 1)
                return new(null, refs, sources, parsed.Confidence, StaleData: false, UseFallback: true);
            var product = products[refs[0]];
            string? replacement = claim.Kind switch
            {
                ClaimKind.Price => product.Price.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "đ",
                ClaimKind.Calories when product.Calories != null => product.Calories + " kcal",
                _ => null // no structured fact to correct against → cannot ground it
            };
            if (replacement == null)
                return new(null, refs, sources, parsed.Confidence, StaleData: false, UseFallback: true);
            answer = answer[..claim.Start] + replacement + answer[(claim.Start + claim.Length)..];
        }

        if (string.IsNullOrWhiteSpace(answer))
            return new(null, refs, sources, parsed.Confidence, StaleData: false, UseFallback: true);

        return new(answer, refs, sources, parsed.Confidence, StaleData: false, UseFallback: false);
    }

    private enum ClaimKind { Price, Calories }

    private sealed record NumericClaim(ClaimKind Kind, decimal Value, int Start, int Length);

    /// <summary>All price/calorie claims in the answer, left→right.</summary>
    private static IEnumerable<NumericClaim> ExtractClaims(string answer)
    {
        foreach (Match m in PriceClaim.Matches(answer))
            if (TryParseNumber(m.Groups[1].Value, out var n))
                yield return new NumericClaim(ClaimKind.Price, n * UnitMultiplier(m.Groups[2].Value), m.Index, m.Length);
        foreach (Match m in CalorieClaim.Matches(answer))
            if (TryParseNumber(m.Groups[1].Value, out var n))
                yield return new NumericClaim(ClaimKind.Calories, n, m.Index, m.Length);
    }

    private static decimal UnitMultiplier(string unit)
        => unit.ToLowerInvariant() is "k" or "kđ" or "ngàn" or "nghìn" ? 1000m : 1m;

    /// <summary>"80.000"/"80,000"/"80" — separators removed; decimal point tolerated.</summary>
    private static bool TryParseNumber(string raw, out decimal value)
    {
        var cleaned = raw.Replace(".", "").Replace(",", "").Replace(" ", "");
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool NearlyEqual(decimal claim, decimal expected)
        => Math.Abs(claim - expected) < 1m;

    private sealed class LlmAnswerWire
    {
        public string? Answer { get; set; }
        public List<string>? ProductRefs { get; set; }
        public List<string>? SourceIds { get; set; }
        public double? Confidence { get; set; }
    }
}
