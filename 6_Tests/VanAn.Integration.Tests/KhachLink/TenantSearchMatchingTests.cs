using VanAn.Gateway.Services;
using Xunit;

namespace VanAn.Integration.Tests.KhachLink
{
    /// <summary>
    /// W3-2d: unit tests cho Pattern B matching helpers (TenantSearchService).
    /// Helpers là public static — test trực tiếp không cần DB.
    /// Lưu ý: prod chạy System.Globalization.Invariant → Normalize(FormD) NO-OP,
    /// fold map thủ công là bắt buộc (xem mobile-search-ux-pattern.md).
    /// </summary>
    [Trait("Category", "Unit")]
    public class TenantSearchMatchingTests
    {
        [Theory]
        [InlineData("Trà Sữa 79", "tra sua 79")]
        [InlineData("Cửa Hàng", "cua hang")]
        [InlineData("Đức Nghĩa", "duc nghia")]
        [InlineData("Sửa Chữa Ô Tô", "sua chua o to")]
        [InlineData("HỢP TÁC XÃ VẠN AN", "hop tac xa van an")]
        [InlineData("  An   Tâm  ", "an tam")]
        [InlineData("Cafe\tTrung  Nguyên", "cafe trung nguyen")]
        public void NormalizeSearchText_StripsVietnameseDiacritics(string input, string expected)
        {
            Assert.Equal(expected, TenantSearchService.NormalizeSearchText(input));
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        public void NormalizeSearchText_EmptyInput_ReturnsEmpty(string? input, string expected)
        {
            Assert.Equal(expected, TenantSearchService.NormalizeSearchText(input));
        }

        [Theory]
        // không dấu gõ liền vs tên có dấu nhiều từ
        [InlineData("antam", "an tam", 0.55)]
        [InlineData("trasua", "tra sua 79", 0.55)]
        // sai chính tả nhẹ
        [InlineData("samho", "samho coffee", 0.55)]
        [InlineData("samhoo", "samho", 0.55)]
        // exact sau normalize → 1.0
        [InlineData("tra sua", "tra sua", 0.99)]
        public void SearchSimilarity_TypoAndNoDiacritics_MeetsThreshold(string query, string name, double minScore)
        {
            var qNorm = TenantSearchService.NormalizeSearchText(query);
            var nNorm = TenantSearchService.NormalizeSearchText(name);
            Assert.True(TenantSearchService.SearchSimilarity(qNorm, nNorm) >= minScore,
                $"similarity({query},{name}) = {TenantSearchService.SearchSimilarity(qNorm, nNorm)} < {minScore}");
        }

        [Fact]
        public void SearchSimilarity_IdenticalNormalized_ReturnsOne()
        {
            Assert.Equal(1.0, TenantSearchService.SearchSimilarity("an tam", "an tam"));
        }

        [Fact]
        public void SearchSimilarity_Unrelated_ReturnsLow()
        {
            var sim = TenantSearchService.SearchSimilarity("sxmxo", "hoang long");
            Assert.True(sim < 0.55, $"unrelated similarity should be < 0.55, got {sim}");
        }

        [Theory]
        [InlineData("kitten", "sitting", 3)]
        [InlineData("", "abc", 3)]
        [InlineData("abc", "", 3)]
        [InlineData("same", "same", 0)]
        public void LevenshteinDistance_BasicCases(string a, string b, int expected)
        {
            Assert.Equal(expected, TenantSearchService.LevenshteinDistance(a, b));
        }
    }
}
