using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Services
{
    /// <summary>
    /// Search Unification v2 (Pattern B → product search): FeaturedProducts matching
    /// mirrors TenantSearchService levels — L1 ILIKE contains (accent-sensitive fast
    /// path) → normalized in-memory contains → token → fuzzy ≥0.55 → DidYouMean
    /// khi best fuzzy &lt; 0.9. Bounded 500 candidates — featured list là curated nhỏ.
    /// Reuse NormalizeSearchText/SearchSimilarity của TenantSearchService — KHÔNG
    /// viết matcher riêng (governance anti-drift, mobile-search-ux-pattern.md).
    /// </summary>
    public interface IFeaturedProductSearchService
    {
        /// <summary>Match FeaturedProducts theo keyword. Empty/whitespace → tất cả
        /// active theo SortOrder+DisplayName (strategy "all").</summary>
        Task<FeaturedProductSearchResult> SearchAsync(string? q, CancellationToken ct = default);
    }

    /// <summary>Kết quả match — strategy: all | contains | token | fuzzy | suggested | none.
    /// DidYouMean chỉ set khi strategy=fuzzy và best score &lt; 0.9 (match chưa chắc).</summary>
    public sealed record FeaturedProductSearchResult(
        List<FeaturedProduct> Products,
        string MatchStrategy,
        string? DidYouMean);

    public sealed class FeaturedProductSearchService : IFeaturedProductSearchService
    {
        private const int FuzzyCandidateLimit = 500;
        private const double FuzzyThreshold = 0.55;
        private const double DidYouMeanMaxScore = 0.9;
        private const int MaxResults = 50;
        private const int SuggestedCount = 10;

        private readonly IVanAnDbContext _db;

        public FeaturedProductSearchService(IVanAnDbContext db) => _db = db;

        public async Task<FeaturedProductSearchResult> SearchAsync(string? q, CancellationToken ct = default)
        {
            var baseQuery = _db.FeaturedProducts
                .AsNoTracking()
                .IgnoreQueryFilters() // public endpoint — catalog browse không gắn tenant context
                .Where(f => f.IsActive);

            if (string.IsNullOrWhiteSpace(q))
            {
                var all = await baseQuery
                    .OrderBy(f => f.SortOrder).ThenBy(f => f.DisplayName)
                    .ToListAsync(ct);
                return new FeaturedProductSearchResult(all, "all", null);
            }

            var keyword = q.Trim();

            // ── Level 1: ILIKE contains (accent-sensitive fast path) ──
            var matched = await baseQuery
                .Where(f => EF.Functions.ILike(f.DisplayName, $"%{keyword}%"))
                .OrderBy(f => f.SortOrder).ThenBy(f => f.DisplayName)
                .ToListAsync(ct);

            if (matched.Count > 0)
                return new FeaturedProductSearchResult(matched, "contains", null);

            // ── Level 2+: normalized in-memory (Pattern B) ──
            var candidates = await baseQuery
                .OrderBy(f => f.DisplayName)
                .Take(FuzzyCandidateLimit)
                .ToListAsync(ct);

            var result = MatchCandidates(candidates, keyword);
            if (result.MatchStrategy != "none")
                return result;

            // ── Level 5: Suggested — tránh dead-end giống tenant search ──
            var suggested = await baseQuery
                .OrderBy(f => f.SortOrder).ThenBy(f => f.DisplayName)
                .Take(SuggestedCount)
                .ToListAsync(ct);
            return new FeaturedProductSearchResult(suggested, "suggested", null);
        }

        /// <summary>Static pure matcher — unit-testable không cần DB/ILIKE
        /// (SQLite test provider không chạy được EF.Functions.ILike).</summary>
        public static FeaturedProductSearchResult MatchCandidates(
            IReadOnlyList<FeaturedProduct> candidates, string keyword)
        {
            var qNorm = TenantSearchService.NormalizeSearchText(keyword);
            var qTokens = qNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(tok => tok.Length >= 2).ToList();

            // level: 2=contains-norm · 3=token · 4=fuzzy — level nhỏ nhất thắng.
            var hits = new List<(int Level, FeaturedProduct P, double Score)>();
            foreach (var p in candidates)
            {
                var n = TenantSearchService.NormalizeSearchText(p.DisplayName);
                if (n.Length == 0) continue;
                if (qNorm.Length > 0 && n.Contains(qNorm))
                    hits.Add((2, p, 1));
                else if (qTokens.Count > 0 && qTokens.Any(tok => n.Contains(tok)))
                    hits.Add((3, p, qTokens.Count(tok => n.Contains(tok))));
                else if (qNorm.Length >= 3)
                {
                    var sim = TenantSearchService.SearchSimilarity(qNorm, n);
                    if (sim >= FuzzyThreshold)
                        hits.Add((4, p, sim));
                }
            }

            if (hits.Count == 0)
                return new FeaturedProductSearchResult(new(), "none", null);

            var bestLevel = hits.Min(h => h.Level);
            var best = hits
                .Where(h => h.Level == bestLevel)
                .OrderByDescending(h => h.Score)
                .ThenBy(h => h.P.SortOrder)
                .ThenBy(h => h.P.DisplayName)
                .Take(MaxResults)
                .ToList();

            var strategy = bestLevel == 2 ? "contains" : bestLevel == 3 ? "token" : "fuzzy";
            // DidYouMean khi match chưa chắc (<0.9) — ≥0.9 gần như chắc → ẩn chip.
            var dym = bestLevel == 4 && best[0].Score < DidYouMeanMaxScore
                ? best[0].P.DisplayName
                : null;
            return new FeaturedProductSearchResult(best.Select(h => h.P).ToList(), strategy, dym);
        }
    }
}
