using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.ShopERP.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.InventoryIntelligence;

/// <summary>
/// VA-IIE Phase 3 (2026-10-05): Forecast multi-tenancy isolation — ShopERPDbContext (KHÔNG có global
/// tenant filter, giống production). ADC + Stockout chỉ tính dữ liệu của tenant đang query (bài học a21f97f2).
/// </summary>
public class ForecastServiceIsolationTests
{
    private static readonly TenantId TenantA = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    private static readonly TenantId TenantB = new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

    private static (SqliteConnection connection, ShopERPDbContext context) CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ShopERPDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ShopERPDbContext(options);
        context.Database.EnsureCreated();
        return (connection, context);
    }

    private static Guid CreateUser(ShopERPDbContext ctx, TenantId tenant, string username)
    {
        var user = new VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser(
            tenant, username, "hash", $"NV {username}", VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole.Staff);
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user.Id;
    }

    private static Guid CreateIngredient(ShopERPDbContext ctx, TenantId tenant, string name, decimal stock)
    {
        var ingredient = new Ingredient(tenant, name, "g", stock, 5_000m, 200m);
        ctx.Ingredients.Add(ingredient);
        ctx.SaveChanges();
        return ingredient.Id;
    }

    private static Shift CreateSubmittedShift(ShopERPDbContext ctx, TenantId tenant, Guid staffId, DateTime startTime)
    {
        var shift = new Shift(tenant, ShiftType.Morning, staffId, startTime);
        ctx.Shifts.Add(shift);
        ctx.SaveChanges();
        shift.Submit(null, 100_000m, 100_000m, startTime.AddHours(4));
        ctx.SaveChanges();
        return shift;
    }

    [Fact]
    public async Task Forecast_IsTenantIsolated()
    {
        var db = CreateContext();
        using var connection = db.connection;
        using var ctx = db.context;
        Guid staffA = CreateUser(ctx, TenantA, "staffA");
        Guid staffB = CreateUser(ctx, TenantB, "staffB");
        Guid ingA = CreateIngredient(ctx, TenantA, "Cà phê bột A", 200m);
        Guid ingB = CreateIngredient(ctx, TenantB, "Nguyên liệu B", 50m);

        // Tenant A: 2 ca, ADC = (100 + 50) / 2 = 75
        Shift shiftA1 = CreateSubmittedShift(ctx, TenantA, staffA, DateTime.UtcNow.AddDays(-3));
        Shift shiftA2 = CreateSubmittedShift(ctx, TenantA, staffA, DateTime.UtcNow.AddDays(-1));
        ctx.TheoreticalConsumptions.AddRange(
            new TheoreticalConsumption(TenantA, shiftA1.Id, ingA, 100m, 100m),
            new TheoreticalConsumption(TenantA, shiftA2.Id, ingA, 50m, 50m));
        // Tenant B: 1 ca, ADC = 40
        Shift shiftB = CreateSubmittedShift(ctx, TenantB, staffB, DateTime.UtcNow.AddDays(-1));
        ctx.TheoreticalConsumptions.Add(new TheoreticalConsumption(TenantB, shiftB.Id, ingB, 40m, 40m));
        await ctx.SaveChangesAsync();

        var service = new ForecastService(ctx, NullLogger<ForecastService>.Instance);

        var forA = await service.GetForecastAsync(TenantA);
        var forB = await service.GetForecastAsync(TenantB);

        forA.StockoutItems.Should().ContainSingle().Which.IngredientId.Should().Be(ingA);
        forA.StockoutItems.Should().NotContain(i => i.IngredientId == ingB);
        forA.StockoutItems.Single().AvgDailyConsumption.Should().Be(75m);

        forB.StockoutItems.Should().ContainSingle().Which.IngredientId.Should().Be(ingB);
        forB.StockoutItems.Should().NotContain(i => i.IngredientId == ingA);
        forB.StockoutItems.Single().AvgDailyConsumption.Should().Be(40m);

        // VaIIeTenantConfig cũng per-tenant: get-or-create tách biệt
        forA.WindowDays.Should().Be(14);
        var configB = await service.GetConfigAsync(TenantB);
        ctx.VaIIeTenantConfigs.CountAsync(c => c.TenantId == TenantA).Result.Should().Be(1);
        ctx.VaIIeTenantConfigs.CountAsync(c => c.TenantId == TenantB).Result.Should().Be(1);
        configB.Id.Should().NotBe((await service.GetConfigAsync(TenantA)).Id);
    }
}
