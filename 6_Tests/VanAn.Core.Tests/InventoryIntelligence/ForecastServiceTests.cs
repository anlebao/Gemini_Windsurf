using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Phase 3 (2026-10-05): Forecast — ADC rolling window + StockoutDays + Restock suggestion (SRS §7.5).
    /// ADC = Σ TheoreticalConsumption trong window ÷ số ngày có ca đóng (min 1);
    /// StockoutDays = CurrentStock ÷ ADC; Restock = max(0, ADC × (Lead+Safety) − CurrentStock).
    /// </summary>
    public class ForecastServiceTests
    {
        private static readonly TenantId TenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        private static Guid CreateUser(VanAnDbContext ctx)
        {
            var user = new VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser(
                TenantId, "staff1", "hash", "Nhân viên", VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole.Staff);
            ctx.Users.Add(user);
            ctx.SaveChanges();
            return user.Id;
        }

        private static Guid CreateIngredient(VanAnDbContext ctx, decimal currentStock = 200m, string unit = "g")
        {
            var ingredient = new Ingredient(TenantId, "Cà phê bột", unit, currentStock, 5_000m, 200m);
            ctx.Ingredients.Add(ingredient);
            ctx.SaveChanges();
            return ingredient.Id;
        }

        /// <summary>Tạo ca đã submit (status Submitted — ForecastService bỏ qua Draft) tại thời điểm startTime.</summary>
        private static Shift CreateSubmittedShift(VanAnDbContext ctx, Guid staffId, DateTime startTime)
        {
            var shift = new Shift(TenantId, ShiftType.Morning, staffId, startTime);
            ctx.Shifts.Add(shift);
            ctx.SaveChanges();
            shift.Submit(null, 100_000m, 100_000m, startTime.AddHours(4));
            ctx.SaveChanges();
            return shift;
        }

        private static void AddConsumption(VanAnDbContext ctx, Shift shift, Guid ingredientId, decimal theoretical)
        {
            var tc = new TheoreticalConsumption(TenantId, shift.Id, ingredientId, theoretical, theoretical);
            ctx.TheoreticalConsumptions.Add(tc);
            ctx.SaveChanges();
        }

        private static ForecastService BuildService(VanAnDbContext ctx)
            => new(ctx, NullLogger<ForecastService>.Instance);

        [Fact]
        public async Task GetConfig_CreatesWithDefaults_OnFirstAccess()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            var service = BuildService(ctx);

            var config = await service.GetConfigAsync(TenantId);

            Assert.Equal(14, config.AvgDailyConsumptionWindowDays);
            Assert.Equal(2, config.LeadTimeDays);
            Assert.Equal(1, config.SafetyDays);
            Assert.False(config.TelegramEnabled);
            Assert.False(config.ZaloEnabled);

            // Get-or-create: lần 2 không tạo row mới
            var again = await service.GetConfigAsync(TenantId);
            Assert.Equal(config.Id, again.Id);
            Assert.Equal(1, await ctx.VaIIeTenantConfigs.CountAsync(c => c.TenantId == TenantId));
        }

        [Fact]
        public async Task UpdateConfig_PersistsValues()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            var service = BuildService(ctx);

            var updated = await service.UpdateConfigAsync(TenantId, 21, 3, 2);

            Assert.Equal(21, updated.AvgDailyConsumptionWindowDays);
            Assert.Equal(3, updated.LeadTimeDays);
            Assert.Equal(2, updated.SafetyDays);

            var reloaded = await ctx.VaIIeTenantConfigs.FirstAsync(c => c.TenantId == TenantId);
            Assert.Equal(21, reloaded.AvgDailyConsumptionWindowDays);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void UpdateConfig_InvalidWindow_Throws(int windowDays)
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var service = BuildService(scope.Context);

            _ = Assert.ThrowsAsync<ArgumentException>(() => service.UpdateConfigAsync(TenantId, windowDays, 2, 1));
        }

        [Fact]
        public async Task Forecast_ComputesAdcStockoutAndRestock()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            Guid staffId = CreateUser(ctx);
            Guid ingredientId = CreateIngredient(ctx, currentStock: 200m);
            // 2 ca trong window: 3 ngày trước (100g) + 1 ngày trước (50g) → ADC = (100+50)/2 = 75
            Shift shift1 = CreateSubmittedShift(ctx, staffId, DateTime.UtcNow.AddDays(-3));
            Shift shift2 = CreateSubmittedShift(ctx, staffId, DateTime.UtcNow.AddDays(-1));
            AddConsumption(ctx, shift1, ingredientId, 100m);
            AddConsumption(ctx, shift2, ingredientId, 50m);
            var service = BuildService(ctx);

            var report = await service.GetForecastAsync(TenantId);

            Assert.Equal(14, report.WindowDays);
            Assert.Equal(2, report.LeadTimeDays);
            Assert.Equal(1, report.SafetyDays);
            var item = Assert.Single(report.StockoutItems);
            Assert.Equal(75m, item.AvgDailyConsumption);
            Assert.Equal(200m, item.CurrentStock);
            // StockoutDays = floor(200 / 75) = 2 ≤ threshold (2+1=3) → Critical
            Assert.Equal(2, item.DaysRemaining);
            Assert.Equal(3, item.ThresholdDays);
            Assert.Equal(ForecastStatus.Critical, item.Status);
            // Restock = ceil(75 × 3 − 200) = ceil(25) = 25
            Assert.Equal(25m, item.SuggestedOrderQuantity);
            // item nằm trong RestockItems
            Assert.Contains(report.RestockItems, r => r.IngredientId == ingredientId);
        }

        [Fact]
        public async Task Forecast_StatusWarning_WhenBelowDoubleThreshold()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            Guid staffId = CreateUser(ctx);
            Guid ingredientId = CreateIngredient(ctx, currentStock: 400m);
            // ADC = 100 → StockoutDays = 4 > 3 (threshold) nhưng ≤ 6 (2×threshold) → Warning
            Shift shift = CreateSubmittedShift(ctx, staffId, DateTime.UtcNow.AddDays(-1));
            AddConsumption(ctx, shift, ingredientId, 100m);
            var service = BuildService(ctx);

            var report = await service.GetForecastAsync(TenantId);

            var item = Assert.Single(report.StockoutItems);
            Assert.Equal(ForecastStatus.Warning, item.Status);
            Assert.Equal(4, item.DaysRemaining);
        }

        [Fact]
        public async Task Forecast_NoData_WhenNoConsumption()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            _ = CreateIngredient(ctx, currentStock: 200m);
            var service = BuildService(ctx);

            var report = await service.GetForecastAsync(TenantId);

            var item = Assert.Single(report.StockoutItems); // StockoutItems gồm cả NoData (badge "Chưa đủ dữ liệu")
            Assert.Equal(ForecastStatus.NoData, item.Status);
            Assert.Equal(0m, item.SuggestedOrderQuantity);
            Assert.Equal(int.MaxValue, item.DaysRemaining);
            Assert.Empty(report.RestockItems);
        }

        [Fact]
        public async Task Forecast_ZeroStock_CriticalEvenWithoutData()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            _ = CreateIngredient(ctx, currentStock: 0m);
            var service = BuildService(ctx);

            var report = await service.GetForecastAsync(TenantId);

            var item = Assert.Single(report.StockoutItems);
            Assert.Equal(ForecastStatus.Critical, item.Status);
            Assert.Equal(0, item.DaysRemaining);
        }

        [Fact]
        public async Task Forecast_IgnoresDraftShifts()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            Guid staffId = CreateUser(ctx);
            Guid ingredientId = CreateIngredient(ctx, currentStock: 200m);
            // Shift Draft (chưa submit) + TheoreticalConsumption row — không được tính vào ADC
            var draft = new Shift(TenantId, ShiftType.Morning, staffId, DateTime.UtcNow.AddDays(-1));
            ctx.Shifts.Add(draft);
            ctx.SaveChanges();
            AddConsumption(ctx, draft, ingredientId, 500m);
            var service = BuildService(ctx);

            var report = await service.GetForecastAsync(TenantId);

            var item = Assert.Single(report.StockoutItems);
            Assert.Equal(ForecastStatus.NoData, item.Status); // không có ca submit → NoData
        }

        [Fact]
        public async Task UpdateNotificationConfig_PersistsValues()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            var service = BuildService(ctx);

            var updated = await service.UpdateNotificationConfigAsync(
                TenantId, true, "123456:token", "chat-1", true, "zalo-token", "user-1");

            Assert.True(updated.TelegramEnabled);
            Assert.Equal("123456:token", updated.TelegramBotToken);
            Assert.Equal("chat-1", updated.TelegramChatId);
            Assert.True(updated.ZaloEnabled);
            Assert.Equal("zalo-token", updated.ZaloAccessToken);
            Assert.Equal("user-1", updated.ZaloRecipientId);

            var reloaded = await ctx.VaIIeTenantConfigs.FirstAsync(c => c.TenantId == TenantId);
            Assert.True(reloaded.TelegramEnabled);
            Assert.Equal("chat-1", reloaded.TelegramChatId);
        }

        [Fact]
        public async Task Forecast_Trend_GroupsByDay()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var ctx = scope.Context;
            Guid staffId = CreateUser(ctx);
            Guid ingredientId = CreateIngredient(ctx, currentStock: 200m);
            Shift shift1 = CreateSubmittedShift(ctx, staffId, DateTime.UtcNow.AddDays(-3));
            Shift shift2 = CreateSubmittedShift(ctx, staffId, DateTime.UtcNow.AddDays(-1));
            AddConsumption(ctx, shift1, ingredientId, 100m);
            AddConsumption(ctx, shift2, ingredientId, 50m);
            var service = BuildService(ctx);

            var report = await service.GetForecastAsync(TenantId);

            Assert.True(report.Trend.ContainsKey(ingredientId));
            var days = report.Trend[ingredientId];
            Assert.Equal(2, days.Count);
            Assert.Equal(100m, days[0].Quantity);
            Assert.Equal(50m, days[1].Quantity);
            Assert.True(days[0].Date < days[1].Date);
        }
    }
}
