using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;
using VanAn.Metering.Infra;
using Xunit;

namespace VanAn.Metering.Tests;

/// <summary>Task P2 test matrix T01–T20 — EF InMemory (unique index not enforced;
/// dup-check logic lives in the service so it is still covered).</summary>
public sealed class RateCardServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
    private const string Actor = "sysadmin";

    private static (MeteringDbContext db, RateCardService svc) NewSut()
    {
        var db = new MeteringDbContext(new DbContextOptionsBuilder<MeteringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        return (db, new RateCardService(db));
    }

    private static RateCardCreateRequest Req(
        string code = "store.visit", string unit = "visit", decimal rate = 1,
        string? enforcement = null, DateTime? from = null)
        => new(code, unit, rate, enforcement, from);

    private static async Task<RateCardVersion> SeedAsync(
        MeteringDbContext db, string code, decimal rate, DateTime from,
        DateTime? to = null, bool active = true)
    {
        var v = RateCardVersion.Create(code, "u", rate, MeteringEnforcement.MeterOnly, from, Actor);
        if (to.HasValue) v.SupersedeAt(to.Value);
        if (!active) v.Close(Now);
        db.RateCardVersions.Add(v);
        await db.SaveChangesAsync();
        return v;
    }

    // ---- Resolve (T01–T06) ----

    [Fact] // T01
    public async Task Resolve_NoVersions_ReturnsNull()
    {
        var (_, svc) = NewSut();
        (await svc.ResolveAsync("store.visit", Now)).Should().BeNull();
    }

    [Fact] // T02
    public async Task Resolve_BeforeFirstEffectiveFrom_ReturnsNull()
    {
        var (db, svc) = NewSut();
        await SeedAsync(db, "store.visit", 1, Now.AddDays(1));
        (await svc.ResolveAsync("store.visit", Now)).Should().BeNull();
    }

    [Fact] // T03
    public async Task Resolve_Boundary_AtFromIncluded_AtToExcluded()
    {
        var (db, svc) = NewSut();
        await SeedAsync(db, "store.visit", 1, Now, Now.AddDays(10));
        (await svc.ResolveAsync("store.visit", Now)).Should().NotBeNull();
        (await svc.ResolveAsync("store.visit", Now.AddDays(10))).Should().BeNull();
    }

    [Fact] // T04
    public async Task Resolve_TwoVersions_CorrectRatePerInterval()
    {
        var (db, svc) = NewSut();
        await SeedAsync(db, "store.visit", 1, Now, Now.AddDays(5));
        await SeedAsync(db, "store.visit", 3, Now.AddDays(5));
        (await svc.ResolveAsync("store.visit", Now.AddDays(2)))!.CreditRate.Should().Be(1);
        (await svc.ResolveAsync("store.visit", Now.AddDays(6)))!.CreditRate.Should().Be(3);
    }

    [Fact] // T05
    public async Task Resolve_InactiveVersion_IsSkipped()
    {
        var (db, svc) = NewSut();
        await SeedAsync(db, "store.visit", 1, Now, active: false);
        (await svc.ResolveAsync("store.visit", Now.AddHours(1))).Should().BeNull();
    }

    [Fact] // T06
    public async Task Resolve_OverlappingVersions_LatestEffectiveFromWins()
    {
        var (db, svc) = NewSut();
        await SeedAsync(db, "store.visit", 1, Now, Now.AddDays(10));
        await SeedAsync(db, "store.visit", 9, Now.AddDays(3), Now.AddDays(10));
        (await svc.ResolveAsync("store.visit", Now.AddDays(5)))!.CreditRate.Should().Be(9);
    }

    // ---- Create (T07–T10) ----

    [Fact] // T07
    public async Task Create_Valid_ReturnsVersion_AndWritesAudit()
    {
        var (db, svc) = NewSut();
        var r = await svc.CreateAsync(Req(), Actor, Now);
        r.Ok.Should().BeTrue();
        r.Version!.IsActive.Should().BeTrue();
        r.Version.ChangedBy.Should().Be(Actor);
        var audit = await db.AuditLogs.SingleAsync();
        audit.Action.Should().Be("create");
        audit.Actor.Should().Be(Actor);
        audit.EntityId.Should().Be(r.Version.Id);
        audit.NewJson.Should().Contain("store.visit");
    }

    [Fact] // T08
    public async Task Create_DuplicateActiveCode_Conflicts()
    {
        var (db, svc) = NewSut();
        await SeedAsync(db, "store.visit", 1, Now);
        var r = await svc.CreateAsync(Req(), Actor, Now);
        r.Ok.Should().BeFalse();
        r.ErrorCode.Should().Be("serviceCode_active_exists");
    }

    [Fact] // T09
    public async Task Create_AfterDeactivate_Allowed()
    {
        var (db, svc) = NewSut();
        await SeedAsync(db, "store.visit", 1, Now, active: false);
        var r = await svc.CreateAsync(Req(), Actor, Now.AddDays(1));
        r.Ok.Should().BeTrue();
    }

    [Theory] // T10
    [InlineData("", "visit", 1, null, "invalid_serviceCode")]
    [InlineData("  ", "visit", 1, null, "invalid_serviceCode")]
    [InlineData("store.visit", "", 1, null, "invalid_meterUnit")]
    [InlineData("store.visit", "visit", -1, null, "invalid_creditRate")]
    [InlineData("store.visit", "visit", 1, "Block", "invalid_enforcement")]
    public async Task Create_Invalid_ReturnsErrorCode(
        string code, string unit, decimal rate, string? enforcement, string expected)
    {
        var (_, svc) = NewSut();
        var r = await svc.CreateAsync(Req(code, unit, rate, enforcement), Actor, Now);
        r.ErrorCode.Should().Be(expected);
    }

    [Fact] // T10b — effectiveFrom >= effectiveTo
    public async Task Create_InvertedRange_ReturnsError()
    {
        var (_, svc) = NewSut();
        var r = await svc.CreateAsync(
            new RateCardCreateRequest("x", "u", 1, null, Now.AddDays(2), Now.AddDays(1)), Actor, Now);
        r.ErrorCode.Should().Be("invalid_effective_range");
    }

    // ---- Patch (T11–T16) ----

    [Fact] // T11
    public async Task Patch_Head_ClosesOld_CreatesNewVersion()
    {
        var (db, svc) = NewSut();
        var head = await SeedAsync(db, "store.visit", 1, Now);
        var at = Now.AddDays(1);
        var r = await svc.PatchAsync(head.Id, new RateCardPatchRequest(CreditRate: 5, EffectiveFrom: at), Actor, at);
        r.Ok.Should().BeTrue();
        var old = await db.RateCardVersions.FindAsync(head.Id);
        old!.EffectiveTo.Should().Be(at);
        old.IsActive.Should().BeTrue(); // superseded, still resolvable for its window
        (await svc.ResolveAsync("store.visit", Now.AddHours(12)))!.CreditRate.Should().Be(1);
        (await svc.ResolveAsync("store.visit", at.AddDays(1)))!.CreditRate.Should().Be(5);
    }

    [Fact] // T12
    public async Task Patch_NullFields_KeepOldValues()
    {
        var (db, svc) = NewSut();
        var head = RateCardVersion.Create("chat.message", "msg", 2, MeteringEnforcement.Deny, Now.AddDays(-1), Actor, "chat");
        db.RateCardVersions.Add(head);
        await db.SaveChangesAsync();
        var r = await svc.PatchAsync(head.Id, new RateCardPatchRequest(CreditRate: 7), Actor, Now);
        r.Ok.Should().BeTrue();
        r.Version!.MeterUnit.Should().Be("msg");
        r.Version.Enforcement.Should().Be(MeteringEnforcement.Deny); // not reset to MeterOnly
        r.Version.Label.Should().Be("chat");
    }

    [Fact] // T13
    public async Task Patch_SupersededOrInactive_ReturnsConflict()
    {
        var (db, svc) = NewSut();
        var superseded = await SeedAsync(db, "a", 1, Now, Now.AddDays(1));
        var inactive = await SeedAsync(db, "b", 1, Now, active: false);
        (await svc.PatchAsync(superseded.Id, new RateCardPatchRequest(), Actor, Now)).ErrorCode
            .Should().Be("version_not_current");
        (await svc.PatchAsync(inactive.Id, new RateCardPatchRequest(), Actor, Now)).ErrorCode
            .Should().Be("version_not_current");
    }

    [Fact] // T14
    public async Task Patch_NotFound_ReturnsNotFound()
    {
        var (_, svc) = NewSut();
        (await svc.PatchAsync(Guid.NewGuid(), new RateCardPatchRequest(), Actor, Now)).ErrorCode
            .Should().Be("not_found");
    }

    [Fact] // T15
    public async Task Patch_InvalidEffectiveFrom_ReturnsError()
    {
        var (db, svc) = NewSut();
        var head = await SeedAsync(db, "store.visit", 1, Now.AddDays(-2));
        // effectiveFrom <= current.EffectiveFrom
        (await svc.PatchAsync(head.Id, new RateCardPatchRequest(EffectiveFrom: Now.AddDays(-2)), Actor, Now))
            .ErrorCode.Should().Be("invalid_effectiveFrom");
        // effectiveFrom > head.From nhưng >5 phút trong quá khứ
        (await svc.PatchAsync(head.Id, new RateCardPatchRequest(EffectiveFrom: Now.AddDays(-1)), Actor, Now))
            .ErrorCode.Should().Be("effectiveFrom_in_past");
    }

    [Fact] // T16
    public async Task Patch_ScheduledFuture_HeadResolvesUntilEffective()
    {
        var (db, svc) = NewSut();
        var head = await SeedAsync(db, "store.visit", 1, Now);
        var future = Now.AddDays(7);
        var r = await svc.PatchAsync(head.Id, new RateCardPatchRequest(CreditRate: 9, EffectiveFrom: future), Actor, Now);
        r.Ok.Should().BeTrue();
        // head keeps resolving until the scheduled instant
        (await svc.ResolveAsync("store.visit", Now.AddDays(3)))!.CreditRate.Should().Be(1);
        (await svc.ResolveAsync("store.visit", future))!.CreditRate.Should().Be(9);
        // scheduled version is the new chain head (patchable per D3)
        var heads = await svc.ListAsync(includeInactive: false);
        heads.Should().ContainSingle().Which.Id.Should().Be(r.Version!.Id);
    }

    // ---- Deactivate (T17–T18) ----

    [Fact] // T17
    public async Task Deactivate_ClosesVersion_AndWritesAudit()
    {
        var (db, svc) = NewSut();
        var v = await SeedAsync(db, "store.visit", 1, Now);
        var r = await svc.DeactivateAsync(v.Id, Actor, Now.AddHours(1));
        r.Ok.Should().BeTrue();
        var saved = await db.RateCardVersions.FindAsync(v.Id);
        saved!.IsActive.Should().BeFalse();
        saved.EffectiveTo.Should().Be(Now.AddHours(1));
        (await svc.ResolveAsync("store.visit", Now)).Should().BeNull();
        (await db.AuditLogs.SingleAsync()).Action.Should().Be("deactivate");
    }

    [Fact] // T18
    public async Task Deactivate_AlreadyInactive_IsIdempotent()
    {
        var (db, svc) = NewSut();
        var v = await SeedAsync(db, "store.visit", 1, Now, active: false);
        var r = await svc.DeactivateAsync(v.Id, Actor, Now);
        r.Ok.Should().BeTrue();
        r.IdempotentNoOp.Should().BeTrue();
        (await db.AuditLogs.CountAsync()).Should().Be(0); // no mutation → no audit
    }

    // ---- List (T19) + audit coverage (T20) ----

    [Fact] // T19
    public async Task List_DefaultReturnsHeads_IncludeInactiveReturnsHistory()
    {
        var (db, svc) = NewSut();
        await SeedAsync(db, "a", 1, Now, Now.AddDays(1));        // superseded
        await SeedAsync(db, "a", 2, Now.AddDays(1));             // head
        await SeedAsync(db, "b", 3, Now, active: false);         // deactivated
        (await svc.ListAsync(false)).Should().ContainSingle(v => v.ServiceCode == "a" && v.CreditRate == 2);
        (await svc.ListAsync(true)).Should().HaveCount(3);
    }

    [Fact] // T20
    public async Task Audit_EveryMutation_WritesCorrectLog()
    {
        var (db, svc) = NewSut();
        var created = await svc.CreateAsync(Req("a"), Actor, Now);
        var patched = await svc.PatchAsync(created.Version!.Id,
            new RateCardPatchRequest(CreditRate: 9, EffectiveFrom: Now.AddDays(1)), Actor, Now.AddDays(1));
        await svc.DeactivateAsync(patched.Version!.Id, "other-admin", Now.AddDays(2));

        var logs = await db.AuditLogs.OrderBy(l => l.AtUtc).ToListAsync();
        logs.Should().HaveCount(3);
        logs.Select(l => l.Action).Should().Equal("create", "patch", "deactivate");
        logs[1].OldJson.Should().Contain("\"CreditRate\":1");
        logs[1].NewJson.Should().Contain("\"CreditRate\":9");
        logs[1].Actor.Should().Be(Actor);
        logs[2].Actor.Should().Be("other-admin");
    }

    // ---- Issue #197 — scope-aware resolve + create (M1–M6) ----

    private static readonly Guid OverrideTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact] // M1 — precedence: Tenant > TenantType > TenantStatus > Global; no attributes → Global
    public async Task ResolveForTenant_Precedence_TenantOverrideWins()
    {
        var (db, svc) = NewSut();
        SeedScoped(db, "feature.x", 1, RateCardScope.Global, null);
        SeedScoped(db, "feature.x", 2, RateCardScope.TenantType, RateCardScopes.Hkd);
        SeedScoped(db, "feature.x", 3, RateCardScope.TenantStatus, RateCardScopes.Unverified);
        SeedScoped(db, "feature.x", 9, RateCardScope.Tenant, OverrideTenant.ToString());

        // Tenant override thắng tất cả
        (await svc.ResolveForTenantAsync("feature.x", OverrideTenant, "HKD", "unverified", Now))!.CreditRate.Should().Be(9);
        // TenantType (HKD) thắng TenantStatus + Global
        (await svc.ResolveForTenantAsync("feature.x", Guid.NewGuid(), "HKD", "unverified", Now))!.CreditRate.Should().Be(2);
        // TenantStatus (unverified) thắng Global khi type không khớp
        (await svc.ResolveForTenantAsync("feature.x", Guid.NewGuid(), "Enterprise_SME", "unverified", Now))!.CreditRate.Should().Be(3);
        // Global fallback khi type + status đều không khớp
        (await svc.ResolveForTenantAsync("feature.x", Guid.NewGuid(), "Enterprise_SME", "verified", Now))!.CreditRate.Should().Be(1);
        // M3 backward compat — không attributes → Global-only
        (await svc.ResolveAsync("feature.x", Now))!.CreditRate.Should().Be(1);
    }

    [Fact] // M1b — case-insensitive attribute matching + tenant override không match tenant khác
    public async Task ResolveForTenant_AttributeCaseInsensitive_TenantScopedToId()
    {
        var (db, svc) = NewSut();
        SeedScoped(db, "feature.x", 2, RateCardScope.TenantType, RateCardScopes.Hkd);
        SeedScoped(db, "feature.x", 9, RateCardScope.Tenant, OverrideTenant.ToString());

        (await svc.ResolveForTenantAsync("feature.x", Guid.NewGuid(), "hkd", "verified", Now))!.CreditRate.Should().Be(2);
        // tenant khác → override Tenant không áp dụng (chỉ đúng id) → type HKD vẫn thắng
        (await svc.ResolveForTenantAsync("feature.x", Guid.NewGuid(), "HKD", "verified", Now))!.CreditRate.Should().Be(2);
        // override chỉ match đúng tenantId (kể cả khi type/status khác)
        (await svc.ResolveForTenantAsync("feature.x", OverrideTenant, "Enterprise_SME", "verified", Now))!.CreditRate.Should().Be(9);
    }

    [Fact] // M2 — nhiều chain cùng ServiceCode khác scope OK; cùng scope → conflict
    public async Task Create_SameCode_DifferentScopes_Allowed()
    {
        var (db, svc) = NewSut();
        var g = await svc.CreateAsync(new RateCardCreateRequest("feature.x", "u", 1, null, Scope: "Global"), Actor, Now);
        g.Ok.Should().BeTrue();
        var t = await svc.CreateAsync(new RateCardCreateRequest("feature.x", "u", 2, null, Scope: "TenantType", ScopeValue: "HKD"), Actor, Now);
        t.Ok.Should().BeTrue();
        var s = await svc.CreateAsync(new RateCardCreateRequest("feature.x", "u", 3, null, Scope: "TenantStatus", ScopeValue: "unverified"), Actor, Now);
        s.Ok.Should().BeTrue();
        var o = await svc.CreateAsync(new RateCardCreateRequest("feature.x", "u", 9, null, Scope: "Tenant", ScopeValue: OverrideTenant.ToString()), Actor, Now);
        o.Ok.Should().BeTrue();

        var dup = await svc.CreateAsync(new RateCardCreateRequest("feature.x", "u", 4, null, Scope: "TenantType", ScopeValue: "HKD"), Actor, Now);
        dup.Ok.Should().BeFalse();
        dup.ErrorCode.Should().Be("serviceCode_active_exists");

        // PATCH head của chain TenantType không ảnh hưởng chain Global
        var patched = await svc.PatchAsync(t.Version!.Id, new RateCardPatchRequest(CreditRate: 20, EffectiveFrom: Now.AddMinutes(1)), Actor, Now.AddMinutes(1));
        patched.Ok.Should().BeTrue();
        (await svc.ResolveForTenantAsync("feature.x", Guid.NewGuid(), "HKD", "verified", Now.AddMinutes(2)))!.CreditRate.Should().Be(20);
        (await svc.ResolveAsync("feature.x", Now.AddMinutes(2)))!.CreditRate.Should().Be(1);
    }

    [Fact] // M3b — create không scope → Global
    public async Task Create_NoScope_DefaultsToGlobal()
    {
        var (db, svc) = NewSut();
        var r = await svc.CreateAsync(Req(), Actor, Now);
        r.Ok.Should().BeTrue();
        r.Version!.Scope.Should().Be(RateCardScope.Global);
        r.Version.ScopeValue.Should().BeNull();
    }

    [Theory] // M5 — validation scope/value
    [InlineData("TenantType", "", "invalid_scopeValue")]
    [InlineData("TenantType", "HKDX", "invalid_scopeValue")]
    [InlineData("TenantType", "  hkd  ", null)]   // trimmed valid
    [InlineData("TenantStatus", "active", "invalid_scopeValue")]
    [InlineData("TenantStatus", "Verified", null)] // case-insensitive
    [InlineData("Tenant", "not-a-guid", "invalid_scopeValue")]
    [InlineData("Tenant", "00000000-0000-0000-0000-000000000000", "invalid_scopeValue")]
    [InlineData("Global", "anything", "invalid_scopeValue")]
    [InlineData("Unknown", null, "invalid_scope")]
    public async Task Create_ScopeValidation(string scope, string? value, string? expectedError)
    {
        var (_, svc) = NewSut();
        var r = await svc.CreateAsync(new RateCardCreateRequest("feature.x", "u", 1, null, Scope: scope, ScopeValue: value), Actor, Now);
        if (expectedError == null)
        {
            r.Ok.Should().BeTrue();
            r.Version!.ScopeValue.Should().NotBeNullOrWhiteSpace();
        }
        else
        {
            r.ErrorCode.Should().Be(expectedError);
        }
    }

    private static void SeedScoped(MeteringDbContext db, string code, decimal rate,
        RateCardScope scope, string? scopeValue)
    {
        db.RateCardVersions.Add(RateCardVersion.Create(
            code, "u", rate, MeteringEnforcement.MeterOnly, Now.AddDays(-1), Actor,
            scope: scope, scopeValue: scopeValue));
        db.SaveChanges();
    }
}
