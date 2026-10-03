using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.1/P2.7): integration — shift workflow end-to-end (SRS §3.1, §3.5):
    /// mở ca → kiểm kê đầu → restock → POS bán → đóng ca (tính tiêu hao + variance + alert)
    /// → bàn giao (ACK → CLOSED) → immutable sau Closed (NFR-7) → RECIPE_MISSING.
    /// </summary>
    public class ShiftReportServiceIntegrationTests
    {
        private static readonly TenantId TenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        // Ca mở 3h trước, đóng "bây giờ" — tránh SHIFT_NOT_ACKNOWLEDGED trigger nhầm (rule so với Now).
        private static readonly DateTime T0 = DateTime.UtcNow.AddHours(-3);

        private static Guid CreateUser(VanAnDbContext ctx, string username = "staff1")
        {
            var user = new VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser(
                TenantId, username, "hash", "Nhân viên", VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole.Staff);
            ctx.Users.Add(user);
            ctx.SaveChanges();
            return user.Id;
        }

        private static Guid CreateProduct(VanAnDbContext ctx, string name = "Cà phê sữa đá")
        {
            var product = new Product(TenantId, name, "Test", 30_000m, "Cà phê");
            ctx.Products.Add(product);
            ctx.SaveChanges();
            return product.Id;
        }

        private static Guid CreateIngredient(VanAnDbContext ctx, string name = "Cà phê bột", string unit = "g", decimal price = 200m, decimal minStock = 5_000m)
        {
            var ingredient = new Ingredient(TenantId, name, unit, 100_000m, minStock, price);
            ctx.Ingredients.Add(ingredient);
            ctx.SaveChanges();
            return ingredient.Id;
        }

        private static void CreateRecipe(VanAnDbContext ctx, Guid productId, Guid ingredientId, decimal qty = 20m)
        {
            // EffectiveFrom TRƯỚC thời điểm bán (T0) — version resolve theo thời điểm bán (SRS §3.2.3).
            var recipe = new Recipe(TenantId, productId, effectiveFrom: T0.AddDays(-1));
            recipe.AddLine(ingredientId, qty, "g");
            ctx.Recipes.Add(recipe);
            ctx.SaveChanges();
        }

        private static void CreateOrder(VanAnDbContext ctx, Guid productId, int units, DateTime createdAt, decimal unitPrice = 30_000m)
        {
            Guid orderId = Guid.NewGuid();
            var items = new List<OrderItem>
            {
                OrderItem.Create(Guid.NewGuid(), TenantId, orderId, productId, units, unitPrice, "Cà phê sữa đá")
            };
            Order order = Order.Create(orderId, TenantId, null, items);
            order.UpdateOrderStatus(OrderStatusId.Completed);
            typeof(BaseEntity).GetProperty(nameof(BaseEntity.CreatedAt))?.SetValue(order, createdAt);
            ctx.Orders.Add(order);
            ctx.SaveChanges();
        }

        private static ShiftReportService BuildService(VanAnDbContext ctx)
        {
            var consumption = new TheoreticalConsumptionService(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<TheoreticalConsumptionService>.Instance);
            var variance = new VarianceAnalysisService(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<VarianceAnalysisService>.Instance);
            var engine = new AlertEngine(
                [
                    new VanAn.CoreHub.Services.InventoryIntelligence.AlertRules.IngVarianceHighRule(),
                    new VanAn.CoreHub.Services.InventoryIntelligence.AlertRules.StockLowRule(),
                    new VanAn.CoreHub.Services.InventoryIntelligence.AlertRules.ConsumptionOverLimitRule(),
                    new VanAn.CoreHub.Services.InventoryIntelligence.AlertRules.SalesHighStockStableRule(),
                    new VanAn.CoreHub.Services.InventoryIntelligence.AlertRules.StockDropNoSalesRule(),
                    new VanAn.CoreHub.Services.InventoryIntelligence.AlertRules.MidShiftRestockUnusualRule(),
                    new VanAn.CoreHub.Services.InventoryIntelligence.AlertRules.ConsumableOverStandardRule(),
                    new VanAn.CoreHub.Services.InventoryIntelligence.AlertRules.CashMismatchRule(),
                    new VanAn.CoreHub.Services.InventoryIntelligence.AlertRules.RecipeMissingRule(),
                    new VanAn.CoreHub.Services.InventoryIntelligence.AlertRules.ShiftNotAcknowledgedRule()
                ],
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AlertEngine>.Instance);
            return new ShiftReportService(ctx, consumption, variance, engine,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ShiftReportService>.Instance);
        }

        [Fact]
        public async Task FullWorkflow_OpenCountsOrdersClose_ComputesConsumptionVarianceAlerts()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;

            Guid staffId = CreateUser(ctx);
            Guid managerId = CreateUser(ctx, "manager");
            Guid productId = CreateProduct(ctx);
            Guid coffeeId = CreateIngredient(ctx);
            CreateRecipe(ctx, productId, coffeeId, 20m);

            // 4 món bán (2 đơn × 2 ly) → theoretical = 4 × 20g = 80g
            CreateOrder(ctx, productId, 2, T0.AddHours(1));
            CreateOrder(ctx, productId, 2, T0.AddHours(2));

            var service = BuildService(ctx);

            // ── 1. Mở ca (SRS §3.1.1) ──
            Shift shift = await service.OpenShiftAsync(TenantId, ShiftType.Morning, staffId, T0);
            Assert.Equal(ShiftStatus.Draft, shift.Status);

            // ── 2. Kiểm kê đầu ca ──
            await service.AddInventoryCountAsync(shift.Id, coffeeId, CountType.Opening, 1_000m, "g");
            // upsert: nhập lại đầu ca → cập nhật, không nhân đôi
            await service.AddInventoryCountAsync(shift.Id, coffeeId, CountType.Opening, 1_100m, "g");
            Assert.Equal(1, await ctx.InventoryCounts.CountAsync(c => c.ShiftId == shift.Id && c.CountType == CountType.Opening));

            // ── 3. Nhập thêm trong ca (SRS §3.1.2) ──
            InventoryCount opening = await service.AddRestockAsync(shift.Id, coffeeId, 400m, "g");
            Assert.Equal(400m, opening.MidShiftStockIn);
            opening = await service.AddRestockAsync(shift.Id, coffeeId, 200m, "g");
            Assert.Equal(600m, opening.MidShiftStockIn); // cộng dồn

            // ── 4. Kiểm kê cuối ca ──
            await service.AddInventoryCountAsync(shift.Id, coffeeId, CountType.Closing, 1_500m, "g");

            // ── 5. Đóng ca (SRS §3.1.3): tiền mặt chênh 20k → CASH_MISMATCH ──
            Shift submitted = await service.SubmitShiftAsync(shift.Id, staffId, 510_000m, 490_000m, "Bàn giao OK", T0.AddHours(8));
            Assert.Equal(ShiftStatus.Submitted, submitted.Status);
            Assert.Equal(510_000m, submitted.CashCount);

            // Variance: Actual = 1.100 + 600 − 1.500 = 200; Theoretical = 80 → Variance 120 (150%)
            var consumption = await ctx.TheoreticalConsumptions.SingleAsync(t => t.ShiftId == shift.Id);
            Assert.Equal(80m, consumption.TheoreticalQuantity);
            Assert.Equal(200m, consumption.ActualQuantity);
            Assert.Equal(120m, consumption.Variance);
            Assert.Equal(150m, consumption.VariancePercent);

            // Alerts: ING_VARIANCE_HIGH (150% > 5%) + STOCK_LOW (closing 1.500 < 5.000)
            //        + CASH_MISMATCH (20k > 10k) + CONSUMPTION_OVER_LIMIT (200 > 80×1.15)
            //        + MIDSHIFT_RESTOCK_UNUSUAL (600 > 1.100×50%)
            var alerts = await ctx.ShiftAlerts.Where(a => a.ShiftId == shift.Id).ToListAsync();
            var codes = alerts.Select(a => a.AlertCode).OrderBy(c => c).ToList();
            Assert.Equal(
                new[] { "CASH_MISMATCH", "CONSUMPTION_OVER_LIMIT", "ING_VARIANCE_HIGH", "MIDSHIFT_RESTOCK_UNUSUAL", "STOCK_LOW" },
                codes);
            Assert.DoesNotContain(alerts, a => a.AlertCode == "RECIPE_MISSING"); // recipe đã có

            // ── 6. Báo cáo cuối ca — 6 phần (SRS §5.1) ──
            var report = await service.GetShiftReportAsync(shift.Id);
            Assert.Equal(ShiftStatus.Submitted, report.Shift.Status);
            Assert.Single(report.OpeningCounts);
            Assert.Single(report.ClosingCounts);
            Assert.Single(report.Consumptions);
            Assert.Equal(5, report.Alerts.Count);
            Assert.Equal(20_000m, report.Cash.Difference);
            Assert.False(report.Cash.IsMatched);
            Assert.Equal(2, report.Orders.Count);
            Assert.Equal(150m, report.Consumptions[0].VariancePercent);
            Assert.Equal(VarianceClassification.Loss, report.Consumptions[0].Classification);

            // ── 7. Bàn giao: ACK → CLOSED (SRS §3.5) ──
            Shift acknowledged = await service.AcknowledgeShiftAsync(shift.Id, managerId);
            Assert.Equal(ShiftStatus.Acknowledged, acknowledged.Status);
            Assert.Equal(managerId, acknowledged.AcknowledgedBy);
            Assert.NotNull(acknowledged.AcknowledgedAt);

            Shift closed = await service.CloseShiftAsync(shift.Id, managerId);
            Assert.Equal(ShiftStatus.Closed, closed.Status);

            // ── 8. NFR-7: InventoryCount immutable sau Closed ──
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddInventoryCountAsync(shift.Id, coffeeId, CountType.Closing, 800m, "g"));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AddRestockAsync(shift.Id, coffeeId, 100m, "g"));
        }

        [Fact]
        public async Task RecipeMissing_ProductSoldWithoutRecipe_GeneratesAlert()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;

            Guid staffId = CreateUser(ctx);
            Guid productWithRecipe = CreateProduct(ctx, "Cà phê");
            Guid productNoRecipe = CreateProduct(ctx, "Trà sữa");
            Guid coffeeId = CreateIngredient(ctx);
            CreateRecipe(ctx, productWithRecipe, coffeeId, 20m);

            CreateOrder(ctx, productWithRecipe, 1, T0.AddHours(1));
            CreateOrder(ctx, productNoRecipe, 1, T0.AddHours(2));

            var service = BuildService(ctx);
            Shift shift = await service.OpenShiftAsync(TenantId, ShiftType.Morning, staffId, T0);
            await service.AddInventoryCountAsync(shift.Id, coffeeId, CountType.Opening, 1_000m, "g");
            await service.AddInventoryCountAsync(shift.Id, coffeeId, CountType.Closing, 800m, "g");

            await service.SubmitShiftAsync(shift.Id, staffId, 100_000m, 100_000m, null, T0.AddHours(8));

            var alerts = await ctx.ShiftAlerts.Where(a => a.ShiftId == shift.Id).ToListAsync();
            Assert.Contains(alerts, a => a.AlertCode == "RECIPE_MISSING" && a.Severity == AlertSeverity.Critical);
        }

        [Fact]
        public async Task Submit_NegativeCash_Throws()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;

            Guid staffId = CreateUser(ctx);
            var service = BuildService(ctx);
            Shift shift = await service.OpenShiftAsync(TenantId, ShiftType.Morning, staffId, T0);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => service.SubmitShiftAsync(shift.Id, staffId, -1m, 0m));
        }
    }
}
