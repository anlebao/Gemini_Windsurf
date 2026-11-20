using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Shared slug helpers for tenant storefront URLs (/store/{slug}).
    /// Extracted from TenantOnboardingService so EVERY tenant-creation path
    /// (onboarding verify, OCR, merchant signup, admin create) produces a slug —
    /// directory "Xem trang cửa hàng" button depends on Settings.Slug being set.
    /// </summary>
    public static class SlugHelper
    {
        /// <summary>Kiểm tra slug đã tồn tại trên tenant khác (IgnoreQueryFilters — toàn hệ thống).</summary>
        public static Task<bool> SlugExistsAsync(
            IVanAnDbContext dbContext,
            string slug,
            TenantId excludeTenantId,
            CancellationToken ct)
            => dbContext.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Settings.Slug == slug && t.Id != excludeTenantId, ct);

        /// <summary>
        /// Auto slug từ tên tenant, collision-safe: base → -2/-3/... (tối đa 20) → random suffix.
        /// Dùng cho mọi creation path không có user-provided slug.
        /// </summary>
        public static async Task<string> GenerateUniqueSlugAsync(
            IVanAnDbContext dbContext,
            string name,
            TenantId excludeTenantId,
            CancellationToken ct)
        {
            string baseSlug = Slugify(name);
            string slug = baseSlug;
            for (int i = 2; i <= 20 && await SlugExistsAsync(dbContext, slug, excludeTenantId, ct); i++)
                slug = $"{baseSlug}-{i}";
            if (await SlugExistsAsync(dbContext, slug, excludeTenantId, ct))
                slug = $"{baseSlug}-{Guid.NewGuid().ToString("N")[..4]}";
            return slug;
        }

        /// <summary>
        /// Explicit Vietnamese diacritics → ASCII map.
        /// Issue #180 (round 2): the Gateway runs with System.Globalization.Invariant=true
        /// (VanAn.Gateway.runtimeconfig.json) → Normalize(FormD) + \p{Mn} are NO-OPs on the
        /// deployed runtime, so NFD-based stripping silently fails there while passing on dev.
        /// An explicit map works in every globalization mode and also covers "đ" (U+0111 —
        /// a single precomposed letter that NFD never decomposes).
        /// </summary>
        private static readonly Dictionary<char, string> VietnameseDiacriticsMap = BuildVietnameseDiacriticsMap();

        private static Dictionary<char, string> BuildVietnameseDiacriticsMap()
        {
            var map = new Dictionary<char, string>();
            void Add(string from, string to)
            {
                foreach (var c in from) map[c] = to;
            }
            Add("áàảãạăắằẳẵặâấầẩẫậ", "a");
            Add("ÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬ", "A");
            Add("éèẻẽẹêếềểễệ", "e");
            Add("ÉÈẺẼẸÊẾỀỂỄỆ", "E");
            Add("íìỉĩị", "i");
            Add("ÍÌỈĨỊ", "I");
            Add("óòỏõọôốồổỗộơớờởỡợ", "o");
            Add("ÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢ", "O");
            Add("úùủũụưứừửữự", "u");
            Add("ÚÙỦŨỤƯỨỪỬỮỰ", "U");
            Add("ýỳỷỹỵ", "y");
            Add("ÝỲỶỸỴ", "Y");
            Add("đ", "d");
            Add("Đ", "D");
            return map;
        }

        public static string Slugify(string name)
        {
            // Simple slugify: lowercase, strip Vietnamese diacritics, replace spaces with hyphens.
            // 1) NFD + \p{Mn} — best-effort for non-Vietnamese accents (no-op under invariant globalization).
            // 2) Explicit Vietnamese map — works in EVERY globalization mode (invariant runtime included).
            // 3) Keep ONLY ASCII [a-z0-9 -] — deterministic; \w would let non-ASCII letters through.
            // Result always satisfies Tenant.UpdateSlug regex (^[a-z0-9]+(?:-[a-z0-9]+)*$).
            var slug = name.Trim().ToLowerInvariant();
            slug = slug.Normalize(System.Text.NormalizationForm.FormD);
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\p{Mn}", "");
            var sb = new System.Text.StringBuilder(slug.Length);
            foreach (var ch in slug)
            {
                sb.Append(VietnameseDiacriticsMap.TryGetValue(ch, out var rep) ? rep : ch);
            }
            slug = sb.ToString();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? $"tenant-{Guid.NewGuid():N}"[..30] : slug[..Math.Min(slug.Length, 100)];
        }
    }
}
