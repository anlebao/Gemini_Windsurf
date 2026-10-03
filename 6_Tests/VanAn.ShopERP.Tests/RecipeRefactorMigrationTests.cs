using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VanAn.ShopERP.Infrastructure;

namespace VanAn.ShopERP.Tests;

/// <summary>
/// VA-IIE Sprint B (P1) — Kiểm chứng migration backfill Recipe flat → header + RecipeLine
/// (Q1-A: backfill TOÀN BỘ, IsActive theo product; Q3-C: kg → g).
/// Chạy migration thật trên SQLite in-memory: migrate về schema cũ → seed data flat → migrate lên mới → assert.
/// </summary>
public class RecipeRefactorMigrationTests
{
    private const string OldMigrationId = "20261002155911_AddOrderHtxInternalFlag";
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task MigrateToLatest_BackfillsFlatRecipesIntoHeadersAndLines_WithKgToGConversion()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShopERPDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new ShopERPDbContext(options);
        var migrator = context.Database.GetService<IMigrator>();

        // ── 1. Migrate về schema cũ (flat Recipe) ──
        await migrator.MigrateAsync(OldMigrationId);

        // ── 2. Seed data flat cũ (raw SQL — model hiện tại đã là schema mới) ──
        Guid productActive = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        Guid productInactive = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
        Guid ingCoffee = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1"); // kg
        Guid ingCondensed = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2"); // lon (phi-SI)
        Guid ingTea = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc3"); // gói (phi-SI)

        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Products" ("Id", "TenantId", "Name", "Description", "Category", "Price", "VatRate", "IsActive", "IsDeleted", "CreatedAt", "UpdatedAt", "CostPrice", "IsPosOnly")
            VALUES
                ({0}, {5}, 'Cà phê đen đá', 'Cà phê phin', 'Cà phê', 25000, 0.10, 1, 0, '2026-01-01 08:00:00', '2026-01-01 08:00:00', 0, 0),
                ({1}, {5}, 'Trà vải', 'Trà đen vải', 'Trà', 40000, 0.10, 0, 0, '2026-01-01 09:00:00', '2026-01-01 09:00:00', 0, 0);
            """, productActive, productInactive, null, null, null, TenantId);

        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Ingredients" ("Id", "TenantId", "Name", "Unit", "CurrentStock", "MinStockThreshold", "PricePerUnit", "CreatedAt", "UpdatedAt", "IsDeleted")
            VALUES
                ({0}, {6}, 'Cà phê bột', 'kg', 100, 5, 200000, '2026-01-01 08:00:00', '2026-01-01 08:00:00', 0),
                ({1}, {6}, 'Sữa đặc', 'lon', 100, 10, 25000, '2026-01-01 08:00:00', '2026-01-01 08:00:00', 0),
                ({2}, {6}, 'Trà đen', 'gói', 100, 10, 8000, '2026-01-01 08:00:00', '2026-01-01 08:00:00', 0);
            """, ingCoffee, ingCondensed, ingTea, null, null, null, TenantId);

        // Recipe flat: productActive có 2 dòng (đa nguyên liệu) + productInactive 1 dòng
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Recipes" ("Id", "TenantId", "ProductId", "IngredientId", "QuantityNeeded", "CreatedAt", "UpdatedAt", "IsDeleted")
            VALUES
                ({0}, {6}, {3}, {4}, 0.02, '2026-01-01 08:00:00', '2026-01-01 08:00:00', 0),
                ({1}, {6}, {3}, {5}, 0.03, '2026-01-01 08:00:00', '2026-01-01 08:00:00', 0),
                ({2}, {6}, {7}, {8}, 0.05, '2026-01-01 09:00:00', '2026-01-01 09:00:00', 0);
            """,
            Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd1"),
            Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd2"),
            Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd3"),
            productActive, ingCoffee, ingCondensed, TenantId, productInactive, ingTea);

        // ── 3. Migrate lên mới (AddVaIIeShiftReportRecipeRefactor + backfill) ──
        await migrator.MigrateAsync();

        // ── 4. Assert: headers grouped theo product ──
        var headers = await context.Recipes.Include(r => r.Lines).ToListAsync();
        Assert.Equal(2, headers.Count); // 1 header per product (P1 gộp 2 dòng, P2 giữ 1)

        var p1 = headers.Single(h => h.ProductId == productActive);
        Assert.Equal(1, p1.Version);
        Assert.True(p1.IsActive); // product active
        Assert.Equal(1m, p1.Yield);
        Assert.Equal(0m, p1.WasteFactor);
        Assert.Equal(2, p1.Lines.Count); // 2 RecipeLines từ 2 row flat cũ

        var p2 = headers.Single(h => h.ProductId == productInactive);
        Assert.False(p2.IsActive); // IsActive theo product (inactive)
        Assert.Single(p2.Lines);

        // ── 5. Assert: kg → g conversion + phi-SI giữ nguyên ──
        var coffeeLine = p1.Lines.Single(l => l.IngredientId == ingCoffee);
        Assert.Equal(20m, coffeeLine.Quantity); // 0.02 kg → 20 g
        Assert.Equal("g", coffeeLine.Unit);

        var condensedLine = p1.Lines.Single(l => l.IngredientId == ingCondensed);
        Assert.Equal(0.03m, condensedLine.Quantity); // lon: không đổi
        Assert.Equal("lon", condensedLine.Unit);

        var teaLine = p2.Lines.Single();
        Assert.Equal(0.05m, teaLine.Quantity); // gói: không đổi
        Assert.Equal("gói", teaLine.Unit);

        // ── 6. Assert: ingredient kg → g (stock/threshold ×1000, price ÷1000) ──
        var coffee = await context.Ingredients.SingleAsync(i => i.Id == ingCoffee);
        Assert.Equal("g", coffee.Unit);
        Assert.Equal(100_000m, coffee.CurrentStock);
        Assert.Equal(5_000m, coffee.MinStockThreshold);
        Assert.Equal(200m, coffee.PricePerUnit);

        var condensed = await context.Ingredients.SingleAsync(i => i.Id == ingCondensed);
        Assert.Equal("lon", condensed.Unit); // phi-SI không đổi
        Assert.Equal(100m, condensed.CurrentStock);

        // ── 7. Assert: EffectiveFrom == CreatedAt (traceability) ──
        Assert.Equal(p1.CreatedAt, p1.EffectiveFrom);
    }

    [Fact]
    public async Task MigrateToLatest_OnEmptyDatabase_ShouldSucceed()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShopERPDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new ShopERPDbContext(options);
        await context.Database.MigrateAsync();

        // Backfill no-op trên DB rỗng — không crash
        Assert.Equal(0, await context.Recipes.CountAsync());
        Assert.Equal(0, await context.RecipeLines.CountAsync());
        Assert.Equal(0, await context.Shifts.CountAsync());
    }
}
