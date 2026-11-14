using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Controllers;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using Xunit;

namespace VanAn.Core.Tests.Cooperatives;

/// <summary>
/// Entry Points Retention W2-1 — CooperativesController public surface tests.
/// Source of truth = Membership Infrastructure (HtxProfile + Member.MemberTenantId).
/// </summary>
public class CooperativesControllerTests
{
    private static readonly Guid HtxId = Guid.NewGuid();
    private static readonly Guid MemberTenantId = Guid.NewGuid();
    private static readonly Guid SuspendedMemberTenantId = Guid.NewGuid();

    private static (VanAnDbContext Ctx, SqliteConnection Conn) NewDb()
    {
        var conn = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        conn.Open();
        var efProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInternalServiceProvider(efProvider).UseSqlite(conn)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new VanAnDbContext(options);
        ctx.Database.EnsureCreated();
        return (ctx, conn);
    }

    private static CooperativesController NewController(VanAnDbContext ctx)
        => new(ctx, NullLogger<CooperativesController>.Instance);

    private static async Task SeedHtxAsync(VanAnDbContext ctx, Guid htxId = default)
    {
        htxId = htxId == default ? HtxId : htxId;
        ctx.Tenants.Add(Tenant.CreateCompany(new TenantId(htxId), "Hợp tác xã Vạn An"));
        ctx.HtxProfiles.Add(HtxProfile.Create(new TenantId(htxId), "v1.0", "v1.0"));
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedMemberTenantAsync(VanAnDbContext ctx, Guid tenantId, string name)
    {
        ctx.Tenants.Add(Tenant.CreateCompany(new TenantId(tenantId), name));
        await ctx.SaveChangesAsync();
    }

    private static Member NewTenantMember(Guid htxId, Guid memberTenantId, string number)
        => Member.CreateActive(
            new TenantId(htxId), number, MembershipType.OfficialMember,
            memberTenantId: new TenantId(memberTenantId));

    private static async Task<Guid> SeedCustomerAsync(VanAnDbContext ctx)
    {
        var customer = new Customer(new TenantId(HtxId), "Cá nhân A", "0900000001");
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer.Id; // Member.MemberCustomerId FK → Customers.Id (PK)
    }

    [Fact]
    public async Task List_ReturnsEmpty_WhenNoHtx()
    {
        var (ctx, conn) = NewDb();
        await using var _ = conn;
        var result = await NewController(ctx).List();
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<List<CooperativeDto>>().Which.Should().BeEmpty();
    }

    [Fact]
    public async Task List_ReturnsHtxWithActiveMemberShopCount()
    {
        var (ctx, conn) = NewDb();
        await using var _ = conn;
        await SeedHtxAsync(ctx);
        await SeedMemberTenantAsync(ctx, MemberTenantId, "Cửa hàng A");
        await SeedMemberTenantAsync(ctx, SuspendedMemberTenantId, "Cửa hàng B");
        ctx.Members.Add(NewTenantMember(HtxId, MemberTenantId, "MEM-1"));
        var suspended = NewTenantMember(HtxId, SuspendedMemberTenantId, "MEM-2");
        suspended.Suspend("test");
        ctx.Members.Add(suspended);
        // Customer-member (no tenant) must NOT count as member shop.
        var customerId = await SeedCustomerAsync(ctx);
        ctx.Members.Add(Member.CreateActive(new TenantId(HtxId), "MEM-3", MembershipType.LinkedNonCapitalMember,
            memberCustomerId: customerId));
        await ctx.SaveChangesAsync();

        var result = await NewController(ctx).List();
        var list = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<List<CooperativeDto>>().Subject;

        list.Should().ContainSingle();
        list[0].TenantId.Should().Be(HtxId);
        list[0].Name.Should().Be("Hợp tác xã Vạn An");
        list[0].MemberCount.Should().Be(1); // chỉ Active tenant-member
    }

    [Fact]
    public async Task Members_ReturnsOnlyActiveTenantMembers_WithStoreFields()
    {
        var (ctx, conn) = NewDb();
        await using var _ = conn;
        await SeedHtxAsync(ctx);
        await SeedMemberTenantAsync(ctx, MemberTenantId, "Cửa hàng A");
        ctx.Members.Add(NewTenantMember(HtxId, MemberTenantId, "MEM-1"));
        await SeedMemberTenantAsync(ctx, SuspendedMemberTenantId, "Cửa hàng B");
        var suspended = NewTenantMember(HtxId, SuspendedMemberTenantId, "MEM-2");
        suspended.Suspend("test");
        ctx.Members.Add(suspended);
        var customerId = await SeedCustomerAsync(ctx);
        ctx.Members.Add(Member.CreateActive(new TenantId(HtxId), "MEM-3", MembershipType.LinkedNonCapitalMember,
            memberCustomerId: customerId));
        await ctx.SaveChangesAsync();

        var result = await NewController(ctx).Members(HtxId);
        var list = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<List<CooperativeMemberDto>>().Subject;

        list.Should().ContainSingle();
        list[0].TenantId.Should().Be(MemberTenantId);
        list[0].Name.Should().Be("Cửa hàng A");
        list[0].MemberNumber.Should().Be("MEM-1");
    }

    [Fact]
    public async Task Members_Returns404_WhenTenantIsNotHtx()
    {
        var (ctx, conn) = NewDb();
        await using var _ = conn;
        var result = await NewController(ctx).Members(Guid.NewGuid());
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ByDomain_Returns404_WhenDomainUnknown()
    {
        var (ctx, conn) = NewDb();
        await using var _ = conn;
        var result = await NewController(ctx).ByDomain("khong-ton-tai.example.com");
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ByDomain_OwnerTenantHtx_ReturnsSingleCoop()
    {
        var (ctx, conn) = NewDb();
        await using var _ = conn;
        await SeedHtxAsync(ctx);
        ctx.KhachLinkInstances.Add(new KhachLinkInstance(
            "HTX portal", KhachLinkProfile.Hoptacxa, "htx-vanan.example.com",
            ownerTenantId: HtxId));
        await ctx.SaveChangesAsync();

        var result = await NewController(ctx).ByDomain("htx-vanan.example.com");
        var list = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<List<CooperativeDto>>().Subject;
        list.Should().ContainSingle().Which.TenantId.Should().Be(HtxId);
    }

    [Fact]
    public async Task ByDomain_PlatformInstance_ReturnsAllCoops()
    {
        var (ctx, conn) = NewDb();
        await using var _ = conn;
        await SeedHtxAsync(ctx);
        ctx.KhachLinkInstances.Add(new KhachLinkInstance(
            "Portal", KhachLinkProfile.Hoptacxa, "hoptacxa.example.com",
            ownerTenantId: null));
        await ctx.SaveChangesAsync();

        var result = await NewController(ctx).ByDomain("hoptacxa.example.com");
        var list = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<List<CooperativeDto>>().Subject;
        list.Should().ContainSingle();
    }

    [Fact]
    public void PublicEndpoints_AreAllowAnonymous()
    {
        var methods = typeof(CooperativesController).GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false).Any());
        methods.Should().HaveCount(3); // List + Members + ByDomain
    }
}
