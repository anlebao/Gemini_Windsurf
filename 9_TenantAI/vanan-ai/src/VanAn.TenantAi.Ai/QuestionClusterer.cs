namespace VanAn.TenantAi.Ai;

/// <summary>W3f — một cụm câu hỏi trùng/đồng nghĩa. Count = tổng số lần xuất hiện
/// (tín hiệu "khách hỏi nhiều lần"); Representative = câu thô xuất hiện nhiều nhất.</summary>
public sealed record QuestionCluster(string Representative, int Count, IReadOnlyList<string> Questions);

/// <summary>
/// W3f — chat learning suggest: gom câu hỏi NoData trùng/đồng nghĩa thành cụm
/// trước khi LLM soạn FAQ draft. Pure + deterministic (không EF, không LLM):
/// normalize (IntentRouter) → group chính xác → merge greedy theo Dice-bigram
/// (cùng họ QuestionSuggester/TenantSearchService — helper nhỏ duplicate chủ đích
/// vì vanan-ai là deployable riêng).
/// </summary>
public static class QuestionClusterer
{
    public const int MinQuestionLength = 4;
    public const int MaxQuestionLength = 200;

    /// <param name="similarityThreshold">Dice-bigram ≥ ngưỡng → merge vào cùng cụm (default 0.85 — chỉ gom typo/biến thể gần).</param>
    /// <param name="minSize">Cụm phải đạt tối thiểu số lần hỏi mới giữ lại (default 3 — tránh nhiễu).</param>
    public static IReadOnlyList<QuestionCluster> Cluster(
        IEnumerable<string>? questions,
        double similarityThreshold = 0.85,
        int minSize = 3)
    {
        if (questions is null) return [];

        // 1) normalize + lọc rác/ngắn/quá dài.
        var groups = new Dictionary<string, GroupBuilder>(StringComparer.Ordinal);
        foreach (var raw in questions)
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > MaxQuestionLength) continue;
            var norm = CollapseWhitespace(IntentRouter.Normalize(trimmed));
            if (norm.Length < MinQuestionLength) continue;
            if (!groups.TryGetValue(norm, out var g))
                groups[norm] = g = new GroupBuilder();
            g.Add(trimmed);
        }
        if (groups.Count == 0) return [];

        // 2) merge greedy: nhóm lớn trước, ghép vào cụm đầu tiên đủ tương đồng.
        var clusters = new List<GroupBuilder>();
        foreach (var g in groups.Values.OrderByDescending(g => g.Count))
        {
            var target = clusters.FirstOrDefault(c =>
                BigramSimilarity(c.RepresentativeNorm, g.RepresentativeNorm) >= similarityThreshold);
            if (target != null) target.Merge(g);
            else clusters.Add(g);
        }

        // 3) lọc minSize + sort ổn định (count desc, rồi representative).
        return clusters
            .Where(c => c.Count >= minSize)
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Representative, StringComparer.Ordinal)
            .Select(c => new QuestionCluster(c.Representative, c.Count, c.Questions))
            .ToList();
    }

    private static string CollapseWhitespace(string s)
        => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Sørensen–Dice over character bigrams — same family as QuestionSuggester.</summary>
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

    /// <summary>Builder per nhóm/chùm: theo dõi count + representative (raw xuất hiện nhiều nhất).</summary>
    private sealed class GroupBuilder
    {
        private readonly Dictionary<string, int> _rawCounts = new(StringComparer.Ordinal);
        private int _repCount;

        public string RepresentativeNorm { get; private set; } = string.Empty;
        public string Representative { get; private set; } = string.Empty;
        public int Count { get; private set; }

        public void Add(string raw)
        {
            Count++;
            var cnt = _rawCounts.GetValueOrDefault(raw) + 1;
            _rawCounts[raw] = cnt;
            if (cnt > _repCount)
            {
                _repCount = cnt;
                Representative = raw;
                RepresentativeNorm = CollapseWhitespace(IntentRouter.Normalize(raw));
            }
        }

        public void Merge(GroupBuilder other)
        {
            Count += other.Count;
            if (other._repCount > _repCount)
            {
                _repCount = other._repCount;
                Representative = other.Representative;
                RepresentativeNorm = other.RepresentativeNorm;
            }
            foreach (var (raw, cnt) in other._rawCounts)
                _rawCounts[raw] = _rawCounts.GetValueOrDefault(raw) + cnt;
        }

        public IReadOnlyList<string> Questions => _rawCounts.Keys.ToList();
    }
}