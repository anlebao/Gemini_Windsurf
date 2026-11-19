using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;
using VanAn.Metering.Infra;
using Xunit;

namespace VanAn.Metering.Tests;

/// <summary>
/// Task P10 — credit lots + expiry engine. FEFO consumption, lazy expiry
/// write-off via Adjust ledger entries, idempotent, append-only preserved.
/// </summary>
public sealed class CreditLotTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private const string Caller = "shoperp-a";
    private const string Admin = "sysadmin";
    private static readonly Guid Tenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (MeteringDbContext db, WalletService wallet, UsageService usage, RateCardService rates) NewSut()
    {
        var db = new MeteringDbContext(new DbContextOptionsBuilder<MeteringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var rates = new RateCardService(db);
        var wallet = new WalletService(db, rates);
        return (db, wallet, new UsageService(db, wallet, rates), rates);
    }

    private static void SeedRateCard(MeteringDbContext db, string code, decimal rate,
        MeteringEnforcement enforcement = MeteringEnforcement.Deny)
    {
        db.RateCardVersions.Add(RateCardVersion.Create(code, "event", rate, enforcement,
            Now.AddDays(-1), "seed"));
        db.SaveChanges();
    }

    // ---------- grant → lot ----------

    [Fact]
    public async Task Grant_WithExpiry_CreatesLot_WithExpiresAt()
    {
        var (db, wallet, _, _) = NewSut();
        var exp = Now.AddDays(7);

        await wallet.GrantAsync(Tenant, 100m, "package:week", Admin, Now, expiresAtUtc: exp);

        var lot = db.CreditLots.Single();
        lot.TenantId.Should().Be(Tenant);
        lot.CreditsGranted.Should().Be(100m);
        lot.CreditsRemaining.Should().Be(100m);
        lot.ExpiresAtUtc.Should().Be(exp);
        lot.ExpiredWrittenOffAtUtc.Should().BeNull();
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(100m);
    }

    [Fact]
    public async Task Grant_WithoutExpiry_CreatesNonExpiringLot()
    {
        var (db, wallet, _, _) = NewSut();

        await wallet.GrantAsync(Tenant, 50m, "topup", Admin, Now);

        db.CreditLots.Single().ExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task GetWallet_ReturnsLots()
    {
        var (db, wallet, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 100m, "pkg", Admin, Now, expiresAtUtc: Now.AddDays(7));
        await wallet.GrantAsync(Tenant, 40m, "topup", Admin, Now);

        var w = await wallet.GetWalletAsync(Tenant);

        w.Lots.Should().NotBeNull();
        w.Lots.Should().HaveCount(2);
        w.Lots!.Should().Contain(l => l.ExpiresAtUtc != null && l.CreditsRemaining == 100m);
        w.Lots.Should().Contain(l => l.ExpiresAtUtc == null && l.CreditsRemaining == 40m);
    }

    // ---------- FEFO deduction ----------

    [Fact]
    public async Task Deduct_FEFO_ConsumesEarliestExpiryFirst()
    {
        var (db, wallet, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 100m, "early", Admin, Now, expiresAtUtc: Now.AddDays(1));
        await wallet.GrantAsync(Tenant, 100m, "late", Admin, Now.AddMinutes(1), expiresAtUtc: Now.AddDays(30));

        var applied = await wallet.DeductInTransactionAsync(Tenant, Guid.NewGuid(), 150m, "use", Now);
        await db.SaveChangesAsync();

        applied.Should().Be(150m);
        var lots = db.CreditLots.OrderBy(l => l.ExpiresAtUtc).ToList();
        lots[0].CreditsRemaining.Should().Be(0m);   // early expiry drained first
        lots[1].CreditsRemaining.Should().Be(50m);
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(50m);
    }

    [Fact]
    public async Task Deduct_NonExpiringLot_ConsumedLast()
    {
        var (db, wallet, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 50m, "expiring", Admin, Now, expiresAtUtc: Now.AddDays(7));
        await wallet.GrantAsync(Tenant, 100m, "forever", Admin, Now);

        await wallet.DeductInTransactionAsync(Tenant, Guid.NewGuid(), 120m, "use", Now);
        await db.SaveChangesAsync();

        var lots = db.CreditLots.ToList();
        lots.Single(l => l.ExpiresAtUtc != null).CreditsRemaining.Should().Be(0m);
        lots.Single(l => l.ExpiresAtUtc == null).CreditsRemaining.Should().Be(30m);
    }

    [Fact]
    public async Task Deduct_ClampsToTotalRemaining()
    {
        var (db, wallet, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 30m, "a", Admin, Now);
        await wallet.GrantAsync(Tenant, 20m, "b", Admin, Now);

        var applied = await wallet.DeductInTransactionAsync(Tenant, Guid.NewGuid(), 100m, "use", Now);
        await db.SaveChangesAsync();

        applied.Should().Be(50m);
        db.CreditLots.Sum(l => l.CreditsRemaining).Should().Be(0m);
    }

    // ---------- expiry write-off ----------

    [Fact]
    public async Task Expiry_WriteOff_RemovesRemaining_AndLedgerAdjust()
    {
        var (db, wallet, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 100m, "pkg", Admin, Now, expiresAtUtc: Now.AddHours(1));
        var future = Now.AddHours(2);

        var w = await wallet.GetWalletAsync(Tenant, nowUtc: future);

        w.Balance.Should().Be(0m);
        var lot = db.CreditLots.Single();
        lot.CreditsRemaining.Should().Be(0m);
        lot.ExpiredWrittenOffAtUtc.Should().NotBeNull();
        db.CreditLedgerEntries.Should().Contain(e =>
            e.Type == LedgerEntryType.Adjust && e.Credits == -100m && e.Reason.StartsWith("expired:"));
    }

    [Fact]
    public async Task Expiry_Idempotent_NoDoubleWriteOff()
    {
        var (db, wallet, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 100m, "pkg", Admin, Now, expiresAtUtc: Now.AddHours(1));
        var future = Now.AddHours(2);

        await wallet.GetWalletAsync(Tenant, nowUtc: future);
        await wallet.GetWalletAsync(Tenant, nowUtc: future);
        await wallet.CheckAsync(Tenant, "x", null, future);

        db.CreditLedgerEntries.Count(e => e.Type == LedgerEntryType.Adjust).Should().Be(1);
        db.CreditLots.Single().CreditsRemaining.Should().Be(0m);
    }

    [Fact]
    public async Task Expiry_OnlyForfeitsUnusedRemainder()
    {
        var (db, wallet, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 100m, "pkg", Admin, Now, expiresAtUtc: Now.AddHours(2));
        await wallet.DeductInTransactionAsync(Tenant, Guid.NewGuid(), 30m, "use", Now);
        await db.SaveChangesAsync();

        var w = await wallet.GetWalletAsync(Tenant, nowUtc: Now.AddHours(3));

        w.Balance.Should().Be(0m);
        db.CreditLedgerEntries.Should().Contain(e =>
            e.Type == LedgerEntryType.Adjust && e.Credits == -70m);
    }

    [Fact]
    public async Task Expiry_NonExpiringLot_Unaffected()
    {
        var (db, wallet, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 50m, "pkg", Admin, Now, expiresAtUtc: Now.AddHours(1));
        await wallet.GrantAsync(Tenant, 80m, "topup", Admin, Now);

        var w = await wallet.GetWalletAsync(Tenant, nowUtc: Now.AddHours(2));

        w.Balance.Should().Be(80m);
        db.CreditLots.Single(l => l.ExpiresAtUtc == null).CreditsRemaining.Should().Be(80m);
    }

    [Fact]
    public async Task Check_Deny_AfterExpiry()
    {
        var (db, wallet, _, rates) = NewSut();
        SeedRateCard(db, "store.visit", 1m, MeteringEnforcement.Deny);
        await wallet.GrantAsync(Tenant, 10m, "pkg", Admin, Now, expiresAtUtc: Now.AddHours(1));

        var before = await wallet.CheckAsync(Tenant, "store.visit", null, Now);
        before.Deny.Should().BeFalse();

        var after = await wallet.CheckAsync(Tenant, "store.visit", null, Now.AddHours(2));
        after.Deny.Should().BeTrue();
        after.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task MixedLots_Expiry_ThenFEFO_StillWorks()
    {
        var (db, wallet, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 100m, "old-pkg", Admin, Now, expiresAtUtc: Now.AddHours(1));
        await wallet.GrantAsync(Tenant, 100m, "new-pkg", Admin, Now, expiresAtUtc: Now.AddDays(30));
        await wallet.GrantAsync(Tenant, 50m, "topup", Admin, Now);

        // Expire old-pkg first.
        await wallet.GetWalletAsync(Tenant, nowUtc: Now.AddHours(2));
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(150m);

        // Remaining spendable: new-pkg (30d) before non-expiring.
        await wallet.DeductInTransactionAsync(Tenant, Guid.NewGuid(), 120m, "use", Now.AddHours(2));
        await db.SaveChangesAsync();

        db.CreditLots.Single(l => l.ExpiresAtUtc == Now.AddDays(30)).CreditsRemaining.Should().Be(0m);
        db.CreditLots.Single(l => l.ExpiresAtUtc == null).CreditsRemaining.Should().Be(30m);
    }

    [Fact]
    public async Task Adjust_Debit_DoesNotOverdrawLots()
    {
        var (db, wallet, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 30m, "a", Admin, Now);

        await wallet.AdjustAsync(Tenant, -100m, "reversal", Admin, Now);
        await db.SaveChangesAsync();

        db.CreditLots.Single().CreditsRemaining.Should().Be(0m);
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(0m);
    }
}
