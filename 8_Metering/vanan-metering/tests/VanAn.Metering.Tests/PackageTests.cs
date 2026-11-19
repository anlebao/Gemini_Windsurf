using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;
using VanAn.Metering.Infra;
using Xunit;

namespace VanAn.Metering.Tests;

/// <summary>
/// Task P11 — sysadmin-defined package catalog + package top-up flow.
/// Package = fixed PriceVnd + fixed Credits + ValidityDays; confirm creates an
/// expiring credit lot (P10). Custom top-up stays non-expiring.
/// </summary>
public sealed class PackageTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private const string Caller = "shoperp-a";
    private const string Admin = "sysadmin";
    private static readonly Guid Tenant = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static (MeteringDbContext db, WalletService wallet, TopupService topups, PackageService packages) NewSut()
    {
        var db = new MeteringDbContext(new DbContextOptionsBuilder<MeteringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var rates = new RateCardService(db);
        var wallet = new WalletService(db, rates);
        var packages = new PackageService(db);
        return (db, wallet, new TopupService(db, wallet, TimeSpan.FromHours(72), packages: packages), packages);
    }

    private static CreditPackage SeedPackage(MeteringDbContext db, string code = "WEEK",
        decimal price = 200000m, decimal credits = 220m, int days = 7, bool active = true)
    {
        var pkg = CreditPackage.Create(code, $"Gói {code}", price, credits, days, active);
        db.CreditPackages.Add(pkg);
        db.SaveChanges();
        return pkg;
    }

    // ---------- catalog ----------

    [Fact]
    public async Task ListActive_ReturnsOnlyActivePackages()
    {
        var (db, _, _, packages) = NewSut();
        SeedPackage(db, "DAY", 50000m, 50m, 1);
        SeedPackage(db, "OLD", 1000m, 1m, 1, active: false);

        var list = await packages.ListActiveAsync();

        list.Should().ContainSingle(p => p.Code == "DAY");
        list.Should().NotContain(p => p.Code == "OLD");
    }

    [Fact]
    public async Task Upsert_CreatesAndUpdates()
    {
        var (_, _, _, packages) = NewSut();

        var created = await packages.UpsertAsync("MONTH", "Gói tháng", 800000m, 1000m, 30, 3, true, Admin, Now);
        created.Code.Should().Be("MONTH");

        var updated = await packages.UpsertAsync("MONTH", "Gói tháng VIP", 900000m, 1200m, 30, 3, true, Admin, Now);
        updated.Name.Should().Be("Gói tháng VIP");
        updated.PriceVnd.Should().Be(900000m);
        (await packages.ListActiveAsync()).Should().ContainSingle(p => p.Code == "MONTH" && p.Credits == 1200m);
    }

    [Fact]
    public async Task Deactivate_SoftDisables()
    {
        var (db, _, _, packages) = NewSut();
        SeedPackage(db, "YEAR");

        await packages.DeactivateAsync("YEAR", Admin, Now);

        (await packages.ListActiveAsync()).Should().BeEmpty();
        db.CreditPackages.Single().IsActive.Should().BeFalse();
    }

    // ---------- create topup with package ----------

    [Fact]
    public async Task Create_WithPackage_UsesPackagePrice()
    {
        var (db, _, topups, _) = NewSut();
        SeedPackage(db, "WEEK", 200000m, 220m, 7);

        var r = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, PackageCode: "WEEK"), Now);

        r.Request.AmountVnd.Should().Be(200000m); // giá gói, không phải amount client gửi
        r.Request.PackageCode.Should().Be("WEEK");
    }

    [Fact]
    public async Task Create_WithInactivePackage_Fails()
    {
        var (db, _, topups, _) = NewSut();
        SeedPackage(db, "OLD", active: false);

        var r = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, PackageCode: "OLD"), Now);

        r.ErrorCode.Should().Be("package_unavailable");
    }

    [Fact]
    public async Task Create_WithUnknownPackage_Fails()
    {
        var (_, _, topups, _) = NewSut();

        var r = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, PackageCode: "NOPE"), Now);

        r.ErrorCode.Should().Be("package_not_found");
    }

    [Fact]
    public async Task Create_WithoutPackage_KeepsCustomAmount()
    {
        var (_, _, topups, _) = NewSut();

        var r = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, 50000m), Now);

        r.Request.AmountVnd.Should().Be(50000m);
        r.Request.PackageCode.Should().BeNull();
    }

    // ---------- confirm → expiring lot ----------

    [Fact]
    public async Task ConfirmPaid_Package_FullPayment_GrantsPackageCredits_WithExpiry()
    {
        var (db, wallet, topups, _) = NewSut();
        var pkg = SeedPackage(db, "WEEK", 200000m, 220m, 7);
        var cfg = TopupConfig.Create("MB", "MB Bank", "123", "VANAN", Admin, Now, vndPerCredit: 1000m);
        db.TopupConfigs.Add(cfg); db.SaveChanges();

        var created = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, PackageCode: "WEEK"), Now);
        var paid = await topups.ConfirmPaidByReferenceAsync(
            created.Request.ReferenceCode, 200000m, "txn-1", Now);

        paid.Request.Status.Should().Be(TopupStatus.Confirmed);
        paid.Request.CreditsGranted.Should().Be(220m); // package credits, NOT paidAmount/1000=200
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(220m);
        var lot = db.CreditLots.Single();
        lot.ExpiresAtUtc.Should().Be(Now.AddDays(7));
        lot.CreditsRemaining.Should().Be(220m);
    }

    [Fact]
    public async Task ConfirmPaid_Package_PartialPayment_StaysPending()
    {
        var (db, wallet, topups, _) = NewSut();
        SeedPackage(db, "WEEK", 200000m, 220m, 7);
        db.TopupConfigs.Add(TopupConfig.Create("MB", "MB", "1", "V", Admin, Now, 1000m));
        db.SaveChanges();

        var created = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, PackageCode: "WEEK"), Now);
        var paid = await topups.ConfirmPaidByReferenceAsync(
            created.Request.ReferenceCode, 50000m, "txn-partial", Now); // thiếu 150k

        paid.Request.Status.Should().Be(TopupStatus.Pending); // gói phải trả đủ
        paid.Request.PaidAmountVnd.Should().Be(50000m);        // nhưng payment fact vẫn ghi
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(0m);
    }

    [Fact]
    public async Task ConfirmPaid_CustomTopup_StillPartialAllowed()
    {
        var (db, wallet, topups, _) = NewSut();
        db.TopupConfigs.Add(TopupConfig.Create("MB", "MB", "1", "V", Admin, Now, 1000m));
        db.SaveChanges();

        var created = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, 100000m), Now);
        var paid = await topups.ConfirmPaidByReferenceAsync(
            created.Request.ReferenceCode, 50000m, "txn-1", Now);

        paid.Request.Status.Should().Be(TopupStatus.Confirmed);
        paid.Request.CreditsGranted.Should().Be(50m); // 50000/1000 — partial OK cho nạp tự do
        db.CreditLots.Single().ExpiresAtUtc.Should().BeNull(); // không hết hạn
    }

    [Fact]
    public async Task ConfirmPaid_Package_ViaQrBillId_Works()
    {
        var (db, wallet, topups, _) = NewSut();
        SeedPackage(db, "DAY", 50000m, 55m, 1);
        db.TopupConfigs.Add(TopupConfig.Create("MB", "MB", "1", "V", Admin, Now, 1000m));
        db.SaveChanges();

        var created = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, PackageCode: "DAY"), Now);
        var req = await db.TopupRequests.FindAsync(created.Request.Id);
        req!.AttachQrBill("QRD-PKG-1");
        await db.SaveChangesAsync();

        var paid = await topups.ConfirmPaidByQrBillIdAsync("QRD-PKG-1", 50000m, "txn-qr", Now);

        paid.Request.Status.Should().Be(TopupStatus.Confirmed);
        paid.Request.CreditsGranted.Should().Be(55m);
        db.CreditLots.Single().ExpiresAtUtc.Should().Be(Now.AddDays(1));
    }

    [Fact]
    public async Task ConfirmPaid_Package_AfterDeactivate_StillConfirms()
    {
        var (db, wallet, topups, packages) = NewSut();
        SeedPackage(db, "WEEK", 200000m, 220m, 7);
        db.TopupConfigs.Add(TopupConfig.Create("MB", "MB", "1", "V", Admin, Now, 1000m));
        db.SaveChanges();

        var created = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, PackageCode: "WEEK"), Now);
        await packages.DeactivateAsync("WEEK", Admin, Now); // tắt SAU khi tạo lệnh

        var paid = await topups.ConfirmPaidByReferenceAsync(
            created.Request.ReferenceCode, 200000m, "txn-1", Now);

        paid.Request.Status.Should().Be(TopupStatus.Confirmed); // gói đã mua không bị ảnh hưởng
        paid.Request.CreditsGranted.Should().Be(220m);
    }

    [Fact]
    public async Task Package_Expiry_FEFO_WithCustomTopup()
    {
        var (db, wallet, topups, _) = NewSut();
        SeedPackage(db, "DAY", 50000m, 55m, 1);
        db.TopupConfigs.Add(TopupConfig.Create("MB", "MB", "1", "V", Admin, Now, 1000m));
        db.SaveChanges();

        var pkg = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, PackageCode: "DAY"), Now);
        await topups.ConfirmPaidByReferenceAsync(pkg.Request.ReferenceCode, 50000m, "t1", Now);
        var custom = await topups.CreateAsync(Caller, "k2",
            new TopupCreateRequest(Tenant, 100000m), Now);
        await topups.ConfirmPaidByReferenceAsync(custom.Request.ReferenceCode, 100000m, "t2", Now);

        // Balance = 55 (pkg) + 100 (custom) = 155
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(155m);

        // Sau 2 ngày: gói DAY hết hạn → forfeit 55 (nếu chưa dùng), custom còn 100.
        var w = await wallet.GetWalletAsync(Tenant, nowUtc: Now.AddDays(2));
        w.Balance.Should().Be(100m);
    }
}
