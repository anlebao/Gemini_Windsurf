using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;
using VanAn.Metering.Infra;
using Xunit;

namespace VanAn.Metering.Tests;

/// <summary>
/// Task P7 test matrix T01–T30 — EF InMemory for service logic; SQLite shared-cache
/// in-memory for real unique-index/race enforcement (T07, T25). T27 (403) is HTTP-layer
/// and verified in the deployment smoke run.
/// </summary>
public sealed class WalletServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
    private const string Caller = "shoperp-a";
    private const string Admin = "sysadmin";
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static (MeteringDbContext db, WalletService wallet, UsageService usage, RateCardService rates, TopupService topups) NewSut()
    {
        var db = new MeteringDbContext(new DbContextOptionsBuilder<MeteringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var rates = new RateCardService(db);
        var wallet = new WalletService(db, rates);
        return (db, wallet, new UsageService(db, wallet, rates), rates,
            new TopupService(db, wallet, TimeSpan.FromHours(72)));
    }

    private static void SeedRateCard(MeteringDbContext db, string code, decimal rate,
        MeteringEnforcement enforcement = MeteringEnforcement.MeterOnly,
        DateTime? effectiveFrom = null)
    {
        db.RateCardVersions.Add(RateCardVersion.Create(code, "event", rate, enforcement,
            effectiveFrom ?? Now.AddDays(-1), "seed"));
        db.SaveChanges();
    }

    private static UsageEventRequest Req(Guid tenant, string code = "store.visit",
        decimal qty = 1, DateTime? occurred = null)
        => new(tenant, code, qty, "event", occurred ?? Now);

    // ---------- D1/D2: grant + deduct ----------

    [Fact] // T01 — grant → balance + ledger
    public async Task Grant_IncreasesBalance_AndWritesLedger()
    {
        var (db, wallet, _, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 100m, "initial", Admin, Now);

        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(100m);
        var ledger = db.CreditLedgerEntries.Single();
        ledger.Type.Should().Be(LedgerEntryType.Grant);
        ledger.Credits.Should().Be(100m);
        ledger.BalanceAfter.Should().Be(100m);
        db.AuditLogs.Single(a => a.Action == "grant").Actor.Should().Be(Admin);
    }

    [Fact] // T02 — priced event deducts qty × rate
    public async Task Ingest_PricedEvent_DeductsCorrectRate()
    {
        var (db, wallet, usage, _, _) = NewSut();
        SeedRateCard(db, "chat.message", 3m);
        await wallet.GrantAsync(Tenant, 100m, "initial", Admin, Now);

        var r = await usage.IngestAsync(Caller, "e1", Req(Tenant, "chat.message", qty: 5), Now);

        r.ErrorCode.Should().BeNull();
        var ev = db.UsageEvents.Single();
        ev.CreditsApplied.Should().Be(15m);
        ev.UnpaidCredits.Should().BeNull();
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(85m);
        db.CreditLedgerEntries.Should().Contain(e =>
            e.Type == LedgerEntryType.Deduct && e.Credits == -15m && e.UsageEventId == ev.Id);
    }

    [Fact] // T03 — deduction over balance → unpaid flag, balance clamped at 0
    public async Task Ingest_InsufficientBalance_UnpaidFlag()
    {
        var (db, wallet, usage, _, _) = NewSut();
        SeedRateCard(db, "chat.bytes", 1m);
        await wallet.GrantAsync(Tenant, 10m, "initial", Admin, Now);

        await usage.IngestAsync(Caller, "e1", Req(Tenant, "chat.bytes", qty: 25), Now);

        var ev = db.UsageEvents.Single();
        ev.CreditsApplied.Should().Be(10m);
        ev.UnpaidCredits.Should().Be(15m);
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(0m);
    }

    [Fact] // T04 — replay ingest must not deduct twice
    public async Task Ingest_Replay_NoDoubleDeduct()
    {
        var (db, wallet, usage, _, _) = NewSut();
        SeedRateCard(db, "store.visit", 1m);
        await wallet.GrantAsync(Tenant, 100m, "initial", Admin, Now);

        var first = await usage.IngestAsync(Caller, "same-key", Req(Tenant), Now);
        var replay = await usage.IngestAsync(Caller, "same-key", Req(Tenant, qty: 99), Now);

        replay.Replay.Should().BeTrue();
        replay.EventId.Should().Be(first.EventId);
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(99m);
        db.CreditLedgerEntries.Count(e => e.Type == LedgerEntryType.Deduct).Should().Be(1);
    }

    [Fact] // T05 — unpriced event → fact recorded, no deduction
    public async Task Ingest_Unpriced_NoDeduction()
    {
        var (db, wallet, usage, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 50m, "initial", Admin, Now);

        var r = await usage.IngestAsync(Caller, "e1", Req(Tenant, "unpriced.code"), Now);

        r.ErrorCode.Should().BeNull();
        db.UsageEvents.Single().CreditsApplied.Should().BeNull();
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(50m);
        db.CreditLedgerEntries.Should().OnlyContain(e => e.Type == LedgerEntryType.Grant);
    }

    [Fact] // T06 + T12 — check on missing tenant → zero balance, no crash (lazy wallet)
    public async Task Check_MissingWallet_ZeroBalance_DenyOnlyForDenyCard()
    {
        var (_, wallet, _, _, _) = NewSut();
        var (db2, wallet2, _, _, _) = NewSut();
        SeedRateCard(db2, "feature.booking", 5m, MeteringEnforcement.Deny);

        var c = await wallet.CheckAsync(Tenant, "unpriced.code", null, Now);
        c.Balance.Should().Be(0m);
        c.Deny.Should().BeFalse();

        var c2 = await wallet2.CheckAsync(Tenant, "feature.booking", null, Now);
        c2.Required.Should().Be(5m);
        c2.Deny.Should().BeTrue();
    }

    [Fact] // T07 — concurrent deductions never take balance negative (SQLite real index)
    public async Task Deduct_Concurrent_NeverNegative()
    {
        await using var conn = new SqliteConnection("Data Source=p7t07;Mode=Memory;Cache=Shared");
        await conn.OpenAsync();
        var opts = new DbContextOptionsBuilder<MeteringDbContext>().UseSqlite(conn).Options;

        await using var seed = new MeteringDbContext(opts);
        await seed.Database.EnsureCreatedAsync();
        await new WalletService(seed, new RateCardService(seed))
            .GrantAsync(Tenant, 100m, "initial", Admin, Now);

        async Task<decimal> TryDeduct()
        {
            await using var db = new MeteringDbContext(opts);
            var w = new WalletService(db, new RateCardService(db));
            var applied = await w.DeductInTransactionAsync(Tenant, Guid.NewGuid(), 60m, "t", Now);
            await db.SaveChangesAsync();
            return applied;
        }

        var results = await Task.WhenAll(TryDeduct(), TryDeduct());
        results.Sum().Should().Be(100m); // 60 + clamped 40
        results.Should().Contain(60m).And.Contain(40m);

        await using var verify = new MeteringDbContext(opts);
        var w3 = await verify.CreditWallets.SingleAsync(x => x.TenantId == Tenant);
        w3.Balance.Should().Be(0m);
    }

    // ---------- D3: check / deny ----------

    [Fact] // T08 — MeterOnly never denies
    public async Task Check_MeterOnly_NeverDenies()
    {
        var (db, wallet, _, _, _) = NewSut();
        SeedRateCard(db, "store.visit", 1m, MeteringEnforcement.MeterOnly);

        var c = await wallet.CheckAsync(Tenant, "store.visit", null, Now);
        c.Deny.Should().BeFalse();
        c.Enforcement.Should().Be("MeterOnly");
    }

    [Fact] // T09 — Deny card + sufficient balance → deny false
    public async Task Check_DenyCard_SufficientBalance_Allows()
    {
        var (db, wallet, _, _, _) = NewSut();
        SeedRateCard(db, "feature.booking", 5m, MeteringEnforcement.Deny);
        await wallet.GrantAsync(Tenant, 10m, "initial", Admin, Now);

        var c = await wallet.CheckAsync(Tenant, "feature.booking", null, Now);
        c.Required.Should().Be(5m);
        c.Deny.Should().BeFalse();
    }

    [Fact] // T10 — Deny card + insufficient → deny true + required reported
    public async Task Check_DenyCard_Insufficient_Denies()
    {
        var (db, wallet, _, _, _) = NewSut();
        SeedRateCard(db, "feature.booking", 5m, MeteringEnforcement.Deny);
        await wallet.GrantAsync(Tenant, 3m, "initial", Admin, Now);

        var c = await wallet.CheckAsync(Tenant, "feature.booking", null, Now);
        c.Deny.Should().BeTrue();
        c.Required.Should().Be(5m);
        c.Balance.Should().Be(3m);
    }

    [Fact] // T11 — estimate parameter scales required
    public async Task Check_Estimate_ScalesRequired()
    {
        var (db, wallet, _, _, _) = NewSut();
        SeedRateCard(db, "chat.bytes", 0.5m, MeteringEnforcement.Deny);
        await wallet.GrantAsync(Tenant, 100m, "initial", Admin, Now);

        var c = await wallet.CheckAsync(Tenant, "chat.bytes", estimate: 300, Now);
        c.Required.Should().Be(150m);
        c.Deny.Should().BeTrue();
    }

    // ---------- D4: alerts ----------

    [Fact] // T13 — Warn50 fires at ≤50% remaining
    public async Task Alerts_Warn50_FiresAtHalf()
    {
        var (db, wallet, usage, _, _) = NewSut();
        SeedRateCard(db, "x", 1m);
        await wallet.GrantAsync(Tenant, 100m, "initial", Admin, Now);

        await usage.IngestAsync(Caller, "e1", Req(Tenant, "x", qty: 50), Now);

        db.WalletAlerts.Should().ContainSingle(a =>
            a.Level == WalletAlertLevel.Warn50 && a.TenantId == Tenant);
        (await wallet.CheckAsync(Tenant, "x", null, Now)).AlertLevel.Should().Be("warn50");
    }

    [Fact] // T14 — Warn90 fires at ≤10% remaining
    public async Task Alerts_Warn90_FiresAtTenPercent()
    {
        var (db, wallet, usage, _, _) = NewSut();
        SeedRateCard(db, "x", 1m);
        await wallet.GrantAsync(Tenant, 100m, "initial", Admin, Now);

        await usage.IngestAsync(Caller, "e1", Req(Tenant, "x", qty: 91), Now);

        db.WalletAlerts.Should().Contain(a => a.Level == WalletAlertLevel.Warn90);
        (await wallet.CheckAsync(Tenant, "x", null, Now)).AlertLevel.Should().Be("warn90");
    }

    [Fact] // T15 — one alert row per threshold per cycle even across many deductions
    public async Task Alerts_OncePerLevelPerCycle()
    {
        var (db, wallet, usage, _, _) = NewSut();
        SeedRateCard(db, "x", 1m);
        await wallet.GrantAsync(Tenant, 100m, "initial", Admin, Now);

        for (var i = 0; i < 6; i++)
            await usage.IngestAsync(Caller, $"e{i}", Req(Tenant, "x", qty: 15), Now);

        db.WalletAlerts.Count(a => a.Level == WalletAlertLevel.Warn50).Should().Be(1);
        db.WalletAlerts.Count(a => a.Level == WalletAlertLevel.Warn90).Should().Be(1);
    }

    [Fact] // T16 — top-up resets cycle → same threshold may fire again
    public async Task Alerts_CycleReset_AllowsRefire()
    {
        var (db, wallet, usage, _, topups) = NewSut();
        SeedRateCard(db, "x", 1m);
        await wallet.GrantAsync(Tenant, 100m, "initial", Admin, Now);
        await usage.IngestAsync(Caller, "e1", Req(Tenant, "x", qty: 50), Now);
        db.WalletAlerts.Count(a => a.Level == WalletAlertLevel.Warn50).Should().Be(1);

        var created = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant, 500_000m), Now);
        await topups.ConfirmAsync(created.Request!.Id,
            new TopupConfirmRequest(100m), Admin, Now);
        await usage.IngestAsync(Caller, "e2", Req(Tenant, "x", qty: 75), Now); // 125/200? → ≤50% of granted 200

        db.WalletAlerts.Count(a => a.Level == WalletAlertLevel.Warn50).Should().Be(2);
        db.WalletAlerts.Select(a => a.CycleKey).Distinct().Count().Should().Be(2);
    }

    [Fact] // T17 — GrantedTotal=0 + balance 0 → depleted, no div-by-zero
    public async Task Alerts_ZeroGranted_Depleted_NoDivByZero()
    {
        var (_, wallet, _, _, _) = NewSut();
        var c = await wallet.CheckAsync(Tenant, "any", null, Now);
        c.AlertLevel.Should().Be("depleted"); // balance 0 & granted 0 → depleted per AlertLevelOf
    }

    [Fact] // T17b — small grant into depleted wallet → Warn90 by remaining %
    public async Task Alerts_SmallGrant_StillWarn90()
    {
        var (db, wallet, _, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 1m, "tiny", Admin, Now); // granted=1, balance=1 → 100% → no alert level

        var c = await wallet.CheckAsync(Tenant, "any", null, Now);
        c.AlertLevel.Should().BeNull(); // 1/1 = 100% remaining → healthy

        // grant 100 then burn to 8 → 8% → warn90
        var (db2, wallet2, usage2, _, _) = NewSut();
        SeedRateCard(db2, "x", 1m);
        await wallet2.GrantAsync(Tenant, 100m, "initial", Admin, Now);
        await usage2.IngestAsync(Caller, "e1", Req(Tenant, "x", qty: 92), Now);
        (await wallet2.CheckAsync(Tenant, "x", null, Now)).AlertLevel.Should().Be("warn90");
    }

    // ---------- D6–D9: top-ups ----------

    [Fact] // T18 — create → unique reference + correct VietQR payload
    public async Task Topup_Create_UniqueRef_AndQrPayload()
    {
        var (_, _, _, _, topups) = NewSut();
        var (_, _, _, _, topups2) = NewSut();
        await topups.PutConfigAsync(
            new TopupConfigPutRequest("970436", "Vietcombank", "0123456789", "CONG TY VAN AN"),
            Admin, Now);

        var a = await topups.CreateAsync(Caller, "k1", new TopupCreateRequest(Tenant, 500_000m), Now);
        var b = await topups2.CreateAsync(Caller, "k2", new TopupCreateRequest(Tenant, 300_000m), Now);

        a.Request!.ReferenceCode.Should().StartWith("TOPUP-").And.HaveLength(14);
        b.Request!.ReferenceCode.Should().NotBe(a.Request.ReferenceCode);

        var list = await topups.ListAsync(null, Tenant, Now);
        var dto = list.Items.Single();
        dto.QrPayload.Should().StartWith("https://img.vietqr.io/image/970436-0123456789-compact.jpg?");
        dto.QrPayload.Should().Contain("amount=500000");
        dto.QrPayload.Should().Contain($"addInfo={Uri.EscapeDataString(a.Request.ReferenceCode)}");
        dto.QrPayload.Should().Contain("accountName=CONG%20TY%20VAN%20AN");
    }

    [Fact] // T19 — confirm → grant + credits + cycle reset + ledger
    public async Task Topup_Confirm_Grants_AndResetsCycle()
    {
        var (db, wallet, _, _, topups) = NewSut();
        var oldCycle = (await wallet.GrantAsync(Tenant, 10m, "initial", Admin, Now)).CycleKey;

        var created = await topups.CreateAsync(Caller, "k1", new TopupCreateRequest(Tenant, 500_000m), Now);
        var confirmed = await topups.ConfirmAsync(created.Request!.Id,
            new TopupConfirmRequest(200m, "da nhan tien"), Admin, Now);

        confirmed.Request!.Status.Should().Be(TopupStatus.Confirmed);
        confirmed.Request.CreditsGranted.Should().Be(200m);
        var w = await wallet.GetWalletAsync(Tenant);
        w.Balance.Should().Be(210m);
        w.GrantedTotal.Should().Be(210m);
        var entity = await db.CreditWallets.SingleAsync(x => x.TenantId == Tenant);
        entity.CycleKey.Should().NotBe(oldCycle);
        db.CreditLedgerEntries.Should().Contain(e =>
            e.Type == LedgerEntryType.Grant && e.TopupRequestId == created.Request.Id);
    }

    [Fact] // T20 — confirm replay is idempotent (no double grant)
    public async Task Topup_Confirm_Replay_Idempotent()
    {
        var (db, wallet, _, _, topups) = NewSut();
        var created = await topups.CreateAsync(Caller, "k1", new TopupCreateRequest(Tenant), Now);
        var id = created.Request!.Id;

        var first = await topups.ConfirmAsync(id, new TopupConfirmRequest(100m), Admin, Now);
        var replay = await topups.ConfirmAsync(id, new TopupConfirmRequest(999m), Admin, Now);

        first.Request!.Status.Should().Be(TopupStatus.Confirmed);
        replay.Request!.Status.Should().Be(TopupStatus.Confirmed);
        replay.Request.CreditsGranted.Should().Be(100m); // first wins, replay unchanged
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(100m);
        db.CreditLedgerEntries.Count(e => e.TopupRequestId == id).Should().Be(1);
    }

    [Fact] // T21 — confirm rejected/expired → conflict error
    public async Task Topup_Confirm_NonPending_Conflict()
    {
        var (_, _, _, _, topups) = NewSut();
        var rejected = await topups.CreateAsync(Caller, "k1", new TopupCreateRequest(Tenant), Now);
        await topups.RejectAsync(rejected.Request!.Id, new TopupRejectRequest("sai noi dung"), Admin, Now);

        var c = await topups.ConfirmAsync(rejected.Request.Id, new TopupConfirmRequest(100m), Admin, Now);
        c.Request.Should().BeNull();
        c.ErrorCode.Should().Be("topup_not_pending");

        // Expired-confirm conflict is covered by T23 (lazy expiry → non-pending).
    }

    [Fact] // T22 — reject records reason + actor
    public async Task Topup_Reject_RecordsReason()
    {
        var (_, _, _, _, topups) = NewSut();
        var created = await topups.CreateAsync(Caller, "k1", new TopupCreateRequest(Tenant), Now);

        var r = await topups.RejectAsync(created.Request!.Id,
            new TopupRejectRequest("sai so tien"), Admin, Now);

        r.Request!.Status.Should().Be(TopupStatus.Rejected);
        r.Request.Note.Should().Be("sai so tien");
        r.Request.ConfirmedBy.Should().Be(Admin);

        var again = await topups.RejectAsync(created.Request.Id, new TopupRejectRequest(), Admin, Now);
        again.ErrorCode.Should().Be("topup_not_pending");
    }

    [Fact] // T23 — lazy expiry: pending past TTL → Expired on read
    public async Task Topup_LazyExpiry_AfterTtl()
    {
        var (db, wallet, _, _, _) = NewSut();
        var topups = new TopupService(db, wallet, TimeSpan.FromHours(1));
        var created = await topups.CreateAsync(Caller, "k1",
            new TopupCreateRequest(Tenant), Now);

        var list = await topups.ListAsync(null, Tenant, Now.AddHours(2));

        list.Items.Single().Status.Should().Be("expired");
        db.TopupRequests.Single().Status.Should().Be(TopupStatus.Expired);
        var c = await topups.ConfirmAsync(created.Request!.Id,
            new TopupConfirmRequest(50m), Admin, Now.AddHours(2));
        c.ErrorCode.Should().Be("topup_not_pending");
    }

    [Fact] // T24 — confirm credits ≤ 0 → invalid_credits
    public async Task Topup_Confirm_ZeroCredits_Rejected()
    {
        var (_, _, _, _, topups) = NewSut();
        var created = await topups.CreateAsync(Caller, "k1", new TopupCreateRequest(Tenant), Now);

        var r = await topups.ConfirmAsync(created.Request!.Id,
            new TopupConfirmRequest(0m), Admin, Now);

        r.ErrorCode.Should().Be("invalid_credits");
    }

    [Fact] // T25 — concurrent confirm grants exactly once (SQLite real unique index)
    public async Task Topup_ConcurrentConfirm_SingleGrant()
    {
        await using var conn = new SqliteConnection("Data Source=p7t25;Mode=Memory;Cache=Shared");
        await conn.OpenAsync();
        var opts = new DbContextOptionsBuilder<MeteringDbContext>().UseSqlite(conn).Options;

        Guid requestId;
        await using (var seed = new MeteringDbContext(opts))
        {
            await seed.Database.EnsureCreatedAsync();
            var topups = new TopupService(seed,
                new WalletService(seed, new RateCardService(seed)), TimeSpan.FromHours(72));
            var created = await topups.CreateAsync(Caller, "k1", new TopupCreateRequest(Tenant), Now);
            requestId = created.Request!.Id;
        }

        async Task<TopupOpResult> TryConfirm()
        {
            await using var db = new MeteringDbContext(opts);
            var topups = new TopupService(db,
                new WalletService(db, new RateCardService(db)), TimeSpan.FromHours(72));
            return await topups.ConfirmAsync(requestId, new TopupConfirmRequest(100m), Admin, Now);
        }

        var results = await Task.WhenAll(TryConfirm(), TryConfirm());
        results.Should().OnlyContain(r => r.Request != null);

        await using var verify = new MeteringDbContext(opts);
        verify.CreditLedgerEntries.Count(e => e.TopupRequestId == requestId).Should().Be(1);
        (await verify.CreditWallets.SingleAsync(x => x.TenantId == Tenant)).Balance.Should().Be(100m);
    }

    [Fact] // T26 — topup-config GET/PUT + audit row
    public async Task TopupConfig_Put_Get_AndAudit()
    {
        var (db, _, _, _, topups) = NewSut();
        (await topups.GetConfigAsync()).Should().BeNull();

        var cfg = await topups.PutConfigAsync(
            new TopupConfigPutRequest("970436", "Vietcombank", "0123", "VAN AN"), Admin, Now);

        cfg.BankCode.Should().Be("970436");
        (await topups.GetConfigAsync())!.AccountNumber.Should().Be("0123");
        db.AuditLogs.Should().Contain(a =>
            a.Action == "upsert" && a.EntityType == nameof(TopupConfig) && a.Actor == Admin);

        // Second PUT updates the SAME singleton row.
        await topups.PutConfigAsync(
            new TopupConfigPutRequest("970415", "VietinBank", "9999", "VAN AN"), Admin, Now);
        db.TopupConfigs.Count().Should().Be(1);
        db.TopupConfigs.Single().BankCode.Should().Be("970415");
    }

    // T27 (non-admin mutation → 403) — HTTP-layer guard, verified in smoke RV.

    [Fact] // T28 — ledger is append-only: entity has no public mutators on persisted fields
    public void Ledger_AppendOnly_NoPublicSetters()
    {
        foreach (var p in typeof(CreditLedgerEntry).GetProperties())
            (p.SetMethod == null || !p.SetMethod.IsPublic)
                .Should().BeTrue(because: $"{p.Name} must not be publicly mutable (append-only)");
        typeof(CreditLedgerEntry).GetMethods()
            .Count(m => m.IsPublic && !m.IsStatic && !m.IsSpecialName
                        && m.DeclaringType == typeof(CreditLedgerEntry))
            .Should().Be(0);
    }

    [Fact] // T29 — adjustment entry reverses a wrong grant without mutating history
    public async Task Adjust_Reversal_CorrectsBalance()
    {
        var (db, wallet, _, _, _) = NewSut();
        await wallet.GrantAsync(Tenant, 100m, "wrong amount", Admin, Now);

        await wallet.AdjustAsync(Tenant, -60m, "reversal: actual transfer was 40", Admin, Now);

        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(40m);
        var entries = db.CreditLedgerEntries.OrderBy(e => e.CreatedAtUtc).ToList();
        entries.Should().HaveCount(2);
        entries[1].Type.Should().Be(LedgerEntryType.Adjust);
        entries[1].Credits.Should().Be(-60m);
        entries[1].BalanceAfter.Should().Be(40m);
        // Original grant row untouched — history preserved.
        entries[0].Credits.Should().Be(100m);
    }

    [Fact] // T30 — ledger paging + wallet balance equals sum of ledger entries
    public async Task Ledger_Paging_BalanceEqualsSum()
    {
        var (db, wallet, usage, _, _) = NewSut();
        SeedRateCard(db, "x", 1m);
        await wallet.GrantAsync(Tenant, 100m, "g1", Admin, Now);
        await wallet.GrantAsync(Tenant, 50m, "g2", Admin, Now);
        for (var i = 0; i < 3; i++)
            await usage.IngestAsync(Caller, $"e{i}", Req(Tenant, "x", qty: 10), Now);

        var page1 = await wallet.LedgerAsync(Tenant, page: 1, pageSize: 3);
        var page2 = await wallet.LedgerAsync(Tenant, page: 2, pageSize: 3);

        page1.Items.Should().HaveCount(3);
        page2.Items.Should().HaveCount(2);
        page1.Items.Concat(page2.Items).Select(e => e.Id).Should().OnlyHaveUniqueItems();
        // Newest first.
        page1.Items.Should().BeInDescendingOrder(e => e.CreatedAtUtc);

        var sum = db.CreditLedgerEntries.Where(e => e.TenantId == Tenant).Sum(e => e.Credits);
        (await wallet.GetWalletAsync(Tenant)).Balance.Should().Be(sum).And.Be(120m);
    }
}
