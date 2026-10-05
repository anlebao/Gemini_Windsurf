using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.ShopERP.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.UserAggregate;

namespace VanAn.ShopERP.Tests.InventoryIntelligence;

/// <summary>
/// VA-IIE (RV 2026-10-04): multi-tenancy isolation — catalog chung 1 SQLite (ShopERPDbContext — KHÔNG có
/// global tenant filter, giống production). Service queries phải filter theo TenantId — không leak cross-tenant.
/// </summary>
public class InventoryIntelligenceIsolationTests
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

    [Fact]
    public async Task GetIngredients_AreTenantIsolated()
    {
        var db = CreateContext();
        using var connection = db.connection;
        using var ctx = db.context;
        ctx.Ingredients.AddRange(
            new Ingredient(TenantA, "Cà phê bột A", "g", 100_000m, 5_000m, 200m),
            new Ingredient(TenantA, "Sữa đặc A", "lon", 100m, 10m, 25_000m),
            new Ingredient(TenantB, "Nguyên liệu B", "g", 50m, 5m, 100m));
        await ctx.SaveChangesAsync();

        var service = new RecipeService(ctx, NullLogger<RecipeService>.Instance);

        var forA = await service.GetIngredientsAsync(TenantA);
        var forB = await service.GetIngredientsAsync(TenantB);

        forA.Should().HaveCount(2).And.OnlyContain(i => i.TenantId == TenantA);
        forB.Should().ContainSingle().Which.TenantId.Should().Be(TenantB);
        forA.Should().NotContain(i => i.TenantId == TenantB);
    }

    [Fact]
    public async Task GetProducts_AreTenantIsolated()
    {
        var db = CreateContext();
        using var connection = db.connection;
        using var ctx = db.context;
        ctx.Products.AddRange(
            new Product(TenantA, "Cà phê A", "Test", 30_000m, "Cà phê"),
            new Product(TenantB, "Sản phẩm B", "Test", 10_000m, "Khác"));
        await ctx.SaveChangesAsync();

        var service = new RecipeService(ctx, NullLogger<RecipeService>.Instance);

        var forA = await service.GetProductsAsync(TenantA);
        var forB = await service.GetProductsAsync(TenantB);

        forA.Should().ContainSingle().Which.TenantId.Should().Be(TenantA);
        forB.Should().ContainSingle().Which.TenantId.Should().Be(TenantB);
        forA.Should().NotContain(p => p.TenantId == TenantB);
    }

    [Fact]
    public async Task ListShifts_AreTenantIsolated()
    {
        var db = CreateContext();
        using var connection = db.connection;
        using var ctx = db.context;
        Guid staffA = CreateUser(ctx, TenantA, "staffA");
        Guid staffB = CreateUser(ctx, TenantB, "staffB");
        ctx.Shifts.AddRange(
            new Shift(TenantA, ShiftType.Morning, staffA, DateTime.UtcNow.AddHours(-2)),
            new Shift(TenantB, ShiftType.Evening, staffB, DateTime.UtcNow.AddHours(-2)));
        await ctx.SaveChangesAsync();

        var service = new ShiftReportService(ctx,
            new TheoreticalConsumptionService(ctx, NullLogger<TheoreticalConsumptionService>.Instance),
            new VarianceAnalysisService(ctx, NullLogger<VarianceAnalysisService>.Instance),
            new AlertEngine([], NullLogger<AlertEngine>.Instance),
            NullLogger<ShiftReportService>.Instance);

        var forA = await service.ListShiftsAsync(TenantA);
        var forB = await service.ListShiftsAsync(TenantB);

        forA.Should().ContainSingle().Which.TenantId.Should().Be(TenantA);
        forB.Should().ContainSingle().Which.TenantId.Should().Be(TenantB);
        forA.Should().NotContain(s => s.TenantId == TenantB);
    }

    [Fact]
    public async Task ShiftAlerts_AreTenantIsolated()
    {
        var db = CreateContext();
        using var connection = db.connection;
        using var ctx = db.context;
        Guid staffA = CreateUser(ctx, TenantA, "staffA");
        Guid staffB = CreateUser(ctx, TenantB, "staffB");
        var shiftA = new Shift(TenantA, ShiftType.Morning, staffA);
        var shiftB = new Shift(TenantB, ShiftType.Evening, staffB);
        ctx.Shifts.AddRange(shiftA, shiftB);
        await ctx.SaveChangesAsync();

        ctx.ShiftAlerts.AddRange(
            new ShiftAlert(TenantA, shiftA.Id, "STOCK_LOW", AlertSeverity.Warning, "A low"),
            new ShiftAlert(TenantB, shiftB.Id, "CASH_MISMATCH", AlertSeverity.Critical, "B cash"));
        await ctx.SaveChangesAsync();

        // Mô phỏng query AlertCenter page — filter theo tenant.
        var forA = await ctx.ShiftAlerts.Where(a => a.TenantId == TenantA).ToListAsync();
        var forB = await ctx.ShiftAlerts.Where(a => a.TenantId == TenantB).ToListAsync();

        forA.Should().ContainSingle().Which.AlertCode.Should().Be("STOCK_LOW");
        forB.Should().ContainSingle().Which.AlertCode.Should().Be("CASH_MISMATCH");
        forA.Should().NotContain(a => a.TenantId == TenantB);
    }
}
