using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.ShopERP.Tests.Services;

/// <summary>
/// SRS v1.2 W6 tiếp (GĐ2): StockMovementService — nguồn audit tồn kho.
/// PurchaseIn tăng tồn + cập nhật giá mới nhất · idempotent retry · trả hàng giảm tồn ·
/// nhập/xuất tay · báo cáo NXT rebuild đúng baseline (tồn có sẵn trước movement).
/// </summary>
public class StockMovementServiceTests : IDisposable
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private readonly SqliteConnection _connection;
    private readonly ShopERPDbContext _ctx;
    private readonly StockMovementService _svc;

    public StockMovementServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _ctx = new ShopERPDbContext(new DbContextOptionsBuilder<ShopERPDbContext>().UseSqlite(_connection).Options);
        _ctx.Database.EnsureCreated();
        _svc = new StockMovementService(_ctx, NullLogger<StockMovementService>.Instance);
    }

    public void Dispose() { _ctx.Dispose(); _connection.Dispose(); }

    private Ingredient SeedIngredient(decimal stock = 0, decimal price = 0)
    {
        var ing = new Ingredient(Tenant, "Gạo", "kg", stock, 1, price);
        _ctx.Ingredients.Add(ing);
        _ctx.SaveChanges();
        return ing;
    }

    private PurchaseReceiptEntity SeedReceipt(params (string name, decimal qty, decimal price, Guid? ingId)[] lines)
    {
        var receipt = new PurchaseReceiptEntity(Tenant, "NM-2026-00001", "NCC A", null,
            DateTime.Today, PurchasePayMethod.Credit, null, "156");
        foreach (var (name, qty, price, ingId) in lines)
            receipt.AddLine(name, qty, price, ingId);
        return receipt;
    }

    [Fact]
    public async Task ApplyReceiptStock_IncreasesStock_AndUpdatesLatestCost()
    {
        var ing = SeedIngredient(stock: 5, price: 40_000);
        var receipt = SeedReceipt(("Gạo", 10m, 52_000m, ing.Id), ("Phí khác", 1, 5_000, null));

        int written = await _svc.ApplyReceiptStockAsync(receipt);

        written.Should().Be(1); // chỉ dòng link, bỏ dòng tự do
        var reloaded = await _ctx.Ingredients.IgnoreQueryFilters().FirstAsync(i => i.Id == ing.Id);
        reloaded.CurrentStock.Should().Be(15);
        reloaded.PricePerUnit.Should().Be(52_000); // giá nhập mới nhất
        var m = await _ctx.StockMovements.IgnoreQueryFilters().SingleAsync(m => m.IngredientId == ing.Id);
        m.MovementType.Should().Be(StockMovementType.PurchaseIn);
        m.PurchaseReceiptId.Should().Be(receipt.Id);
        m.UnitCost.Should().Be(52_000);
    }

    [Fact]
    public async Task ApplyReceiptStock_Idempotent_SecondCallWritesNothing()
    {
        var ing = SeedIngredient();
        var receipt = SeedReceipt(("Gạo", 10m, 50_000m, ing.Id));

        (await _svc.ApplyReceiptStockAsync(receipt)).Should().Be(1);
        (await _svc.ApplyReceiptStockAsync(receipt)).Should().Be(0);
        (await _ctx.Ingredients.IgnoreQueryFilters().FirstAsync(i => i.Id == ing.Id)).CurrentStock.Should().Be(10);
    }

    [Fact]
    public async Task ApplyReturnStock_DecreasesStock_Idempotent()
    {
        var ing = SeedIngredient();
        var receipt = SeedReceipt(("Gạo", 10m, 50_000m, ing.Id));
        await _svc.ApplyReceiptStockAsync(receipt);

        (await _svc.ApplyReturnStockAsync(receipt)).Should().Be(1);
        (await _svc.ApplyReturnStockAsync(receipt)).Should().Be(0); // lần 2 không trùng
        (await _ctx.Ingredients.IgnoreQueryFilters().FirstAsync(i => i.Id == ing.Id)).CurrentStock.Should().Be(0);
    }

    [Fact]
    public async Task RecordManual_InOut_AndNegativeGuard()
    {
        var ing = SeedIngredient(stock: 5);

        await _svc.RecordManualAsync(Tenant, ing.Id, isIn: true, 10, null, "nhập tay", "user1");
        (await _ctx.Ingredients.IgnoreQueryFilters().FirstAsync(i => i.Id == ing.Id)).CurrentStock.Should().Be(15);

        var act = () => _svc.RecordManualAsync(Tenant, ing.Id, isIn: false, 20, null, null, null);
        await act.Should().ThrowAsync<InvalidOperationException>(); // xuất vượt tồn

        await _svc.RecordManualAsync(Tenant, ing.Id, isIn: false, 5, null, "xuất tay", "user1");
        (await _ctx.Ingredients.IgnoreQueryFilters().FirstAsync(i => i.Id == ing.Id)).CurrentStock.Should().Be(10);
        (await _ctx.StockMovements.IgnoreQueryFilters().CountAsync(m => m.IngredientId == ing.Id)).Should().Be(2);
    }

    [Fact]
    public async Task StockReport_RebuildsBaseline_AndComputesPeriod()
    {
        // Tồn 100 có sẵn TRƯỚC khi có movement (baseline) + nhập mua 10 trong kỳ + xuất tay 3.
        var ing = SeedIngredient(stock: 107, price: 50_000);
        var receipt = SeedReceipt(("Gạo", 10m, 50_000m, ing.Id));
        var from = DateTime.Today.AddDays(-1);
        var to = DateTime.Today.AddDays(1);
        await _svc.ApplyReceiptStockAsync(receipt);                    // +10 → 117
        await _svc.RecordManualAsync(Tenant, ing.Id, false, 3, null, null, null); // −3 → 114

        var rows = await _svc.GetStockReportAsync(Tenant, from, to);
        var row = rows.Should().ContainSingle().Subject;

        // baseline = 114 − (10 − 3) = 107 → opening 107, in 10, out 3, closing 114 = CurrentStock ✓
        row.OpeningQty.Should().Be(107);
        row.InQty.Should().Be(10);
        row.OutQty.Should().Be(3);
        row.ClosingQty.Should().Be(114);
        row.ClosingValue.Should().Be(114 * 50_000);
    }
}
