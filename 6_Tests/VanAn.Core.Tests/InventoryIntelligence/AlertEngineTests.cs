using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.CoreHub.Services.InventoryIntelligence.AlertRules;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.5): kiểm chứng 10 alert rules (SRS §4.1) + AlertEngine (dedupe/disabled/fail-safe).
    /// </summary>
    public class AlertEngineTests
    {
        private static readonly Guid ProductA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // ── Engine ────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Evaluate_AllRules_ReturnsAllAlerts()
        {
            var engine = new AlertEngine(
                [
                    new IngVarianceHighRule(), new StockLowRule(), new ConsumptionOverLimitRule(),
                    new SalesHighStockStableRule(), new StockDropNoSalesRule(), new MidShiftRestockUnusualRule(),
                    new ConsumableOverStandardRule(), new CashMismatchRule(), new RecipeMissingRule(),
                    new ShiftNotAcknowledgedRule()
                ],
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AlertEngine>.Instance);

            Ingredient coffee = AlertContextTestFactory.Ingredient();
            var recipe = new Recipe(AlertContextTestFactory.TenantId, ProductA);
            recipe.AddLine(coffee.Id, 20m, "g");
            var ctx = AlertContextTestFactory.Build(
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee },
                recipesByProduct: new Dictionary<Guid, Recipe> { [ProductA] = recipe },
                revenueByProduct: new Dictionary<Guid, decimal> { [ProductA] = 100_000m });

            var alerts = await engine.EvaluateAsync(ctx, new AlertThresholds());

            Assert.Empty(alerts); // không vi phạm gì → rỗng
        }

        [Fact]
        public async Task Evaluate_DisabledRule_Skipped()
        {
            var engine = new AlertEngine(
                [new IngVarianceHighRule()],
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AlertEngine>.Instance);

            Ingredient coffee = AlertContextTestFactory.Ingredient();
            var consumption = new TheoreticalConsumption(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, 100m, 200m); // variance 100%
            var ctx = AlertContextTestFactory.Build(
                consumptions: [consumption],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var enabled = await engine.EvaluateAsync(ctx, new AlertThresholds());
            Assert.Single(enabled); // ING_VARIANCE_HIGH trigger

            var disabled = await engine.EvaluateAsync(ctx, new AlertThresholds { DisabledRules = new HashSet<string> { "ING_VARIANCE_HIGH" } });
            Assert.Empty(disabled);
        }

        [Fact]
        public async Task Evaluate_RuleThrows_DoesNotBlockOthers()
        {
            var engine = new AlertEngine(
                [new ThrowingRule(), new CashMismatchRule()],
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AlertEngine>.Instance);

            var shift = new Shift(AlertContextTestFactory.TenantId, ShiftType.Morning, StaffId);
            shift.Submit(null, 100_000m, 110_000m); // chênh 10k > tolerance 0
            var ctx = AlertContextTestFactory.Build(shift: shift);

            var alerts = await engine.EvaluateAsync(ctx, new AlertThresholds { CashTolerance = 0m });

            Assert.Single(alerts);
            Assert.Equal("CASH_MISMATCH", alerts[0].AlertCode);
        }

        // ── Rule 1: ING_VARIANCE_HIGH ─────────────────────────────────────────────

        [Fact]
        public async Task IngVarianceHigh_Trigger_WhenOverThreshold()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient(varianceThreshold: 5m);
            // Theoretical 100, Actual 120 → variance 20% > 5%
            var consumption = new TheoreticalConsumption(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, 100m, 120m);
            var ctx = AlertContextTestFactory.Build(
                consumptions: [consumption],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new IngVarianceHighRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Single(alerts);
            Assert.Equal("ING_VARIANCE_HIGH", alerts[0].AlertCode);
            Assert.Equal(AlertSeverity.Warning, alerts[0].Severity);
            Assert.Equal(coffee.Id, alerts[0].IngredientId);
        }

        [Fact]
        public async Task IngVarianceHigh_NotTrigger_WithinThreshold()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient();
            var consumption = new TheoreticalConsumption(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, 100m, 102m); // 2% < 5%
            var ctx = AlertContextTestFactory.Build(
                consumptions: [consumption],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new IngVarianceHighRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Empty(alerts);
        }

        [Fact]
        public async Task IngVarianceHigh_PerIngredientOverride_Wins()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient(varianceThreshold: 30m); // override cao
            var consumption = new TheoreticalConsumption(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, 100m, 120m); // 20% < 30%
            var ctx = AlertContextTestFactory.Build(
                consumptions: [consumption],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new IngVarianceHighRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Empty(alerts);
        }

        [Fact]
        public async Task IngVarianceHigh_ZeroTheoretical_NotTrigger()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient();
            var consumption = new TheoreticalConsumption(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, 0m, 500m);
            var ctx = AlertContextTestFactory.Build(
                consumptions: [consumption],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new IngVarianceHighRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Empty(alerts);
        }

        // ── Rule 2: STOCK_LOW ─────────────────────────────────────────────────────

        [Fact]
        public async Task StockLow_Trigger_WhenClosingBelowReorderPoint()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient(minStock: 5_000m);
            var closing = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Closing, 4_000m, "g");
            var ctx = AlertContextTestFactory.Build(
                counts: [closing],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new StockLowRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Single(alerts);
            Assert.Equal("STOCK_LOW", alerts[0].AlertCode);
        }

        [Fact]
        public async Task StockLow_NotTrigger_WhenAboveReorderPoint()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient(minStock: 5_000m);
            var closing = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Closing, 6_000m, "g");
            var ctx = AlertContextTestFactory.Build(
                counts: [closing],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new StockLowRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Empty(alerts);
        }

        // ── Rule 3: CONSUMPTION_OVER_LIMIT ────────────────────────────────────────

        [Fact]
        public async Task ConsumptionOverLimit_Trigger_WhenActualExceedsLimit()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient();
            // Theoretical 100, Actual 200 > 100 × 1.15
            var consumption = new TheoreticalConsumption(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, 100m, 200m);
            var ctx = AlertContextTestFactory.Build(
                consumptions: [consumption],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new ConsumptionOverLimitRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Single(alerts);
            Assert.Equal("CONSUMPTION_OVER_LIMIT", alerts[0].AlertCode);
            Assert.Equal(AlertSeverity.Critical, alerts[0].Severity);
        }

        [Fact]
        public async Task ConsumptionOverLimit_NotTrigger_WithinLimit()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient();
            var consumption = new TheoreticalConsumption(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, 100m, 110m); // 10% < 15%
            var ctx = AlertContextTestFactory.Build(
                consumptions: [consumption],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new ConsumptionOverLimitRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Empty(alerts);
        }

        // ── Rule 4: SALES_HIGH_STOCK_STABLE ───────────────────────────────────────

        [Fact]
        public async Task SalesHighStockStable_Trigger_WhenHighSalesAndStableStock()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient();
            var recipe = new Recipe(AlertContextTestFactory.TenantId, ProductA);
            recipe.AddLine(coffee.Id, 20m, "g");
            var opening = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Opening, 100_000m, "g");
            var closing = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Closing, 99_000m, "g"); // giảm 1% — coi như stable
            var ctx = AlertContextTestFactory.Build(
                counts: [opening, closing],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee },
                recipesByProduct: new Dictionary<Guid, Recipe> { [ProductA] = recipe },
                revenueByProduct: new Dictionary<Guid, decimal> { [ProductA] = 2_000_000m });

            var alerts = await new SalesHighStockStableRule().EvaluateAsync(ctx, new AlertThresholds { SalesHighThreshold = 1_000_000m }, default);

            Assert.Single(alerts);
            Assert.Equal("SALES_HIGH_STOCK_STABLE", alerts[0].AlertCode);
            Assert.Equal(AlertSeverity.Critical, alerts[0].Severity);
        }

        [Fact]
        public async Task SalesHighStockStable_NotTrigger_WhenStockDropsNormally()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient();
            var recipe = new Recipe(AlertContextTestFactory.TenantId, ProductA);
            recipe.AddLine(coffee.Id, 20m, "g");
            var opening = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Opening, 100_000m, "g");
            var closing = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Closing, 50_000m, "g"); // giảm 50%
            var ctx = AlertContextTestFactory.Build(
                counts: [opening, closing],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee },
                recipesByProduct: new Dictionary<Guid, Recipe> { [ProductA] = recipe },
                revenueByProduct: new Dictionary<Guid, decimal> { [ProductA] = 2_000_000m });

            var alerts = await new SalesHighStockStableRule().EvaluateAsync(ctx, new AlertThresholds { SalesHighThreshold = 1_000_000m }, default);

            Assert.Empty(alerts);
        }

        // ── Rule 5: STOCK_DROP_NO_SALES ───────────────────────────────────────────

        [Fact]
        public async Task StockDropNoSales_Trigger_WhenDropWithoutSales()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient();
            var opening = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Opening, 100_000m, "g");
            var closing = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Closing, 80_000m, "g");
            var ctx = AlertContextTestFactory.Build(
                counts: [opening, closing],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new StockDropNoSalesRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Single(alerts);
            Assert.Equal("STOCK_DROP_NO_SALES", alerts[0].AlertCode);
            Assert.Equal(AlertSeverity.Critical, alerts[0].Severity);
        }

        [Fact]
        public async Task StockDropNoSales_NotTrigger_WhenDropWithSales()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient();
            var recipe = new Recipe(AlertContextTestFactory.TenantId, ProductA);
            recipe.AddLine(coffee.Id, 20m, "g");
            var opening = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Opening, 100_000m, "g");
            var closing = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Closing, 80_000m, "g");
            var ctx = AlertContextTestFactory.Build(
                counts: [opening, closing],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee },
                recipesByProduct: new Dictionary<Guid, Recipe> { [ProductA] = recipe },
                revenueByProduct: new Dictionary<Guid, decimal> { [ProductA] = 100_000m });

            var alerts = await new StockDropNoSalesRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Empty(alerts);
        }

        // ── Rule 6: MIDSHIFT_RESTOCK_UNUSUAL ──────────────────────────────────────

        [Fact]
        public async Task MidShiftRestockUnusual_Trigger_WhenRestockIsOutlier()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient();
            // Opening 100.000, restock 80.000 (80% > 50% default)
            var opening = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Opening, 100_000m, "g", midShiftStockIn: 80_000m);
            var ctx = AlertContextTestFactory.Build(
                counts: [opening],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new MidShiftRestockUnusualRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Single(alerts);
            Assert.Equal("MIDSHIFT_RESTOCK_UNUSUAL", alerts[0].AlertCode);
            Assert.Equal(AlertSeverity.Warning, alerts[0].Severity);
        }

        [Fact]
        public async Task MidShiftRestockUnusual_NotTrigger_WhenNormalRestock()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient();
            var opening = new InventoryCount(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, CountType.Opening, 100_000m, "g", midShiftStockIn: 30_000m); // 30% < 50%
            var ctx = AlertContextTestFactory.Build(
                counts: [opening],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee });

            var alerts = await new MidShiftRestockUnusualRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Empty(alerts);
        }

        // ── Rule 7: CONSUMABLE_OVER_STANDARD ──────────────────────────────────────

        [Fact]
        public async Task ConsumableOverStandard_Trigger_WhenUsageExceedsStandard()
        {
            Ingredient cup = AlertContextTestFactory.Ingredient(name: "Ly giấy", category: IngredientCategory.Consumable, price: 1_000m);
            // 100 món bán, chuẩn 1 ly/món × 1.1 = 110; dùng 150
            var consumption = new TheoreticalConsumption(AlertContextTestFactory.TenantId, Guid.NewGuid(), cup.Id, 100m, 150m);
            var ctx = AlertContextTestFactory.Build(
                consumptions: [consumption],
                ingredients: new Dictionary<Guid, Ingredient> { [cup.Id] = cup },
                totalUnitsSold: 100);

            var alerts = await new ConsumableOverStandardRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Single(alerts);
            Assert.Equal("CONSUMABLE_OVER_STANDARD", alerts[0].AlertCode);
            Assert.Equal(AlertSeverity.Warning, alerts[0].Severity);
        }

        [Fact]
        public async Task ConsumableOverStandard_NotTrigger_ForRawMaterial()
        {
            Ingredient coffee = AlertContextTestFactory.Ingredient(); // RawMaterial
            var consumption = new TheoreticalConsumption(AlertContextTestFactory.TenantId, Guid.NewGuid(), coffee.Id, 100m, 500m);
            var ctx = AlertContextTestFactory.Build(
                consumptions: [consumption],
                ingredients: new Dictionary<Guid, Ingredient> { [coffee.Id] = coffee },
                totalUnitsSold: 100);

            var alerts = await new ConsumableOverStandardRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Empty(alerts);
        }

        // ── Rule 8: CASH_MISMATCH ─────────────────────────────────────────────────

        [Fact]
        public async Task CashMismatch_Trigger_WhenDiffExceedsTolerance()
        {
            var shift = new Shift(AlertContextTestFactory.TenantId, ShiftType.Morning, StaffId);
            shift.Submit(null, 1_000_000m, 950_000m); // chênh 50.000 > 10.000
            var ctx = AlertContextTestFactory.Build(shift: shift);

            var alerts = await new CashMismatchRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Single(alerts);
            Assert.Equal("CASH_MISMATCH", alerts[0].AlertCode);
            Assert.Equal(AlertSeverity.Critical, alerts[0].Severity);
        }

        [Fact]
        public async Task CashMismatch_NotTrigger_WithinTolerance()
        {
            var shift = new Shift(AlertContextTestFactory.TenantId, ShiftType.Morning, StaffId);
            shift.Submit(null, 1_000_000m, 995_000m); // chênh 5.000 < 10.000
            var ctx = AlertContextTestFactory.Build(shift: shift);

            var alerts = await new CashMismatchRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Empty(alerts);
        }

        // ── Rule 9: RECIPE_MISSING ────────────────────────────────────────────────

        [Fact]
        public async Task RecipeMissing_Trigger_WhenProductSoldWithoutRecipe()
        {
            var ctx = AlertContextTestFactory.Build(
                recipesByProduct: new Dictionary<Guid, Recipe>(),
                revenueByProduct: new Dictionary<Guid, decimal> { [ProductA] = 200_000m });

            var alerts = await new RecipeMissingRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Single(alerts);
            Assert.Equal("RECIPE_MISSING", alerts[0].AlertCode);
            Assert.Equal(AlertSeverity.Critical, alerts[0].Severity);
        }

        [Fact]
        public async Task RecipeMissing_NotTrigger_WhenRecipeExists()
        {
            var recipe = new Recipe(AlertContextTestFactory.TenantId, ProductA);
            var ctx = AlertContextTestFactory.Build(
                recipesByProduct: new Dictionary<Guid, Recipe> { [ProductA] = recipe },
                revenueByProduct: new Dictionary<Guid, decimal> { [ProductA] = 200_000m });

            var alerts = await new RecipeMissingRule().EvaluateAsync(ctx, new AlertThresholds(), default);

            Assert.Empty(alerts);
        }

        // ── Rule 10: SHIFT_NOT_ACKNOWLEDGED ───────────────────────────────────────

        [Fact]
        public async Task ShiftNotAcknowledged_Trigger_WhenSubmittedOverTimeout()
        {
            var shift = new Shift(AlertContextTestFactory.TenantId, ShiftType.Morning, StaffId, startTime: DateTime.UtcNow.AddHours(-8));
            shift.Submit(null, 100m, 100m, endTime: DateTime.UtcNow.AddHours(-3)); // chờ 3h > 2h
            var ctx = AlertContextTestFactory.Build(shift: shift, now: DateTime.UtcNow);

            var alerts = await new ShiftNotAcknowledgedRule().EvaluateAsync(ctx, new AlertThresholds { ShiftAckTimeoutHours = 2 }, default);

            Assert.Single(alerts);
            Assert.Equal("SHIFT_NOT_ACKNOWLEDGED", alerts[0].AlertCode);
            Assert.Equal(AlertSeverity.Warning, alerts[0].Severity);
        }

        [Fact]
        public async Task ShiftNotAcknowledged_NotTrigger_WithinTimeout()
        {
            var shift = new Shift(AlertContextTestFactory.TenantId, ShiftType.Morning, StaffId, startTime: DateTime.UtcNow.AddHours(-2));
            shift.Submit(null, 100m, 100m, endTime: DateTime.UtcNow.AddMinutes(-30)); // chờ 0.5h < 2h
            var ctx = AlertContextTestFactory.Build(shift: shift, now: DateTime.UtcNow);

            var alerts = await new ShiftNotAcknowledgedRule().EvaluateAsync(ctx, new AlertThresholds { ShiftAckTimeoutHours = 2 }, default);

            Assert.Empty(alerts);
        }

        // ── Rule helper: rule throw không chặn engine ─────────────────────────────

        private sealed class ThrowingRule : IAlertRule
        {
            public string AlertCode => "THROW_TEST";
            public AlertSeverity Severity => AlertSeverity.Warning;

            public Task<IReadOnlyList<ShiftAlert>> EvaluateAsync(ShiftAlertContext context, AlertThresholds thresholds, CancellationToken ct)
                => throw new InvalidOperationException("boom");
        }
    }
}
