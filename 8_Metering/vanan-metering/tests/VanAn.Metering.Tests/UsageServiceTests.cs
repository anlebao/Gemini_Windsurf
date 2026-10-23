using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;
using VanAn.Metering.Infra;
using Xunit;

namespace VanAn.Metering.Tests;

/// <summary>Task P3 test matrix T01–T15 — EF InMemory for service logic;
/// SQLite shared-cache in-memory for real unique-index enforcement (T10).</summary>
public sealed class UsageServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
    private const string Caller = "shoperp-a";
    private const string OtherCaller = "directory";
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Tenant2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (MeteringDbContext db, UsageService svc) NewSut()
    {
        var db = new MeteringDbContext(new DbContextOptionsBuilder<MeteringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        return (db, new UsageService(db, new WalletService(db, new RateCardService(db)), new RateCardService(db)));
    }

    private static UsageEventRequest Req(
        Guid? tenant = null, string code = "store.visit", decimal qty = 1,
        string unit = "visit", DateTime? at = null)
        => new(tenant ?? Tenant, code, qty, unit, at ?? Now);

    private static UsageBatchItem Item(string key, string code = "store.visit", decimal qty = 1)
        => new(key, Tenant, code, qty, "visit", Now);

    private static async Task<RateCardVersion> SeedRateAsync(
        MeteringDbContext db, string code, decimal rate, DateTime from, DateTime? to = null)
    {
        var v = RateCardVersion.Create(code, "u", rate, MeteringEnforcement.MeterOnly, from, "sysadmin");
        if (to.HasValue) v.SupersedeAt(to.Value);
        db.RateCardVersions.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    // ---- Single ingest (T01–T06) ----

    [Fact] // T01
    public async Task Ingest_Valid_RowsComplete()
    {
        var (db, svc) = NewSut();
        var r = await svc.IngestAsync(Caller, "k1", Req(qty: 3.5m), Now);

        r.ErrorCode.Should().BeNull();
        r.Replay.Should().BeFalse();
        var ev = await db.UsageEvents.SingleAsync();
        r.EventId.Should().Be(ev.Id);
        ev.CallerId.Should().Be(Caller);
        ev.IdempotencyKey.Should().Be("k1");
        ev.Quantity.Should().Be(3.5m);
        ev.ReceivedAtUtc.Should().Be(Now);
        ev.LateEvent.Should().BeFalse();
    }

    [Fact] // T02
    public async Task Ingest_MissingKey_Rejected()
    {
        var (db, svc) = NewSut();
        var r = await svc.IngestAsync(Caller, null, Req(), Now);
        r.ErrorCode.Should().Be("missing_idempotency_key");
        (await db.UsageEvents.CountAsync()).Should().Be(0);

        var r2 = await svc.IngestAsync(Caller, new string('x', 129), Req(), Now);
        r2.ErrorCode.Should().Be("idempotency_key_too_long");
    }

    [Fact] // T03
    public async Task Ingest_ReplaySameKey_ReturnsOldId_NoDup()
    {
        var (db, svc) = NewSut();
        var first = await svc.IngestAsync(Caller, "dup", Req(), Now);
        var second = await svc.IngestAsync(Caller, "dup", Req(qty: 99), Now);

        second.Replay.Should().BeTrue();
        second.EventId.Should().Be(first.EventId);
        (await db.UsageEvents.CountAsync()).Should().Be(1);
        (await db.UsageEvents.SingleAsync()).Quantity.Should().Be(1); // first payload wins
    }

    [Fact] // T04
    public async Task Ingest_SameKeyDifferentCaller_BothInsert()
    {
        var (db, svc) = NewSut();
        await svc.IngestAsync(Caller, "k", Req(), Now);
        var r2 = await svc.IngestAsync(OtherCaller, "k", Req(), Now);

        r2.Replay.Should().BeFalse();
        (await db.UsageEvents.CountAsync()).Should().Be(2);
    }

    [Theory] // T05
    [InlineData(0)] [InlineData(-1)]
    public async Task Ingest_BadQuantity_Rejected(decimal qty)
    {
        var (db, svc) = NewSut();
        var r = await svc.IngestAsync(Caller, "k", Req(qty: qty), Now);
        r.ErrorCode.Should().Be("invalid_quantity");
        (await db.UsageEvents.CountAsync()).Should().Be(0);
    }

    [Fact] // T05
    public async Task Ingest_InvalidFields_Rejected()
    {
        var (db, svc) = NewSut();
        (await svc.IngestAsync(Caller, "k", Req(tenant: Guid.Empty), Now)).ErrorCode.Should().Be("invalid_tenantId");
        (await svc.IngestAsync(Caller, "k", Req(code: " "), Now)).ErrorCode.Should().Be("invalid_serviceCode");
        (await svc.IngestAsync(Caller, "k", Req(unit: ""), Now)).ErrorCode.Should().Be("invalid_unit");
        (await svc.IngestAsync(Caller, "k", Req(at: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc)), Now)).ErrorCode.Should().Be("invalid_occurredAtUtc");
        (await db.UsageEvents.CountAsync()).Should().Be(0);
    }

    [Theory] // T06 — skew past & future both flag LateEvent (D3)
    [InlineData(-6)] [InlineData(-90 * 24 * 60)] [InlineData(6)] [InlineData(120)]
    public async Task Ingest_SkewBeyond5Min_LateFlag(int offsetMinutes)
    {
        var (db, svc) = NewSut();
        var r = await svc.IngestAsync(Caller, "k-" + offsetMinutes, Req(at: Now.AddMinutes(offsetMinutes)), Now);
        r.ErrorCode.Should().BeNull();
        (await db.UsageEvents.SingleAsync(e => e.IdempotencyKey == "k-" + offsetMinutes))
            .LateEvent.Should().BeTrue();
    }

    [Fact] // T06 boundary — exactly 5 minutes is NOT late
    public async Task Ingest_SkewExactly5Min_NotLate()
    {
        var (db, svc) = NewSut();
        await svc.IngestAsync(Caller, "k", Req(at: Now.AddMinutes(-5)), Now);
        (await db.UsageEvents.SingleAsync()).LateEvent.Should().BeFalse();
    }

    // ---- Batch (T07–T09) ----

    [Fact] // T07
    public async Task Batch_Mixed_PerItemStatus()
    {
        var (db, svc) = NewSut();
        await svc.IngestAsync(Caller, "pre", Req(), Now); // pre-existing for replay

        var items = new[]
        {
            Item("new-1"),                                          // accepted
            Item("pre"),                                            // replayed (db)
            Item("bad", qty: 0),                                    // rejected
            new UsageBatchItem("same", Tenant, "store.visit", 1, "visit", Now),
            new UsageBatchItem("same", Tenant, "store.visit", 2, "visit", Now), // replayed (in-batch)
        };
        var r = await svc.IngestBatchAsync(Caller, items, Now);

        r.Accepted.Should().Be(2);
        r.Replayed.Should().Be(2);
        r.Rejected.Should().Be(1);
        r.Results.Select(x => x.Status).Should()
            .Equal("accepted", "replayed", "rejected", "accepted", "replayed");
        r.Results[4].EventId.Should().Be(r.Results[3].EventId); // in-batch dedupe returns first id
        r.Results[2].Error.Should().Be("invalid_quantity");
        (await db.UsageEvents.CountAsync()).Should().Be(3); // pre + new-1 + same
    }

    [Fact] // T08 — endpoint-level guard: service accepts any list; size cap lives in API.
    public void Batch_SizeCap_IsConstant()
    {
        UsageService.MaxBatchSize.Should().Be(500);
    }

    [Fact] // T09
    public async Task Batch_DuplicateKeyInBatch_FirstAcceptedSecondReplayed()
    {
        var (db, svc) = NewSut();
        var r = await svc.IngestBatchAsync(Caller, new[] { Item("x"), Item("x") }, Now);
        r.Accepted.Should().Be(1);
        r.Replayed.Should().Be(1);
        (await db.UsageEvents.CountAsync()).Should().Be(1);
    }

    [Fact] // T10 — SQLite shared-cache: real unique index blocks dup insert; service replays.
    public async Task Ingest_SameKeyTwice_RealProvider_UniqueIndexAndReplay()
    {
        await using var conn = new SqliteConnection("Data Source=p3t10;Mode=Memory;Cache=Shared");
        await conn.OpenAsync();
        var opts = new DbContextOptionsBuilder<MeteringDbContext>().UseSqlite(conn).Options;

        await using var db1 = new MeteringDbContext(opts);
        await db1.Database.EnsureCreatedAsync();
        var svc1 = new UsageService(db1, new WalletService(db1, new RateCardService(db1)), new RateCardService(db1));
        var first = await svc1.IngestAsync(Caller, "race", Req(), Now);
        first.Replay.Should().BeFalse();

        await using var db2 = new MeteringDbContext(opts);
        var svc2 = new UsageService(db2, new WalletService(db2, new RateCardService(db2)), new RateCardService(db2));
        var second = await svc2.IngestAsync(Caller, "race", Req(qty: 50), Now);
        second.Replay.Should().BeTrue();
        second.EventId.Should().Be(first.EventId);

        // Provider-level proof: bypassing service dedupe hits the real unique index.
        db2.UsageEvents.Add(UsageEvent.Create(
            Caller, "race", Tenant, "store.visit", 1, "visit", Now, Now, lateEvent: false));
        var act = () => db2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ---- Summary / shadow report (T11–T14) ----

    [Fact] // T11
    public async Task Summary_GroupsAndSumsWithRate()
    {
        var (db, svc) = NewSut();
        await SeedRateAsync(db, "store.visit", 2m, Now.AddDays(-10));
        await svc.IngestAsync(Caller, "a", Req(qty: 3, at: Now.AddDays(-2)), Now);
        await svc.IngestAsync(Caller, "b", Req(qty: 5, at: Now.AddDays(-1)), Now);
        await svc.IngestAsync(Caller, "c", Req(qty: 7, at: Now.AddMonths(-1)), Now); // previous month — excluded

        var (r, err, _) = await svc.GetSummaryAsync(Tenant, "2026-10");
        err.Should().BeNull();
        var item = r!.Items.Should().ContainSingle().Subject;
        item.ServiceCode.Should().Be("store.visit");
        item.Quantity.Should().Be(8);
        item.Priced.Should().BeTrue();
        item.Credits.Should().Be(16); // (3+5) × 2
    }

    [Fact] // T12 — rate change mid-period: each event priced at ITS occurredAt ([G7]).
    public async Task Summary_RateChangedMidPeriod_UsesVersionAtEventTime()
    {
        var (db, svc) = NewSut();
        var cutover = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        var v1 = await SeedRateAsync(db, "chat.message", 1m, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), cutover);
        await SeedRateAsync(db, "chat.message", 4m, cutover);

        await svc.IngestAsync(Caller, "early", Req(code: "chat.message", unit: "msg", qty: 10, at: cutover.AddDays(-1)), Now);
        await svc.IngestAsync(Caller, "late", Req(code: "chat.message", unit: "msg", qty: 10, at: cutover.AddDays(1)), Now);

        var (r, _, _) = await svc.GetSummaryAsync(Tenant, "2026-10");
        var item = r!.Items.Should().ContainSingle().Subject;
        item.Credits.Should().Be(10 * 1m + 10 * 4m); // 50 — NOT 80 (current rate) or 20
    }

    [Fact] // T13
    public async Task Summary_UnpricedCode_NullCredits()
    {
        var (db, svc) = NewSut();
        await svc.IngestAsync(Caller, "a", Req(code: "no.such.service"), Now);

        var (r, _, _) = await svc.GetSummaryAsync(Tenant, "2026-10");
        var item = r!.Items.Should().ContainSingle().Subject;
        item.Priced.Should().BeFalse();
        item.Credits.Should().BeNull();
        item.UnpricedEvents.Should().Be(1);
    }

    [Fact] // T14
    public async Task Summary_EmptyTenantAndBadPeriod()
    {
        var (_, svc) = NewSut();
        var (r, _, _) = await svc.GetSummaryAsync(Tenant2, "2026-10");
        r!.Items.Should().BeEmpty();

        var (_, err1, _) = await svc.GetSummaryAsync(Tenant, "2026-13");
        err1.Should().Be("invalid_period");
        var (_, err2, _) = await svc.GetSummaryAsync(Tenant, "Oct-2026");
        err2.Should().Be("invalid_period");
        var (_, err3, _) = await svc.GetSummaryAsync(Guid.Empty, "2026-10");
        err3.Should().Be("invalid_tenantId");
    }

    // ---- Events list (T15) ----

    [Fact] // T15
    public async Task ListEvents_PagingFilterDescOrder()
    {
        var (db, svc) = NewSut();
        for (var i = 0; i < 5; i++)
            await svc.IngestAsync(Caller, $"k{i}", Req(at: Now.AddDays(-i)), Now);
        await svc.IngestAsync(Caller, "other", Req(code: "chat.message", unit: "msg"), Now);
        await svc.IngestAsync(Caller, "t2", Req(tenant: Tenant2), Now); // different tenant

        var page1 = await svc.ListEventsAsync(Tenant, "store.visit", null, null, 1, 2);
        page1.Items.Should().HaveCount(2);
        page1.Items[0].OccurredAtUtc.Should().BeAfter(page1.Items[1].OccurredAtUtc); // DESC

        var page2 = await svc.ListEventsAsync(Tenant, "store.visit", null, null, 2, 2);
        page2.Items.Should().HaveCount(2);
        page2.Items[0].OccurredAtUtc.Should().BeOnOrBefore(page1.Items[1].OccurredAtUtc);

        var chat = await svc.ListEventsAsync(Tenant, "chat.message", null, null, 1, 50);
        chat.Items.Should().HaveCount(1);
        chat.Items[0].ServiceCode.Should().Be("chat.message");

        var ranged = await svc.ListEventsAsync(Tenant, null, Now.AddDays(-2), Now, 1, 50);
        ranged.Items.Should().OnlyContain(e => e.OccurredAtUtc >= Now.AddDays(-2));
    }
}
