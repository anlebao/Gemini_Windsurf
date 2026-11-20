using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;
using VanAn.Metering.Infra;
using Xunit;

namespace VanAn.Metering.Tests;

/// <summary>
/// Task P13 — public top-up guardrail: anonymous storefront callers can create
/// top-ups via the gateway, so metering caps pending requests per tenant and
/// creation rate per hour to bound QR-spam abuse.
/// </summary>
public sealed class TopupRateLimitTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private const string Caller = "gateway";
    private static readonly Guid Tenant = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static (MeteringDbContext db, WalletService wallet, TopupService topups) NewSut()
    {
        var db = new MeteringDbContext(new DbContextOptionsBuilder<MeteringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var wallet = new WalletService(db, new RateCardService(db));
        return (db, wallet, new TopupService(db, wallet, TimeSpan.FromHours(72)));
    }

    private static Task<TopupOpResult> Create(TopupService topups, string key, Guid? tenant = null, DateTime? now = null)
        => topups.CreateAsync(Caller, key,
            new TopupCreateRequest(tenant ?? Tenant, 10000m), now ?? Now);

    [Fact]
    public async Task Create_PendingCapReached_Fails()
    {
        var (_, _, topups) = NewSut();
        for (var i = 0; i < 5; i++)
            (await Create(topups, $"k{i}")).Request.Should().NotBeNull();

        var r = await Create(topups, "k-overflow");

        r.Request.Should().BeNull();
        r.ErrorCode.Should().Be("too_many_pending_topups");
    }

    [Fact]
    public async Task Create_PendingCap_PerTenant_Isolated()
    {
        var (_, _, topups) = NewSut();
        var other = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
            (await Create(topups, $"k{i}")).Request.Should().NotBeNull();

        var r = await Create(topups, "k-other", tenant: other);

        r.Request.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_ConfirmedTopups_DoNotCountTowardPendingCap()
    {
        var (db, _, topups) = NewSut();
        for (var i = 0; i < 5; i++)
        {
            var r = await Create(topups, $"k{i}");
            r.Request!.Confirm(10m, "admin", null, Now);
            await db.SaveChangesAsync();
        }

        var r2 = await Create(topups, "k-new");

        r2.Request.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_ExpiredPending_DoesNotCountTowardCap()
    {
        var (_, _, topups) = NewSut();
        // 5 pending created 73h ago — past the 72h TTL, never lazy-expired (no read).
        for (var i = 0; i < 5; i++)
            (await Create(topups, $"k{i}", now: Now.AddHours(-73))).Request.Should().NotBeNull();

        var r = await Create(topups, "k-fresh", now: Now);

        r.Request.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_RateWindowExceeded_Fails_ThenRecoversAfterOneHour()
    {
        var (db, _, topups) = NewSut();
        // 20 creations inside the hour, each rejected to free pending slots.
        for (var i = 0; i < 20; i++)
        {
            var r = await Create(topups, $"k{i}");
            r.Request.Should().NotBeNull();
            r.Request!.Reject("admin", "test", Now);
            await db.SaveChangesAsync();
        }

        var blocked = await Create(topups, "k-21");
        blocked.Request.Should().BeNull();
        blocked.ErrorCode.Should().Be("topup_rate_limited");

        var afterHour = await Create(topups, "k-22", now: Now.AddHours(1).AddMinutes(1));
        afterHour.Request.Should().NotBeNull();
    }

    // ---------- P13b: physical purge of stale top-ups ----------

    [Fact]
    public async Task PurgeStale_DeletesOnlyOldNonConfirmed()
    {
        var (db, _, topups) = NewSut();
        var old = Now.AddDays(-100);
        // expired + rejected + stale-pending, all >90d old → deleted
        foreach (var key in new[] { "e1", "r1", "p1" })
        {
            var r = await Create(topups, key, now: old);
            r.Request.Should().NotBeNull();
        }
        db.TopupRequests.Local.First(t => t.IdempotencyKey == "e1").ExpireIfStale(Now);
        db.TopupRequests.Local.First(t => t.IdempotencyKey == "r1").Reject("admin", null, old);
        // p1 stays Pending but ExpiresAtUtc = old+72h << Now → stale
        await db.SaveChangesAsync();

        // confirmed old → KEPT (payment fact); fresh pending → KEPT; fresh expired → KEPT (<90d)
        var conf = await Create(topups, "c1", now: old);
        conf.Request!.Confirm(10m, "admin", null, old);
        await db.SaveChangesAsync();
        (await Create(topups, "fresh", now: Now)).Request.Should().NotBeNull();
        var freshExpired = await Create(topups, "fe1", now: Now.AddDays(-10));
        freshExpired.Request!.ExpireIfStale(Now);
        await db.SaveChangesAsync();

        var deleted = await topups.PurgeStaleAsync(Now);

        deleted.Should().Be(3);
        var remaining = await db.TopupRequests.Select(t => t.IdempotencyKey).ToListAsync();
        remaining.Should().BeEquivalentTo(new[] { "c1", "fresh", "fe1" });
    }
}
