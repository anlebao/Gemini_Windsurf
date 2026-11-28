using VanAn.Shared.Helpers;

namespace VanAn.UI.Platform.Services
{
    /// <summary>
    /// Search load hardening (W2-A3 — task_search_personalized_didyoumean):
    /// client-side short-TTL search result cache — gõ rồi xóa rồi gõ lại không gọi
    /// API lần 2. Scoped (per-circuit/app lifetime), cap 50 entries LRU-ish,
    /// TTL 60s — mirror server ResponseCache ở search endpoints.
    /// Key = "scope|normalized(query)" qua <see cref="SearchTextNormalizer"/> —
    /// "Trà Sữa" và "tra sua" hit cùng entry.
    /// </summary>
    public sealed class SearchResultCache
    {
        private const int MaxEntries = 50;
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

        private readonly Dictionary<string, (object Value, DateTimeOffset Expires)> _map = new();

        private static string Key(string scope, string query)
            => scope + "|" + SearchTextNormalizer.Normalize(query);

        /// <summary>Hit → value + true; miss/expired → false (entry hết hạn bị xóa luôn).</summary>
        public bool TryGet<T>(string scope, string query, out T? value)
        {
            value = default;
            var key = Key(scope, query);
            if (!_map.TryGetValue(key, out var e)) return false;
            if (e.Expires <= DateTimeOffset.UtcNow)
            {
                _map.Remove(key);
                return false;
            }
            value = (T)e.Value;
            return true;
        }

        /// <summary>Lưu kết quả. Null bị bỏ qua (không cache lỗi/empty payload).</summary>
        public void Set<T>(string scope, string query, T value)
        {
            if (value is null) return;
            if (_map.Count >= MaxEntries)
            {
                // Evict entry hết hạn sớm nhất — đủ cho cache nhỏ 50 phần tử.
                var oldest = _map.MinBy(kv => kv.Value.Expires).Key;
                _map.Remove(oldest);
            }
            _map[Key(scope, query)] = (value, DateTimeOffset.UtcNow.Add(Ttl));
        }
    }
}
