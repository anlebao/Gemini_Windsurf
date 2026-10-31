using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Reconcile;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Reconcile;

/// <summary>
/// SRS v1.2 W6/E — R13 inventory-vs-GL (task card E §5):
/// T5 stock 100tr vs GL 95tr → Warning + suggestion · T6 provider fail → Info, run vẫn pass phần còn lại ·
/// no-15x-activity → Info (tenant chưa ghi 15x) · khớp → Pass.
/// </summary>
public class R13InventoryReconcileTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-0000000000AB"));
    private static readonly AccountingPeriod Period = new(2026, 10);

    private sealed class Ctx : IDisposable
    {
        public SqliteConnection Conn { get; } = new("Data Source=:memory:");
        public VanAnDbContext Db { get; }

        public Ctx()
        {
            Conn.Open();
            Db = new VanAnDbContext(new DbContextOptionsBuilder<VanAnDbContext>().UseSqlite(Conn).Options);
            _ = Db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            Db.Dispose();
            Conn.Dispose();
        }
    }

    private sealed class FakeValuationProvider(InventoryValuationDto? dto) : IInventoryValuationProvider
    {
        public Task<InventoryValuationDto?> GetValuationAsync(Guid tenantId, string period, CancellationToken ct = default)
            => Task.FromResult(dto);
    }

    private static AccountingReconcileService Build(VanAnDbContext db, IInventoryValuationProvider? provider)
        => new(db, db, new Mock<IAuditTrailService>().Object,
            NullLogger<AccountingReconcileService>.Instance, inventoryProvider: provider);

    private static void Seed(VanAnDbContext db, params (string acct, decimal d, decimal c)[] lines)
    {
        var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(Tenant, "Test Tenant");
        typeof(VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant).GetProperty("Type")!
            .SetValue(tenant, TenantType.Enterprise_SME);
        db.Tenants.Add(tenant);

        if (lines.Length > 0)
        {
            var je = new JournalEntry(Tenant, new DateTime(2026, 10, 15), "Nhập mua test", "PurchaseReceipt", Guid.NewGuid());
            foreach (var (acct, d, c) in lines) je.AddLine(acct, d, c);
            // Cân bằng: Có 331 cho phần còn lại.
            decimal debit = lines.Sum(l => l.d) - lines.Sum(l => l.c);
            if (debit > 0) je.AddLine("331", 0, debit, "cân");
            db.JournalEntries.Add(je);
        }
        db.SaveChanges();
    }

    private static ReconcileCheck R13(ReconcileResult result)
        => result.Checks.Single(c => c.Code == "R13");

    // ── T5: stock 100tr vs GL 95tr → Warning + suggestion ────────────────────
    [Fact]
    public async Task StockVsGlDiff_Warning_WithSuggestion()
    {
        using var ctx = new Ctx();
        Seed(ctx.Db, ("156", 95_000_000m, 0));

        var svc = Build(ctx.Db, new FakeValuationProvider(new InventoryValuationDto(Tenant.Value, 5, 100_000_000m)));
        var result = await svc.RunAsync(Tenant, Period);

        var r13 = R13(result);
        Assert.Equal(ReconcileLevel.Warning, r13.Level);
        Assert.Contains("5,000,000", r13.Summary.Replace(" ", ""));
        Assert.Contains(result.Suggestions, s => s.StartsWith("R13:"));
    }

    // ── T6: provider fail (null) → Info skip, các check khác vẫn chạy ────────
    [Fact]
    public async Task ProviderFail_InfoSkip_OtherChecksRun()
    {
        using var ctx = new Ctx();
        Seed(ctx.Db, ("156", 95_000_000m, 0));

        var svc = Build(ctx.Db, new FakeValuationProvider(null));
        var result = await svc.RunAsync(Tenant, Period);

        Assert.Equal(ReconcileLevel.Info, R13(result).Level);
        Assert.Contains(result.Checks, c => c.Code == "R1");
        Assert.Contains(result.Checks, c => c.Code == "R4");
    }

    // ── Kho có giá trị nhưng GL 15x = 0 → Info (chưa ghi 15x — 6xx/611 hợp lệ) ──
    [Fact]
    public async Task StockButNoGl15x_Info()
    {
        using var ctx = new Ctx();
        Seed(ctx.Db, ("642", 50_000m, 0)); // chi phí trực tiếp — không qua 15x

        var svc = Build(ctx.Db, new FakeValuationProvider(new InventoryValuationDto(Tenant.Value, 3, 100_000_000m)));
        var result = await svc.RunAsync(Tenant, Period);

        Assert.Equal(ReconcileLevel.Info, R13(result).Level);
        Assert.DoesNotContain(result.Suggestions, s => s.StartsWith("R13:"));
    }

    // ── Khớp → Pass ──────────────────────────────────────────────────────────
    [Fact]
    public async Task Matching_Pass()
    {
        using var ctx = new Ctx();
        Seed(ctx.Db, ("156", 95_000_000m, 0));

        var svc = Build(ctx.Db, new FakeValuationProvider(new InventoryValuationDto(Tenant.Value, 4, 95_000_000m)));
        var result = await svc.RunAsync(Tenant, Period);

        Assert.Equal(ReconcileLevel.Pass, R13(result).Level);
    }

    // ── Cả hai đều 0 → Pass ──────────────────────────────────────────────────
    [Fact]
    public async Task BothZero_Pass()
    {
        using var ctx = new Ctx();
        Seed(ctx.Db);

        var svc = Build(ctx.Db, new FakeValuationProvider(new InventoryValuationDto(Tenant.Value, 0, 0)));
        var result = await svc.RunAsync(Tenant, Period);

        Assert.Equal(ReconcileLevel.Pass, R13(result).Level);
    }
}
