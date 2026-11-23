using System.Text;

namespace VanAn.TenantAi.Ai;

/// <summary>
/// W3e — "did you mean" suggester for the chat pipeline. Fuzzy-matches the raw
/// customer question against the tenant's known vocabulary (product names +
/// approved public knowledge doc titles) so a typo'd/unclear question can be
/// corrected with a one-tap suggestion instead of a dead-end "no data" answer.
///
/// Pure + deterministic; invoked only on the no-facts path (cheap — bounded
/// candidate set, short strings). Mirrors the normalize + Dice-bigram approach
/// of Gateway TenantSearchService; vanan-ai is a separate deployable so the
/// small helper is duplicated intentionally rather than shared cross-service.
/// </summary>
public static class QuestionSuggester
{
    /// <summary>Mean best-token similarity across candidate tokens must reach this.</summary>
    private const double MinScore = 0.70;

    /// <summary>At least one token pair must be this similar — guards 1-weak-token matches.</summary>
    private const double MinPairSimilarity = 0.75;

    public static string? Suggest(string? question, IEnumerable<string>? candidates)
    {
        if (string.IsNullOrWhiteSpace(question) || candidates is null) return null;

        var qTokens = Tokenize(IntentRouter.Normalize(question));
        if (qTokens.Count == 0) return null;

        string? best = null;
        var bestScore = 0.0;
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var cTokens = Tokenize(IntentRouter.Normalize(candidate));
            if (cTokens.Count == 0) continue;

            // Already named (near-)verbatim → nothing to correct; retrieval owns it.
            if (ContainsSequence(qTokens, cTokens)) continue;

            var (score, bestPair) = Score(qTokens, cTokens);
            if (bestPair >= MinPairSimilarity && score >= MinScore && score > bestScore)
            {
                bestScore = score;
                best = candidate.Trim();
            }
        }
        return best;
    }

    private static List<string> Tokenize(string normalized)
        => normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .ToList();

    /// <summary>True when every candidate token appears contiguously in the question tokens.</summary>
    private static bool ContainsSequence(List<string> qTokens, List<string> cTokens)
    {
        if (cTokens.Count > qTokens.Count) return false;
        for (var i = 0; i + cTokens.Count <= qTokens.Count; i++)
        {
            var match = true;
            for (var j = 0; j < cTokens.Count; j++)
                if (qTokens[i + j] != cTokens[j]) { match = false; break; }
            if (match) return true;
        }
        return false;
    }

    /// <summary>
    /// score = mean over candidate tokens of their best bigram similarity to any
    /// question token; bestPair = the single strongest pair (threshold gate).
    /// </summary>
    private static (double Score, double BestPair) Score(List<string> qTokens, List<string> cTokens)
    {
        var total = 0.0;
        var bestPair = 0.0;
        foreach (var cToken in cTokens)
        {
            var best = 0.0;
            foreach (var qToken in qTokens)
                best = Math.Max(best, BigramSimilarity(cToken, qToken));
            total += best;
            bestPair = Math.Max(bestPair, best);
        }
        return (total / cTokens.Count, bestPair);
    }

    /// <summary>Sørensen–Dice coefficient over character bigrams (same family as TenantSearchService).</summary>
    private static double BigramSimilarity(string a, string b)
    {
        if (a == b) return 1.0;
        if (a.Length < 2 || b.Length < 2) return 0.0;

        var ba = new HashSet<string>(a.Length - 1, StringComparer.Ordinal);
        var bb = new HashSet<string>(b.Length - 1, StringComparer.Ordinal);
        for (var i = 0; i < a.Length - 1; i++) ba.Add(a.Substring(i, 2));
        for (var i = 0; i < b.Length - 1; i++) bb.Add(b.Substring(i, 2));

        var inter = ba.Count(bb.Contains);
        return 2.0 * inter / (ba.Count + bb.Count);
    }
}
