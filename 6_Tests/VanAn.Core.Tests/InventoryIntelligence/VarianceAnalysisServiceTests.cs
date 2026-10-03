using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.4): Variance Analysis — Actual = Opening + MidShiftStockIn − Closing;
    /// Variance = Actual − Theoretical; phân loại (SRS §3.4). Pure calc + persist test.
    /// </summary>
    public class VarianceAnalysisServiceTests
    {
        private static readonly TenantId TenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        private static readonly Guid IngredientId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1");

        // ── CalculateActual (pure) ────────────────────────────────────────────────

        [Fact]
        public void CalculateActual_OpeningPlusRestockMinusClosing()
        {
            var counts = new List<InventoryCount>
            {
                new(TenantId, Guid.NewGuid(), IngredientId, CountType.Opening, 100_000m, "g", midShiftStockIn: 20_000m),
                new(TenantId, Guid.NewGuid(), IngredientId, CountType.Closing, 90_000m, "g")
            };

            // Actual = 100.000 + 20.000 − 90.000 = 30.000
            Assert.Equal(30_000m, IVarianceAnalysisService.CalculateActual(counts));
        }

        [Fact]
        public void CalculateActual_WithoutRestock()
        {
            var counts = new List<InventoryCount>
            {
                new(TenantId, Guid.NewGuid(), IngredientId, CountType.Opening, 100_000m, "g"),
                new(TenantId, Guid.NewGuid(), IngredientId, CountType.Closing, 95_000m, "g")
            };

            Assert.Equal(5_000m, IVarianceAnalysisService.CalculateActual(counts));
        }

        [Fact]
        public void CalculateActual_MissingClosing_ReturnsZero()
        {
            var counts = new List<InventoryCount>
            {
                new(TenantId, Guid.NewGuid(), IngredientId, CountType.Opening, 100_000m, "g")
            };

            Assert.Equal(100_000m, IVarianceAnalysisService.CalculateActual(counts)); // opening + 0 − 0
        }

        // ── Classify (pure, SRS §3.4) ─────────────────────────────────────────────

        [Fact]
        public void Classify_Positive_IsLoss()
        {
            Assert.Equal(VarianceClassification.Loss, IVarianceAnalysisService.Classify(500m));
        }

        [Fact]
        public void Classify_Negative_IsAnomaly()
        {
            Assert.Equal(VarianceClassification.Anomaly, IVarianceAnalysisService.Classify(-500m));
        }

        [Fact]
        public void Classify_Zero_IsNormal()
        {
            Assert.Equal(VarianceClassification.Normal, IVarianceAnalysisService.Classify(0m));
        }

        // ── AnalyzeShiftAsync (persist) ───────────────────────────────────────────

        private static Guid CreateUser(VanAn.CoreHub.Infrastructure.VanAnDbContext ctx)
        {
            var user = new VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser(
                TenantId, "staff1", "hash", "Nhân viên", VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole.Staff);
            ctx.Users.Add(user);
            ctx.SaveChanges();
            return user.Id;
        }

        [Fact]
        public async Task AnalyzeShift_PersistsConsumptions_WithVarianceComputed()
        {
            using var scope = VanAn.CoreHub.Tests.TestInfrastructure.VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;

            Guid staffId = CreateUser(ctx);
            var ingredient = new Ingredient(TenantId, "Cà phê bột", "g", 100_000m, 5_000m, 200m);
            ctx.Ingredients.Add(ingredient);
            var shift = new Shift(TenantId, ShiftType.Morning, staffId);
            ctx.Shifts.Add(shift);
            await ctx.SaveChangesAsync();
            Guid ingredientId = ingredient.Id; // Id thật — FK

            var counts = new List<InventoryCount>
            {
                new(TenantId, shift.Id, ingredientId, CountType.Opening, 100_000m, "g"),
                new(TenantId, shift.Id, ingredientId, CountType.Closing, 92_000m, "g")
            };
            ctx.InventoryCounts.AddRange(counts);
            await ctx.SaveChangesAsync();

            var theoretical = new Dictionary<Guid, decimal> { [ingredientId] = 6_000m };

            var service = new VarianceAnalysisService(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<VarianceAnalysisService>.Instance);
            var result = await service.AnalyzeShiftAsync(shift, counts, theoretical);

            Assert.Single(result);
            var c = result[0];
            Assert.Equal(8_000m, c.ActualQuantity);   // 100.000 − 92.000
            Assert.Equal(6_000m, c.TheoreticalQuantity);
            Assert.Equal(2_000m, c.Variance);          // Actual − Theoretical
            Assert.Equal(33.33m, Math.Round(c.VariancePercent, 2));

            // Persisted
            var persisted = ctx.TheoreticalConsumptions.Count(t => t.ShiftId == shift.Id);
            Assert.Equal(1, persisted);
        }

        [Fact]
        public async Task AnalyzeShift_SkipsIngredients_WithoutCountsAndTheoretical()
        {
            using var scope = VanAn.CoreHub.Tests.TestInfrastructure.VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;

            Guid staffId = CreateUser(ctx);
            var shift = new Shift(TenantId, ShiftType.Morning, staffId);
            ctx.Shifts.Add(shift);
            await ctx.SaveChangesAsync();

            var service = new VarianceAnalysisService(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<VarianceAnalysisService>.Instance);
            var result = await service.AnalyzeShiftAsync(shift, [], new Dictionary<Guid, decimal>());

            Assert.Empty(result);
            Assert.Equal(0, ctx.TheoreticalConsumptions.Count());
        }
    }
}
