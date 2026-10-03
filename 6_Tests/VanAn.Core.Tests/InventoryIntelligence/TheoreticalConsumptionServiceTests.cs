using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.3): tiêu hao lý thuyết = Σ(RecipeLine.Quantity × UnitsSold × (1 + WasteFactor)),
    /// version resolve theo thời điểm bán (SRS §3.3, §3.2.3, §7.3).
    /// </summary>
    public class TheoreticalConsumptionServiceTests
    {
        private static readonly TenantId TenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        private static readonly Guid ProductA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        private static readonly Guid ProductB = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
        private static readonly Guid IngCoffee = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1");
        private static readonly Guid IngMilk = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2");

        private static Recipe RecipeV1(Guid productId, DateTime effectiveFrom)
        {
            var recipe = new Recipe(TenantId, productId, wasteFactor: 0.1m, effectiveFrom: effectiveFrom);
            recipe.AddLine(IngCoffee, 20m, "g");
            return recipe;
        }

        // ── CalculateFromItems (pure) ─────────────────────────────────────────────

        [Fact]
        public void CalculateFromItems_AppliesWasteFactor()
        {
            var recipe = new Recipe(TenantId, ProductA, wasteFactor: 0.1m); // 10% waste
            recipe.AddLine(IngCoffee, 20m, "g"); // 20g × 10 món × 1.1 = 220g
            recipe.AddLine(IngMilk, 0.03m, "lon"); // 0.03 × 10 × 1.1 = 0.33

            var service = new TheoreticalConsumptionService(null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<TheoreticalConsumptionService>.Instance);
            var result = service.CalculateFromItems(
                [(ProductA, 10m, DateTime.UtcNow)],
                new Dictionary<Guid, IReadOnlyList<Recipe>> { [ProductA] = [recipe] });

            Assert.Equal(220m, result[IngCoffee]);
            Assert.Equal(0.33m, result[IngMilk]);
        }

        [Fact]
        public void CalculateFromItems_ResolvesVersionAtSaleTime()
        {
            var t0 = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
            var v1 = new Recipe(TenantId, ProductA, effectiveFrom: t0.AddDays(-10));
            v1.AddLine(IngCoffee, 20m, "g");
            var v2 = new Recipe(TenantId, ProductA, effectiveFrom: t0.AddDays(1)); // active từ ngày 2
            v2.AddLine(IngCoffee, 25m, "g");

            var service = new TheoreticalConsumptionService(null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<TheoreticalConsumptionService>.Instance);

            // Bán ngày 1 → v1 (20g); bán ngày 3 → v2 (25g)
            var result = service.CalculateFromItems(
                [(ProductA, 2m, t0), (ProductA, 2m, t0.AddDays(3))],
                new Dictionary<Guid, IReadOnlyList<Recipe>> { [ProductA] = [v1, v2] });

            Assert.Equal(20m * 2 + 25m * 2, result[IngCoffee]);
        }

        [Fact]
        public void CalculateFromItems_SkipsProductWithoutRecipe()
        {
            var service = new TheoreticalConsumptionService(null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<TheoreticalConsumptionService>.Instance);

            var result = service.CalculateFromItems(
                [(ProductB, 5m, DateTime.UtcNow)],
                new Dictionary<Guid, IReadOnlyList<Recipe>>());

            Assert.Empty(result); // RECIPE_MISSING — alert engine xử lý
        }

        [Fact]
        public void CalculateFromItems_EmptyItems_ReturnsEmpty()
        {
            var service = new TheoreticalConsumptionService(null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<TheoreticalConsumptionService>.Instance);

            Assert.Empty(service.CalculateFromItems([], new Dictionary<Guid, IReadOnlyList<Recipe>>()));
        }
    }
}
