using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using VanAn.Shared.Helpers;

namespace VanAn.UI.Platform.Services
{
    /// <summary>
    /// Personalized DidYouMean (SRS v1.1 — client-side self-learning):
    /// localStorage <c>vanan_search_corrections</c> — per-scope map
    /// misspelled_norm → { correctedNorm(c), correctedDisplay(d), acceptCount(n), lastUsed(t), source(s) }.
    /// Record khi user accept chip DidYouMean; Resolve trước mỗi search (Level 0);
    /// Forget khi user undo. LRU 200/scope, TTL 180d, fail-open toàn bộ lỗi storage
    /// (private mode/quota/corrupt → feature tắt êm, search vẫn chạy bình thường).
    /// Normalize dùng chung SearchTextNormalizer (1_Shared) — KHÔNG viết lại.
    /// </summary>
    public sealed class SearchCorrectionStore(IJSRuntime js)
    {
        public const string ScopeProduct = "product";
        public const string ScopeTenant = "tenant";

        private const string StorageKey = "vanan_search_corrections";
        private const int MaxEntriesPerScope = 200;
        private const int MinAccepts = 1;
        private const long TtlMs = 180L * 24 * 60 * 60 * 1000;

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        private CorrectionRoot? _cache;

        private sealed class CorrectionEntry
        {
            [JsonPropertyName("c")] public string CorrectedNorm { get; set; } = "";
            [JsonPropertyName("d")] public string CorrectedDisplay { get; set; } = "";
            [JsonPropertyName("n")] public int AcceptCount { get; set; }
            [JsonPropertyName("t")] public long LastUsed { get; set; }
            [JsonPropertyName("s")] public string Source { get; set; } = "unknown";
        }

        private sealed class CorrectionRoot
        {
            [JsonPropertyName("v")] public int Version { get; set; } = 1;
            [JsonPropertyName("scopes")] public Dictionary<string, Dictionary<string, CorrectionEntry>> Scopes { get; set; } = new();
        }

        /// <summary>Level 0: query (raw) → corrected display nếu user đã học cặp này.
        /// Null khi chưa học / hết hạn / storage lỗi.</summary>
        public async Task<string?> ResolveAsync(string scope, string query, CancellationToken ct = default)
        {
            var norm = SearchTextNormalizer.Normalize(query);
            if (norm.Length == 0) return null;

            var root = await LoadAsync();
            if (!root.Scopes.TryGetValue(scope, out var map) || !map.TryGetValue(norm, out var e))
                return null;

            var now = NowMs();
            if (now - e.LastUsed > TtlMs)
            {
                map.Remove(norm);
                await SaveAsync(root); // prune expired — self-hygiene, không cần job
                return null;
            }
            if (e.AcceptCount < MinAccepts) return null;

            e.LastUsed = now; // LRU recency
            await SaveAsync(root);
            return e.CorrectedDisplay;
        }

        /// <summary>Ghi nhận user accept DidYouMean: (scope, raw → suggestion).
        /// Normalize cả 2 phía; raw == suggestion sau normalize → no-op.</summary>
        public async Task RecordAsync(string scope, string rawQuery, string suggestion, string source, CancellationToken ct = default)
        {
            var normRaw = SearchTextNormalizer.Normalize(rawQuery);
            var normSug = SearchTextNormalizer.Normalize(suggestion);
            if (normRaw.Length == 0 || normSug.Length == 0 || normRaw == normSug) return;

            var root = await LoadAsync();
            if (!root.Scopes.TryGetValue(scope, out var map))
                map = root.Scopes[scope] = new Dictionary<string, CorrectionEntry>();

            var now = NowMs();
            if (map.TryGetValue(normRaw, out var e))
            {
                e.AcceptCount++;
                e.CorrectedNorm = normSug;
                e.CorrectedDisplay = suggestion.Trim();
                e.LastUsed = now;
                if (!string.IsNullOrEmpty(source)) e.Source = source;
            }
            else
            {
                map[normRaw] = new CorrectionEntry
                {
                    CorrectedNorm = normSug,
                    CorrectedDisplay = suggestion.Trim(),
                    AcceptCount = 1,
                    LastUsed = now,
                    Source = string.IsNullOrEmpty(source) ? "unknown" : source
                };
            }

            // LRU cap — evict entry lastUsed cũ nhất khi vượt giới hạn
            while (map.Count > MaxEntriesPerScope)
            {
                var oldest = map.Aggregate((a, b) => a.Value.LastUsed < b.Value.LastUsed ? a : b).Key;
                map.Remove(oldest);
            }

            await SaveAsync(root);
        }

        /// <summary>Undo — xóa correction user không còn muốn.</summary>
        public async Task ForgetAsync(string scope, string query, CancellationToken ct = default)
        {
            var norm = SearchTextNormalizer.Normalize(query);
            if (norm.Length == 0) return;

            var root = await LoadAsync();
            if (root.Scopes.TryGetValue(scope, out var map) && map.Remove(norm))
                await SaveAsync(root);
        }

        /// <summary>Load + parse 1 lần, cache in-memory. Corrupt/denied → store rỗng.</summary>
        private async Task<CorrectionRoot> LoadAsync()
        {
            if (_cache is not null) return _cache;
            try
            {
                var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                _cache = string.IsNullOrEmpty(json)
                    ? new CorrectionRoot()
                    : JsonSerializer.Deserialize<CorrectionRoot>(json, JsonOpts) ?? new CorrectionRoot();
            }
            catch { _cache = new CorrectionRoot(); }
            return _cache;
        }

        /// <summary>Persist — lỗi nuốt êm (in-memory cache vẫn phục vụ phiên hiện tại).</summary>
        private async Task SaveAsync(CorrectionRoot root)
        {
            try
            {
                await js.InvokeVoidAsync("localStorage.setItem", StorageKey,
                    JsonSerializer.Serialize(root, JsonOpts));
            }
            catch { /* quota/denied — fail-open */ }
        }

        private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
