using VanAn.Gateway.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Integration.Tests.KhachLink
{
    /// <summary>
    /// Search Unification v2: unit tests cho FeaturedProductSearchService.MatchCandidates —
    /// pure static matcher (SQLite test provider không chạy được EF.Functions.ILike nên
    /// pipeline L2+ được tách ra test trực tiếp, giống TenantSearchMatchingTests).
    /// Levels: 2=contains-norm · 3=token · 4=fuzzy(≥0.55) → DidYouMean khi best &lt; 0.9.
    /// </summary>
    [Trait("Category", "Unit")]
    public class CatalogSearchMatchingTests
    {
        private static readonly TenantId TestTenantId = new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

        private static FeaturedProduct P(string displayName, int sortOrder = 0)
            => FeaturedProduct.Create(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), displayName, 10000m, sortOrder: sortOrder);

        [Fact]
        public void MatchCandidates_NormalizedContains_NoDiacriticsQuery()
        {
            // "tra sua" (không dấu) phải khớp "Trà Sữa 79" ở level contains-norm.
            var result = FeaturedProductSearchService.MatchCandidates(
                new[] { P("Trà Sữa 79"), P("Bánh Mì") }, "tra sua");

            Assert.Equal("contains", result.MatchStrategy);
            Assert.Single(result.Products);
            Assert.Equal("Trà Sữa 79", result.Products[0].DisplayName);
            Assert.Null(result.DidYouMean);
        }

        [Fact]
        public void MatchCandidates_TokenLevel_AnyTokenHit()
        {
            // Token-match: query "sua da" khớp "Trà Sữa Đá" qua token sau normalize.
            var result = FeaturedProductSearchService.MatchCandidates(
                new[] { P("Trà Sữa Đá"), P("Bánh Mì") }, "sua khong co");

            Assert.Equal("token", result.MatchStrategy);
            Assert.Equal("Trà Sữa Đá", result.Products[0].DisplayName);
            Assert.Null(result.DidYouMean);
        }

        [Fact]
        public void MatchCandidates_FuzzyTypo_SetsDidYouMean()
        {
            // Sai chính tả nhẹ "samhoo" → fuzzy match "samho" (score ~0.83 <0.9) → chip gợi ý.
            // Lưu ý: candidate "Samho Coffee" KHÔNG fuzzy (" coffee" kéo full-string sim <0.55),
            // "samhoo" cũng không substring/token → pure fuzzy path.
            var result = FeaturedProductSearchService.MatchCandidates(
                new[] { P("samho"), P("Bánh Mì") }, "samhoo");

            Assert.Equal("fuzzy", result.MatchStrategy);
            Assert.Single(result.Products);
            Assert.Equal("samho", result.DidYouMean);
        }

        [Fact]
        public void MatchCandidates_FuzzyExactNormalized_HidesDidYouMean()
        {
            // Fuzzy hit nhưng score ≥0.9 (gần như chắc) → không cần user confirm.
            var result = FeaturedProductSearchService.MatchCandidates(
                new[] { P("samho") }, "samho");

            Assert.NotEqual("none", result.MatchStrategy);
            Assert.Null(result.DidYouMean);
        }

        [Fact]
        public void MatchCandidates_NoMatch_ReturnsNone()
        {
            var result = FeaturedProductSearchService.MatchCandidates(
                new[] { P("Trà Sữa"), P("Bánh Mì") }, "xylophone");

            Assert.Equal("none", result.MatchStrategy);
            Assert.Empty(result.Products);
            Assert.Null(result.DidYouMean);
        }

        [Fact]
        public void MatchCandidates_BestLevelWins_ContainsBeatsFuzzy()
        {
            // Cùng query: 1 candidate contains-norm, 1 candidate chỉ fuzzy → contains thắng,
            // fuzzy candidate bị loại (level nhỏ nhất thắng).
            var result = FeaturedProductSearchService.MatchCandidates(
                new[] { P("Trà Sữa 79"), P("Trar Suur") }, "tra sua");

            Assert.Equal("contains", result.MatchStrategy);
            Assert.Single(result.Products);
            Assert.Equal("Trà Sữa 79", result.Products[0].DisplayName);
        }

        [Fact]
        public void MatchCandidates_FuzzyOrdersByScoreDesc()
        {
            // 2 candidates đều chỉ fuzzy ("samhoo" không substring/token của cái nào) —
            // "samho" (sim~0.83) phải xếp trên "samo" (sim thấp hơn), DidYouMean = top scorer.
            var result = FeaturedProductSearchService.MatchCandidates(
                new[] { P("samo"), P("samho") }, "samhoo");

            Assert.Equal("fuzzy", result.MatchStrategy);
            Assert.Equal("samho", result.Products[0].DisplayName);
            Assert.Equal(result.Products[0].DisplayName, result.DidYouMean);
        }

        [Fact]
        public void MatchCandidates_EmptyCandidates_ReturnsNone()
        {
            var result = FeaturedProductSearchService.MatchCandidates(
                Array.Empty<FeaturedProduct>(), "tra sua");

            Assert.Equal("none", result.MatchStrategy);
        }

        [Fact]
        public void MatchCandidates_MultiWordQuery_NormalizedTokenFallback()
        {
            // Query không dấu nhiều từ: "com ga" vs "Cơm Gà Xối Mỡ" → contains-norm level.
            var result = FeaturedProductSearchService.MatchCandidates(
                new[] { P("Cơm Gà Xối Mỡ"), P("Phở Bò") }, "com ga");

            Assert.Equal("contains", result.MatchStrategy);
            Assert.Equal("Cơm Gà Xối Mỡ", result.Products[0].DisplayName);
        }
    }
}
