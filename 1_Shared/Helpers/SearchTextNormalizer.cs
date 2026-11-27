using System.Text;

namespace VanAn.Shared.Helpers
{
    /// <summary>
    /// Shared Vietnamese-aware search normalization + similarity — single source
    /// cho mọi search surface (governance anti-drift, mobile-search-ux-pattern.md).
    /// Extracted từ TenantSearchService (2_Gateway) để client WASM/Directory reuse
    /// (vd Personalized DidYouMean Level-0 client lookup) — Gateway delegate lại.
    /// LƯU Ý: runtime prod bật System.Globalization.Invariant → Normalize(FormD) là NO-OP,
    /// nên PHẢI fold thủ công qua bảng map — KHÔNG dùng FormD/CharUnicodeInfo.
    /// </summary>
    public static class SearchTextNormalizer
    {
        private static readonly Dictionary<char, char> DiacriticFold = BuildDiacriticFold();

        private static Dictionary<char, char> BuildDiacriticFold()
        {
            var map = new Dictionary<char, char>();
            void Add(string accented, char basis)
            {
                foreach (var c in accented)
                    map[c] = basis;
                foreach (var c in accented.ToUpperInvariant())
                    map[c] = basis;
            }
            Add("àáảãạăằắẳẵặâầấẩẫậ", 'a');
            Add("èéẻẽẹêềếểễệ", 'e');
            Add("ìíỉĩị", 'i');
            Add("òóỏõọôồốổỗộơờớởỡợ", 'o');
            Add("ùúủũụưừứửữự", 'u');
            Add("ỳýỷỹỵ", 'y');
            Add("đ", 'd');
            // Latin-1 bổ sung — search robust cho tên không phải tiếng Việt.
            Add("äå", 'a'); Add("ç", 'c'); Add("ë", 'e'); Add("îï", 'i'); Add("ñ", 'n');
            Add("öø", 'o'); Add("üû", 'u'); Add("ÿ", 'y');
            return map;
        }

        /// <summary>Bỏ dấu tiếng Việt + lowercase + collapse spaces.
        /// null separator → split mọi whitespace (space/tab/newline), collapse về 1 space.</summary>
        public static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                var lower = char.ToLowerInvariant(c);
                sb.Append(DiacriticFold.TryGetValue(lower, out var basis) ? basis : lower);
            }
            return string.Join(' ', sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>Similarity 0..1 = max(levenshtein ratio full-string, token Dice coefficient).</summary>
        public static double Similarity(string qNorm, string nameNorm)
        {
            if (qNorm.Length == 0 || nameNorm.Length == 0) return 0;

            var maxLen = Math.Max(qNorm.Length, nameNorm.Length);
            var levRatio = 1.0 - (double)Levenshtein(qNorm, nameNorm) / maxLen;

            var qTokens = qNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var nTokens = nameNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var intersection = qTokens.Count(nTokens.Contains);
            var dice = qTokens.Count + nTokens.Count == 0
                ? 0
                : 2.0 * intersection / (qTokens.Count + nTokens.Count);

            return Math.Max(levRatio, dice);
        }

        /// <summary>Levenshtein distance (DP, 2 rows) — strings ngắn nên đủ nhanh.</summary>
        public static int Levenshtein(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;
            var prev = new int[b.Length + 1];
            var curr = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) prev[j] = j;
            for (var i = 1; i <= a.Length; i++)
            {
                curr[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }
            return prev[b.Length];
        }
    }
}
