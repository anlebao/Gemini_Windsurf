using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.6): Food Cost / COGS / Waste Ratio (SRS §5.2).
    /// FoodCostPerUnit = Σ(line.Qty × PricePerUnit) × (1 + WasteFactor); COGS = Σ(Actual × PricePerUnit).
    /// </summary>
    public class FoodCostServiceTests
    {
        private static readonly TenantId TenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        private static readonly Guid ProductA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        private static readonly Guid IngCoffee = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1");
        private static readonly Guid IngMilk = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2");

        private static FoodCostReport Calculate(
            decimal totalSales = 1_000_000m,
            decimal? actualCoffee = 6_000m, decimal? theoreticalCoffee = 5_000m,
            decimal? actualMilk = 10m, decimal? theoreticalMilk = 8m)
        {
            var coffee = new Ingredient(TenantId, "Cà phê bột", "g", 100_000m, 5_000m, 200m);   // 200đ/g
            var milk = new Ingredient(TenantId, "Sữa đặc", "lon", 100m, 10m, 25_000m);            // 25.000đ/lon
            var ingredients = new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee, [milk.Id] = milk };

            var recipe = new Recipe(TenantId, ProductA, wasteFactor: 0.1m); // 10% waste
            recipe.AddLine(coffee.Id, 20m, "g"); // 20g × 200đ = 4.000đ
            recipe.AddLine(milk.Id, 0.03m, "lon"); // 0.03 × 25.000 = 750đ
            var recipes = new Dictionary<Guid, Recipe> { [ProductA] = recipe };

            List<TheoreticalConsumption> consumptions = [];
            if (actualCoffee is not null && theoreticalCoffee is not null)
            {
                consumptions.Add(new TheoreticalConsumption(TenantId, Guid.NewGuid(), coffee.Id, theoreticalCoffee.Value, actualCoffee.Value));
            }
            if (actualMilk is not null && theoreticalMilk is not null)
            {
                consumptions.Add(new TheoreticalConsumption(TenantId, Guid.NewGuid(), milk.Id, theoreticalMilk.Value, actualMilk.Value));
            }

            var service = new FoodCostService(null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<FoodCostService>.Instance);
            return service.Calculate(
                new Shift(TenantId, ShiftType.Full, Guid.Parse("11111111-1111-1111-1111-111111111111")),
                consumptions,
                ingredients,
                recipes,
                new Dictionary<Guid, int> { [ProductA] = 100 },
                new Dictionary<Guid, decimal> { [ProductA] = totalSales },
                totalSales);
        }

        [Fact]
        public void Calculate_ItemFoodCost_AppliesWasteFactor()
        {
            var report = Calculate();

            // cost/unit = (20×200 + 0.03×25.000) × 1.1 = (4.000 + 750) × 1.1 = 5.225
            var item = Assert.Single(report.Items);
            Assert.Equal(5_225m, item.FoodCostPerUnit);
            Assert.Equal(522_500m, item.TotalFoodCost); // × 100 món
            Assert.Equal(100, item.UnitsSold);
        }

        [Fact]
        public void Calculate_Cogs_UsesActualConsumption()
        {
            var report = Calculate();

            // COGS = 6.000g × 200đ + 10 lon × 25.000đ = 1.200.000 + 250.000 = 1.450.000
            Assert.Equal(1_450_000m, report.Cogs);
            // Theoretical = 5.000 × 200 + 8 × 25.000 = 1.000.000 + 200.000 = 1.200.000
            Assert.Equal(1_200_000m, report.TheoreticalCost);
            // Waste = 250.000; ratio = 250.000/1.200.000 = 20.83%
            Assert.Equal(250_000m, report.WasteCost);
            Assert.Equal(20.83m, Math.Round(report.WasteRatioPercent, 2));
            // FoodCost% = 1.450.000 / 1.000.000 = 145%
            Assert.Equal(145m, report.FoodCostPercent);
        }

        [Fact]
        public void Calculate_ZeroSales_NoDivisionByZero()
        {
            var report = Calculate(totalSales: 0m);

            Assert.Equal(0m, report.FoodCostPercent);
            Assert.Equal(1_450_000m, report.Cogs); // vẫn tính COGS
        }

        [Fact]
        public void Calculate_ZeroTheoretical_NoDivisionByZero()
        {
            var report = Calculate(actualCoffee: 1_000m, theoreticalCoffee: 0m, actualMilk: 5m, theoreticalMilk: 0m);

            Assert.Equal(0m, report.WasteRatioPercent);
            Assert.Equal(325_000m, report.WasteCost); // COGS − 0
        }
    }
}
