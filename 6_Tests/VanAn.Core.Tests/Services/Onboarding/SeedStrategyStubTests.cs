using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.CoreHub.Services.Onboarding.Strategies;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services.Onboarding
{
    /// <summary>
    /// Unit tests for Wave 1 stub seed strategies.
    /// Verifies each stub: correct IndustryCode, IndustryName, zero-counts, and warning message.
    /// Also verifies industry codes are globally unique.
    ///
    /// OBSOLETE TESTS (Skip): The "zero-counts" and "no DbContext calls" tests below are skipped
    /// because commit f40d162b (Quick-Setup real seeding) replaced stub implementations with real
    /// seeding strategies. These tests still validate IndustryCode/IndustryName (still valid) but
    /// the SeedAsync behavior tests are obsolete. They should be replaced with real-seeding tests
    /// in a future cleanup batch.
    /// </summary>
    public class SeedStrategyStubTests
    {
        private static readonly TenantId TestTenantId = new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        private readonly Mock<IVanAnDbContext> _dbContextMock = new();

        private static IEnumerable<IIndustrySeedStrategy> AllStubs() =>
        [
            new SpaSeedStrategy(),
            new HotelSeedStrategy(),
            new BarberSeedStrategy(),
            new ClothesSeedStrategy(),
            new HealthySeedStrategy(),
            new PetShopSeedStrategy(),
        ];

        // ── Industry code uniqueness ─────────────────────────────────────────────

        [Fact]
        public void AllStubs_IndustryCodes_ShouldBeUnique()
        {
            var codes = AllStubs().Select(s => s.IndustryCode).ToList();
            Assert.Equal(codes.Count, codes.Distinct().Count());
        }

        [Fact]
        public void AllStubs_IndustryCodes_ShouldBeNonEmpty()
        {
            foreach (var stub in AllStubs())
                Assert.False(string.IsNullOrWhiteSpace(stub.IndustryCode),
                    $"{stub.GetType().Name}.IndustryCode should not be empty");
        }

        [Fact]
        public void AllStubs_IndustryNames_ShouldBeNonEmpty()
        {
            foreach (var stub in AllStubs())
                Assert.False(string.IsNullOrWhiteSpace(stub.IndustryName),
                    $"{stub.GetType().Name}.IndustryName should not be empty");
        }

        // ── SpaSeedStrategy ──────────────────────────────────────────────────────

        [Fact]
        public void SpaSeedStrategy_IndustryCode_ShouldBeSPA()
        {
            Assert.Equal("SPA", new SpaSeedStrategy().IndustryCode);
        }

        [Fact(Skip = "Obsolete: SpaSeedStrategy now implements real seeding (commit f40d162b).")]
        public async Task SpaSeedStrategy_SeedAsync_ReturnsZeroCounts_AndWarning()
        {
            var result = await new SpaSeedStrategy().SeedAsync(TestTenantId, _dbContextMock.Object);

            Assert.Equal(0, result.ProductsCreated);
            Assert.Equal(0, result.IngredientsCreated);
            Assert.Equal(0, result.RecipesCreated);
            Assert.Equal(0, result.ShopsCreated);
            Assert.Single(result.Warnings);
            Assert.Contains("not yet implemented", result.Warnings[0]);
        }

        // ── HotelSeedStrategy ────────────────────────────────────────────────────

        [Fact]
        public void HotelSeedStrategy_IndustryCode_ShouldBeHOTEL()
        {
            Assert.Equal("HOTEL", new HotelSeedStrategy().IndustryCode);
        }

        [Fact(Skip = "Obsolete: HotelSeedStrategy now implements real seeding (commit f40d162b).")]
        public async Task HotelSeedStrategy_SeedAsync_ReturnsZeroCounts_AndWarning()
        {
            var result = await new HotelSeedStrategy().SeedAsync(TestTenantId, _dbContextMock.Object);

            Assert.Equal(0, result.ProductsCreated);
            Assert.Equal(0, result.IngredientsCreated);
            Assert.Equal(0, result.RecipesCreated);
            Assert.Equal(0, result.ShopsCreated);
            Assert.Single(result.Warnings);
            Assert.Contains("not yet implemented", result.Warnings[0]);
        }

        // ── BarberSeedStrategy ───────────────────────────────────────────────────

        [Fact]
        public void BarberSeedStrategy_IndustryCode_ShouldBeBarber()
        {
            Assert.Equal("BARBER", new BarberSeedStrategy().IndustryCode);
        }

        [Fact(Skip = "Obsolete: BarberSeedStrategy now implements real seeding (commit f40d162b).")]
        public async Task BarberSeedStrategy_SeedAsync_ReturnsZeroCounts_AndWarning()
        {
            var result = await new BarberSeedStrategy().SeedAsync(TestTenantId, _dbContextMock.Object);

            Assert.Equal(0, result.ProductsCreated);
            Assert.Equal(0, result.IngredientsCreated);
            Assert.Equal(0, result.RecipesCreated);
            Assert.Equal(0, result.ShopsCreated);
            Assert.Single(result.Warnings);
            Assert.Contains("not yet implemented", result.Warnings[0]);
        }

        // ── ClothesSeedStrategy ──────────────────────────────────────────────────

        [Fact]
        public void ClothesSeedStrategy_IndustryCode_ShouldBeClothes()
        {
            Assert.Equal("CLOTHES", new ClothesSeedStrategy().IndustryCode);
        }

        [Fact(Skip = "Obsolete: ClothesSeedStrategy now implements real seeding (commit f40d162b).")]
        public async Task ClothesSeedStrategy_SeedAsync_ReturnsZeroCounts_AndWarning()
        {
            var result = await new ClothesSeedStrategy().SeedAsync(TestTenantId, _dbContextMock.Object);

            Assert.Equal(0, result.ProductsCreated);
            Assert.Equal(0, result.IngredientsCreated);
            Assert.Equal(0, result.RecipesCreated);
            Assert.Equal(0, result.ShopsCreated);
            Assert.Single(result.Warnings);
            Assert.Contains("not yet implemented", result.Warnings[0]);
        }

        // ── HealthySeedStrategy ──────────────────────────────────────────────────

        [Fact]
        public void HealthySeedStrategy_IndustryCode_ShouldBeHealthy()
        {
            Assert.Equal("HEALTHY", new HealthySeedStrategy().IndustryCode);
        }

        [Fact(Skip = "Obsolete: HealthySeedStrategy now implements real seeding (commit f40d162b).")]
        public async Task HealthySeedStrategy_SeedAsync_ReturnsZeroCounts_AndWarning()
        {
            var result = await new HealthySeedStrategy().SeedAsync(TestTenantId, _dbContextMock.Object);

            Assert.Equal(0, result.ProductsCreated);
            Assert.Equal(0, result.IngredientsCreated);
            Assert.Equal(0, result.RecipesCreated);
            Assert.Equal(0, result.ShopsCreated);
            Assert.Single(result.Warnings);
            Assert.Contains("not yet implemented", result.Warnings[0]);
        }

        // ── PetShopSeedStrategy ──────────────────────────────────────────────────

        [Fact]
        public void PetShopSeedStrategy_IndustryCode_ShouldBePetShop()
        {
            Assert.Equal("PETSHOP", new PetShopSeedStrategy().IndustryCode);
        }

        [Fact(Skip = "Obsolete: PetShopSeedStrategy now implements real seeding (commit f40d162b).")]
        public async Task PetShopSeedStrategy_SeedAsync_ReturnsZeroCounts_AndWarning()
        {
            var result = await new PetShopSeedStrategy().SeedAsync(TestTenantId, _dbContextMock.Object);

            Assert.Equal(0, result.ProductsCreated);
            Assert.Equal(0, result.IngredientsCreated);
            Assert.Equal(0, result.RecipesCreated);
            Assert.Equal(0, result.ShopsCreated);
            Assert.Single(result.Warnings);
            Assert.Contains("not yet implemented", result.Warnings[0]);
        }

        // ── All stubs via theory ─────────────────────────────────────────────────

        public static IEnumerable<object[]> AllStubsTheoryData() =>
            AllStubs().Select(s => new object[] { s });

        [Theory(Skip = "Obsolete: strategies now implement real seeding (commit f40d162b).")]
        [MemberData(nameof(AllStubsTheoryData))]
        public async Task AllStubs_SeedAsync_ShouldReturnZeroCounts(IIndustrySeedStrategy strategy)
        {
            var result = await strategy.SeedAsync(TestTenantId, _dbContextMock.Object);

            Assert.Equal(0, result.ProductsCreated);
            Assert.Equal(0, result.IngredientsCreated);
            Assert.Equal(0, result.RecipesCreated);
            Assert.Equal(0, result.ShopsCreated);
        }

        [Theory(Skip = "Obsolete: strategies now implement real seeding (commit f40d162b).")]
        [MemberData(nameof(AllStubsTheoryData))]
        public async Task AllStubs_SeedAsync_ShouldReturnWarningContainingIndustryName(IIndustrySeedStrategy strategy)
        {
            var result = await strategy.SeedAsync(TestTenantId, _dbContextMock.Object);

            Assert.Single(result.Warnings);
            Assert.Contains(strategy.IndustryName, result.Warnings[0]);
        }

        [Theory(Skip = "Obsolete: strategies now implement real seeding (commit f40d162b).")]
        [MemberData(nameof(AllStubsTheoryData))]
        public async Task AllStubs_SeedAsync_ShouldNeverCallDbContext(IIndustrySeedStrategy strategy)
        {
            // Stubs should be completely pure — no DB interaction
            _ = await strategy.SeedAsync(TestTenantId, _dbContextMock.Object);

            _dbContextMock.VerifyNoOtherCalls();
        }
    }
}
