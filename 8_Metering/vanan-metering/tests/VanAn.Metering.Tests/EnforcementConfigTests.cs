using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;
using VanAn.Metering.Infra;
using Xunit;

namespace VanAn.Metering.Tests;

/// <summary>
/// Issue #197 — master switch toàn hệ thống (M7–M12). Toggle OFF → check deny=false
/// + ingest không deduct (usage fact vẫn ghi) + không alert; ON → hành vi cũ.
/// M11 (PUT non-admin → 403) là HTTP-layer — verify ở deployment smoke (pattern T27).
/// </summary>
public sealed class EnforcementConfigTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc);
    private const string Caller = "shoperp-a";
    private const string Admin = "sysadmin";
    private static readonly Guid Tenant = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static (MeteringDbContext db, WalletService wallet, UsageService usage, EnforcementConfigService cfg) NewSut()
    {
        var db = new MeteringDbContext(new DbContextOptionsBuilder<MeteringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var rates = new RateCardService(db);
        var cfg = new EnforcementConfigService(db);
        var wallet = new WalletService(db, rates, cfg);
        return (db, wallet, new UsageService(db, wallet, rates, cfg), cfg);
    }

    private static void SeedDenyCard(MeteringDbContext db, string code = "feature.booking", decimal rate = 30m)
    {
        db.RateCardVersions.Add(RateCardVersion.Create(code, "event", rate,
            MeteringEnforcement.Deny, Now.AddDays(-1), "seed"));
        db.SaveChanges();
    }

    private static UsageEventRequest Req(string code = "feature.booking", decimal qty = 1)
        => new(Tenant, code, qty, "event", Now);

    [Fact] // M7 — row chưa tồn tại → enabled (default ON)
    public async Task IsEnabled_RowMissing_DefaultsTrue()
    {
        var (_, _, _, cfg) = NewSut();
        (await cfg.IsEnabledAsync()).Should().BeTrue();
        (await cfg.GetAsync()).Enabled.Should().BeTrue();
    }

    [Fact] // M8 — OFF → check deny=false dù card Deny + ví 0
    public async Task Check_MasterOff_DenyAlwaysFalse()
    {
        var (db, wallet, _, cfg) = NewSut();
        SeedDenyCard(db);
        await cfg.SetAsync(false, Admin, Now);

        var c = await wallet.CheckAsync(Tenant, "feature.booking", null, Now);
        c.Deny.Should().BeFalse();
        c.Required.Should().Be(30m);   // vẫn báo required để UI hiển thị
        c.Enforcement.Should().Be("Deny");
    }

    [Fact] // M9 — OFF → ingest không deduct, usage vẫn ghi (fact)
    public async Task Ingest_MasterOff_RecordsFact_NoDeduct()
    {
        var (db, wallet, usage, cfg) = NewSut();
        SeedDenyCard(db);
        await wallet.GrantAsync(Tenant, 100m, "initial", Admin, Now);
        await cfg.SetAsync(false, Admin, Now);

        var r = await usage.IngestAsync(Caller, "e1", Req(), Now);

        r.ErrorCode.Should().BeNull();
        var ev = db.UsageEvents.Single();
        ev.CreditsApplied.Should().BeNull();
        ev.UnpaidCredits.Should().BeNull();
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(100m);
        db.CreditLedgerEntries.Should().OnlyContain(e => e.Type == LedgerEntryType.Grant);
        db.WalletAlerts.Should().BeEmpty();
    }

    [Fact] // M10 — ON (bật lại) → deny + deduct quay lại
    public async Task CheckAndIngest_MasterOn_EnforceAgain()
    {
        var (db, wallet, usage, cfg) = NewSut();
        SeedDenyCard(db, rate: 100m); // required = 100
        await wallet.GrantAsync(Tenant, 50m, "initial", Admin, Now); // 50 < 100 → deny khi ON
        await cfg.SetAsync(false, Admin, Now);
        (await wallet.CheckAsync(Tenant, "feature.booking", null, Now)).Deny.Should().BeFalse();

        await cfg.SetAsync(true, Admin, Now.AddMinutes(1));

        (await wallet.CheckAsync(Tenant, "feature.booking", null, Now.AddMinutes(2))).Deny.Should().BeTrue();
        await usage.IngestAsync(Caller, "e2", Req(), Now.AddMinutes(2));
        db.UsageEvents.Single().CreditsApplied.Should().Be(50m); // clamp tại balance
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(0m);
    }

    [Fact] // M12 — SetAsync singleton + audit (1 row, update không insert mới)
    public async Task SetAsync_Singleton_WritesAudit()
    {
        var (db, _, _, cfg) = NewSut();

        var first = await cfg.SetAsync(false, Admin, Now);
        var second = await cfg.SetAsync(true, "other-admin", Now.AddMinutes(1));

        db.EnforcementConfigs.Count().Should().Be(1);
        second.Enabled.Should().BeTrue();
        second.UpdatedBy.Should().Be("other-admin");
        var audits = await db.AuditLogs.OrderBy(a => a.AtUtc).ToListAsync();
        audits.Should().HaveCount(2);
        audits[0].Action.Should().Be("disable");
        audits[1].Action.Should().Be("enable");
        audits[1].Actor.Should().Be("other-admin");
        audits[1].OldJson.Should().Contain("false");
        audits[1].NewJson.Should().Contain("true");
    }
}