using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Phase 4 (2026-10-05): Profitability per Item / per Shift (SRS §5.2).
    /// Profit = Revenue − FoodCost; ShiftProfit = Sales − Cogs. Nguồn: FoodCostReport + tên sản phẩm.
    /// </summary>
    public class ProfitabilityServiceTests
    {
        private static readonly TenantId TenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        private static readonly Guid ShiftId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid ProductA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        private static readonly Guid ProductB = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");

        private static FoodCostReport BuildFoodCostReport()
        {
            return new FoodCostReport
            {
                Cogs = 1_450_000m,
                TheoreticalCost = 1_200_000m,
                WasteCost = 250_000m,
                WasteRatioPercent = 20.83m,
                Sales = 2_000_000m,
                FoodCostPercent = 72.5m,
                Items =
                [
                    new FoodCostPerItem(ProductA, 5_225m, 100, 522_500m, 1_200_000m),
                    new FoodCostPerItem(ProductB, 9_275m, 80, 742_000m, 800_000m),
                ]
            };
        }

        [Fact]
        public async Task GetShiftProfitability_ComputesProfitAndNames()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            var coffee = new Product(TenantId, "Cà phê sữa đá", 30_000m, "Cà phê");
            var bacXiu = new Product(TenantId, "Bạc xỉu", 25_000m, "Cà phê");
            // Product (legacy entity) KHÔNG sync Id với business key trong constructor — set Id = PK để
            // khớp ProductId trong FoodCostReport (Single-Identity rule #5: FK reference BaseEntity.Id).
            typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty(nameof(VanAn.Shared.Domain.Common.BaseEntity.Id))!
                .SetValue(coffee, ProductA);
            typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty(nameof(VanAn.Shared.Domain.Common.BaseEntity.Id))!
                .SetValue(bacXiu, ProductB);
            ctx.Products.AddRange(coffee, bacXiu);
            await ctx.SaveChangesAsync();

            var foodCost = new Mock<IFoodCostService>();
            foodCost.Setup(f => f.GetShiftFoodCostAsync(ShiftId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildFoodCostReport());
            var service = new ProfitabilityService(foodCost.Object, ctx, NullLogger<ProfitabilityService>.Instance);

            var report = await service.GetShiftProfitabilityAsync(ShiftId);

            Assert.NotNull(report);
            Assert.Equal(2_000_000m, report!.Sales);
            Assert.Equal(1_450_000m, report.Cogs);
            Assert.Equal(550_000m, report.ShiftProfit); // 2.000.000 − 1.450.000
            Assert.Equal(27.5m, report.ShiftProfitPercent); // 550.000 / 2.000.000 × 100

            // Item A: 1.200.000 − 522.500 = 677.500 (lãi) · Item B: 800.000 − 742.000 = 58.000 (lãi)
            // Sort theo Profit DESC → A trước.
            Assert.Equal(2, report.Items.Count);
            Assert.Equal(ProductA, report.Items[0].ProductId);
            Assert.Equal(677_500m, report.Items[0].Profit);
            Assert.Equal(56.46m, Math.Round(report.Items[0].ProfitPercent, 2));
            Assert.Equal("Cà phê sữa đá", report.Items[0].ProductName);
            Assert.Equal(58_000m, report.Items[1].Profit);
        }

        [Fact]
        public async Task GetShiftProfitability_ShiftMissing_ReturnsNull()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            var foodCost = new Mock<IFoodCostService>();
            foodCost.Setup(f => f.GetShiftFoodCostAsync(ShiftId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException($"Shift {ShiftId} không tồn tại"));
            var service = new ProfitabilityService(foodCost.Object, ctx, NullLogger<ProfitabilityService>.Instance);

            var report = await service.GetShiftProfitabilityAsync(ShiftId);

            Assert.Null(report);
        }

        [Fact]
        public async Task GetShiftProfitability_ZeroSales_NoDivisionByZero()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            var foodCost = new Mock<IFoodCostService>();
            foodCost.Setup(f => f.GetShiftFoodCostAsync(ShiftId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FoodCostReport
                {
                    Cogs = 0m,
                    TheoreticalCost = 0m,
                    WasteCost = 0m,
                    WasteRatioPercent = 0m,
                    Sales = 0m,
                    FoodCostPercent = 0m,
                    Items = []
                });
            var service = new ProfitabilityService(foodCost.Object, ctx, NullLogger<ProfitabilityService>.Instance);

            var report = await service.GetShiftProfitabilityAsync(ShiftId);

            Assert.NotNull(report);
            Assert.Equal(0m, report!.ShiftProfit);
            Assert.Equal(0m, report.ShiftProfitPercent);
            Assert.Empty(report.Items);
        }
    }
}
