using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Reconcile;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using Xunit;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.Core.Tests.Reconcile;

/// <summary>
/// P1 — AccountingReconcileService (tra soát số liệu kế toán, SRS v1.1).
/// Read-only (KHÔNG ghi DB) · fail-safe từng check (NFR-4) · tolerance 0.01.
/// Case: T1 (R1) · T3 (R10) · T4 (R3/R8b/R9) · T5 (R12) · T6 (R7) · T8 (R4 HTX 512/612) ·
/// T9 (R4-sub 911) · T10 (fail-safe) · R8a (Order-ref post-fix) · R11 (fail-open).
/// </summary>
public class AccountingReconcileServiceTests
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

    private static AccountingReconcileService Build(VanAnDbContext db, IMirrorOrderProvider? mirror = null)
        => new(db, db, new Mock<IAuditTrailService>().Object, NullLogger<AccountingReconcileService>.Instance, mirror);

    private static void SeedTenant(VanAnDbContext db, TenantType type, string name = "Test Tenant")
    {
        var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(Tenant, name);
        typeof(VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant).GetProperty("Type")!.SetValue(tenant, type);
        db.Tenants.Add(tenant);
    }

    private static JournalEntry Journal(string desc, DateTime date, string refType, Guid refId,
        params (string acct, decimal d, decimal c)[] lines)
    {
        var je = new JournalEntry(Tenant, date, desc, refType, refId);
        foreach (var (acct, d, c) in lines) je.AddLine(acct, d, c);
        return je;
    }

    private static void SeedPaidOrder(VanAnDbContext db, Guid orderId, decimal subTotal, decimal vat, decimal cogs,
        decimal deposit = 0, string status = "confirmed")
    {
        // OrderItem.Product navigation → EF convention tạo FK ProductId → Products.Id — phải seed Product trước.
        var product = new Product(Tenant, "Sản phẩm test", "Test", subTotal, "Nhóm test");
        db.Products.Add(product);
        var item = OrderItem.Create(Guid.NewGuid(), Tenant, orderId, product.Id, 1, subTotal, product.Name);
        var order = Order.Create(orderId, Tenant, null, [item]);
        order.ConfirmPayment("txn", "Cash");
        if (deposit > 0) order.SetDepositPaidAmount(deposit);
        typeof(Order).GetProperty("PaymentStatus")!.SetValue(order, "Paid");
        typeof(Order).GetProperty("Status")!.SetValue(order, new OrderStatusId(status));
        typeof(Order).GetProperty("CreatedAt")!.SetValue(order, new DateTime(2026, 10, 15));
        db.Orders.Add(order);
        // entries (reference = orderId) — 511 = net − cọc (semantic production, R3: revJ == entries revenue + offset)
        db.AccountingEntries.Add(CoreAccountingEntry.CreateRevenue(Tenant, Period, new Money(subTotal - deposit), "Doanh thu", accountCode: "511", reference: orderId.ToString(), transactionDate: new DateTime(2026, 10, 15)));
        if (vat > 0)
            db.AccountingEntries.Add(CoreAccountingEntry.CreateRevenue(Tenant, Period, new Money(vat), "VAT", accountCode: "3331", reference: orderId.ToString(), transactionDate: new DateTime(2026, 10, 15)));
        if (cogs > 0)
            db.AccountingEntries.Add(CoreAccountingEntry.CreateExpense(Tenant, Period, new Money(cogs), "COGS", accountCode: "632", reference: orderId.ToString(), transactionDate: new DateTime(2026, 10, 15)));
        if (deposit > 0)
            db.AccountingEntries.Add(CoreAccountingEntry.CreateDepositOffset(Tenant, Period, new Money(deposit), "Offset cọc", accountCode: "511", reference: $"{orderId}-DEPOSIT-OFFSET", transactionDate: new DateTime(2026, 10, 15)));
    }

    // ── T1: journal lệch Nợ/Có → R1 Error ───────────────────────────────────
    [Fact]
    public async Task R1_UnbalancedJournal_Error()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.HKD);
        c.Db.JournalEntries.Add(Journal("Lệch", new DateTime(2026, 10, 5), "ManualEntry", Guid.NewGuid(), ("111", 100m, 0m), ("511", 0m, 99m)));
        _ = await c.Db.SaveChangesAsync();

        var r = await Build(c.Db).RunAsync(Tenant, Period);
        r.Checks.First(x => x.Code == "R1").Level.Should().Be(ReconcileLevel.Error);
        r.Checks.First(x => x.Code == "R2").Level.Should().Be(ReconcileLevel.Error); // trial balance cũng lệch
    }

    // ── T4: order paid đủ entries + journal (revenue+COGS+offset) → pass ────
    [Fact]
    public async Task R3_R8b_R10_DepositOrderFull_Pass()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.HKD);
        Guid orderId = Guid.NewGuid();
        SeedPaidOrder(c.Db, orderId, subTotal: 100_000m, vat: 10_000m, cogs: 70_000m, deposit: 10_000m);
        _ = await c.Db.SaveChangesAsync(); // entries cần lưu trước khi query lấy Id
        // journals: revenue (Nợ 111/Có 511 90.000) + offset (Nợ 3387/Có 511 10.000) + COGS (Nợ 632/Có 111 70.000)
        var revenueEntry = c.Db.AccountingEntries.First(e => e.AccountCode == "511" && e.Reference == orderId.ToString());
        var cogsEntry = c.Db.AccountingEntries.First(e => e.AccountCode == "632");
        var offEntry = c.Db.AccountingEntries.First(e => e.EntryType == AccountingEntryType.DepositOffset);
        c.Db.JournalEntries.Add(Journal("Doanh thu", new DateTime(2026, 10, 15), "ManualEntry", revenueEntry.Id, ("111", 90_000m, 0m), ("511", 0m, 90_000m)));
        c.Db.JournalEntries.Add(Journal("Offset", new DateTime(2026, 10, 15), "ManualEntry", offEntry.Id, ("3387", 10_000m, 0m), ("511", 0m, 10_000m)));
        c.Db.JournalEntries.Add(Journal("COGS", new DateTime(2026, 10, 15), "ManualEntry", cogsEntry.Id, ("632", 70_000m, 0m), ("111", 0m, 70_000m)));
        _ = await c.Db.SaveChangesAsync();

        var r = await Build(c.Db).RunAsync(Tenant, Period);
        r.Checks.First(x => x.Code == "R3").Level.Should().Be(ReconcileLevel.Pass);
        r.Checks.First(x => x.Code == "R8b").Level.Should().Be(ReconcileLevel.Pass);
        r.Checks.First(x => x.Code == "R10").Level.Should().Be(ReconcileLevel.Pass);
    }

    // ── T8: HTX nội bộ 512/612 → R3/R4 pass ─────────────────────────────────
    [Fact]
    public async Task R3_R4_HtxInternal512_612_Pass()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.HTX);
        c.Db.JournalEntries.Add(Journal("Doanh thu nội bộ", new DateTime(2026, 10, 5), "ManualEntry", Guid.NewGuid(), ("111", 200_000m, 0m), ("512", 0m, 200_000m)));
        c.Db.JournalEntries.Add(Journal("COGS nội bộ", new DateTime(2026, 10, 5), "ManualEntry", Guid.NewGuid(), ("612", 120_000m, 0m), ("111", 0m, 120_000m)));
        c.Db.AccountingEntries.Add(CoreAccountingEntry.CreateRevenue(Tenant, Period, new Money(200_000m), "DT", accountCode: "512", transactionDate: new DateTime(2026, 10, 5)));
        c.Db.AccountingEntries.Add(CoreAccountingEntry.CreateExpense(Tenant, Period, new Money(120_000m), "COGS", accountCode: "612", transactionDate: new DateTime(2026, 10, 5)));
        _ = await c.Db.SaveChangesAsync();

        var r = await Build(c.Db).RunAsync(Tenant, Period);
        r.Checks.First(x => x.Code == "R3").Level.Should().Be(ReconcileLevel.Pass);
        r.Checks.First(x => x.Code == "R4").Level.Should().Be(ReconcileLevel.Pass);
    }

    // ── T9: account 911 kết chuyển → R4-sub Warning ─────────────────────────
    [Fact]
    public async Task R4Sub_Account911_Warning()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.HKD);
        c.Db.JournalEntries.Add(Journal("Kết chuyển", new DateTime(2026, 10, 31), "ManualEntry", Guid.NewGuid(), ("911", 100_000m, 0m), ("511", 0m, 100_000m)));
        _ = await c.Db.SaveChangesAsync();

        var r = await Build(c.Db).RunAsync(Tenant, Period);
        var sub = r.Checks.FirstOrDefault(x => x.Code == "R4-sub");
        sub.Should().NotBeNull();
        sub!.Level.Should().Be(ReconcileLevel.Warning);
        sub.Summary.Should().Contain("911");
    }

    // ── T5: TransactionDate xấu → R12 Warning ───────────────────────────────
    [Fact]
    public async Task R12_BadTransactionDate_Warning()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.HKD);
        c.Db.AccountingEntries.Add(CoreAccountingEntry.CreateRevenue(Tenant, Period, new Money(10_000m), "DT",
            accountCode: "511", transactionDate: new DateTime(1, 1, 1)));
        _ = await c.Db.SaveChangesAsync();

        var r = await Build(c.Db).RunAsync(Tenant, Period);
        r.Checks.First(x => x.Code == "R12").Level.Should().Be(ReconcileLevel.Warning);
    }

    // ── T6: tenant HKD không journal kỳ → R7 Warning ────────────────────────
    [Fact]
    public async Task R7_HkdNoJournal_Warning()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.HKD);
        _ = await c.Db.SaveChangesAsync();

        var r = await Build(c.Db).RunAsync(Tenant, Period);
        r.Checks.First(x => x.Code == "R7").Level.Should().Be(ReconcileLevel.Warning);
    }

    // ── R7: tenant không phải HKD → Info (bỏ qua) ───────────────────────────
    [Fact]
    public async Task R7_NonHkd_Info()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.Enterprise_SME);
        _ = await c.Db.SaveChangesAsync();

        var r = await Build(c.Db).RunAsync(Tenant, Period);
        r.Checks.First(x => x.Code == "R7").Level.Should().Be(ReconcileLevel.Info);
    }

    // ── R8a: journal ref 'Order' sau ngày fix → Error ───────────────────────
    [Fact]
    public async Task R8a_OrderRefAfterFix_Error()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.HKD);
        c.Db.JournalEntries.Add(Journal("Doanh thu", new DateTime(2026, 10, 29, 12, 0, 0), "Order", Guid.NewGuid(), ("111", 100m, 0m), ("511", 0m, 100m)));
        _ = await c.Db.SaveChangesAsync();

        var r = await Build(c.Db).RunAsync(Tenant, Period);
        r.Checks.First(x => x.Code == "R8a").Level.Should().Be(ReconcileLevel.Error);
    }

    // ── R11: không provider → Info (fail-open) ──────────────────────────────
    [Fact]
    public async Task R11_NoProvider_Info()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.HKD);
        _ = await c.Db.SaveChangesAsync();

        var r = await Build(c.Db).RunAsync(Tenant, Period);
        r.Checks.First(x => x.Code == "R11").Level.Should().Be(ReconcileLevel.Info);
    }

    // ── T10: fail-safe — mirror provider throw → R11 error, check khác chạy tiếp ──
    [Fact]
    public async Task FailSafe_ProviderThrows_R11Error_OthersRun()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.HKD);
        c.Db.JournalEntries.Add(Journal("Doanh thu", new DateTime(2026, 10, 5), "ManualEntry", Guid.NewGuid(), ("111", 100m, 0m), ("511", 0m, 100m)));
        _ = await c.Db.SaveChangesAsync();
        var throwing = new ThrowingMirror();

        var r = await Build(c.Db, throwing).RunAsync(Tenant, Period);
        r.Checks.First(x => x.Code == "R11").Level.Should().Be(ReconcileLevel.Error); // exception → error
        r.Checks.First(x => x.Code == "R1").Level.Should().Be(ReconcileLevel.Pass);    // check khác vẫn chạy
    }

    // ── Read-only guard (NFR-1): service không gọi SaveChanges ──────────────
    [Fact]
    public async Task ReadOnly_NoWriteToDb()
    {
        using var c = new Ctx();
        SeedTenant(c.Db, TenantType.HKD);
        c.Db.JournalEntries.Add(Journal("Doanh thu", new DateTime(2026, 10, 5), "ManualEntry", Guid.NewGuid(), ("111", 100m, 0m), ("511", 0m, 100m)));
        _ = await c.Db.SaveChangesAsync();

        _ = await Build(c.Db).RunAsync(Tenant, Period);
        // ChangeTracker không có entry mới nào (service chỉ đọc)
        c.Db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .Should().Be(0);
    }

    private sealed class ThrowingMirror : IMirrorOrderProvider
    {
        public Task<IReadOnlyList<MirrorOrderDto>?> GetMirrorOrdersAsync(string period, CancellationToken ct = default)
            => throw new InvalidOperationException("mirror boom");
    }
}