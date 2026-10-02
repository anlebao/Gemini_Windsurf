using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Membership;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Services.Membership;

/// <summary>
/// Membership v2 (2026-10-02, user directive): tests 2 luồng admin —
/// M1 CreateMembershipProfile · M2 Member capital · M3 AddMemberAsync (guard D6/D7)
/// · M4 CollaboratorTenantProvisioning · M5 HtxProfileService.ListAsync.
/// </summary>
public class MembershipHtxAdminTests
{
    // ── M1: Tenant.CreateMembershipProfile ─────────────────────────────────

    [Fact]
    public void CreateMembershipProfile_UnclassifiedActiveHousehold()
    {
        var id = new TenantId(Guid.NewGuid());
        var tenant = TenantAggregate.CreateMembershipProfile(id, "HKD Cộng tác viên A");

        Assert.Null(tenant.Type);                                  // D5: không loại hình
        Assert.Equal(TenantStatus.Active, tenant.Status);          // auto-verified
        Assert.Equal(BusinessType.HouseholdBusiness, tenant.BusinessType);
        Assert.Equal(id, tenant.Id);
    }

    // ── M2: Member capital snapshot ────────────────────────────────────────

    [Fact]
    public void Member_CreateActive_CapturesCapitalContribution()
    {
        var htx = new TenantId(Guid.NewGuid());
        var memberTenant = new TenantId(Guid.NewGuid());

        var member = Member.CreateActive(
            htx, "MEM-htx-test-000001", MembershipType.LinkedCapitalMember,
            memberTenantId: memberTenant, capitalContributionAmount: 10_000_000m);

        Assert.Equal(10_000_000m, member.CapitalContributionAmount);
        Assert.Equal(MembershipType.LinkedCapitalMember, member.MembershipType);
        Assert.Equal(memberTenant, member.MemberTenantId);
        Assert.Null(member.MemberCustomerId);
    }

    // ── M3: MemberRegistryService.AddMemberAsync ───────────────────────────

    private async Task<(TestContextScope scope, VanAnDbContext db, MemberRegistryService svc, TenantId htxId, TenantId memberTenantId)> SetupAddMemberAsync(
        bool htxWithProfile = true, TenantStatus memberStatus = TenantStatus.Active)
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;

        // HTX tenant (đã MarkAsHtx → TT71)
        var htxId = new TenantId(Guid.NewGuid());
        var htx = TenantAggregate.CreateCompany(htxId, "HTX Sản Xuất A");
        htx.MarkAsHtx();
        db.Tenants.Add(htx);
        if (htxWithProfile)
        {
            db.HtxProfiles.Add(HtxProfile.Create(htxId, "v1.0", "v1.0"));
        }

        // Member tenant
        var memberTenantId = new TenantId(Guid.NewGuid());
        var memberTenant = memberStatus == TenantStatus.Pending
            ? TenantAggregate.CreateUnverified(memberTenantId, "HKD Thành Viên B", null, "pending-hkd-b-1234")
            : TenantAggregate.CreateCompany(memberTenantId, "HKD Thành Viên B");
        db.Tenants.Add(memberTenant);

        await db.SaveChangesAsync();
        var svc = new MemberRegistryService(db, NullLogger<MemberRegistryService>.Instance);
        return (scope, db, svc, htxId, memberTenantId);
    }

    [Fact]
    public async Task AddMemberAsync_Success_CreatesMemberWithCapital()
    {
        var (_, _, svc, htxId, memberTenantId) = await SetupAddMemberAsync();

        var memberId = await svc.AddMemberAsync(
            htxId.Value, memberTenantId.Value, MembershipType.OfficialMember, 5_000_000m, Guid.NewGuid());

        var member = await svc.GetAsync(memberId);
        Assert.NotNull(member);
        Assert.Equal(htxId.Value, member!.HtxTenantId);
        Assert.Equal(memberTenantId.Value, member.MemberTenantId);
        Assert.Equal(5_000_000m, member.CapitalContributionAmount);
        Assert.StartsWith("MEM-", member.MemberNumber);
        Assert.Equal("Active", member.Status);
    }

    [Fact]
    public async Task AddMemberAsync_OfficialWithoutCapital_Throws()
    {
        var (_, _, svc, htxId, memberTenantId) = await SetupAddMemberAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AddMemberAsync(htxId.Value, memberTenantId.Value, MembershipType.OfficialMember, null, Guid.NewGuid()));
        Assert.Contains("capital contribution", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMemberAsync_NonCapitalWithCapital_Throws()
    {
        var (_, _, svc, htxId, memberTenantId) = await SetupAddMemberAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AddMemberAsync(htxId.Value, memberTenantId.Value, MembershipType.LinkedNonCapitalMember, 1_000_000m, Guid.NewGuid()));
        Assert.Contains("cannot declare", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMemberAsync_TenantNotActive_Throws()
    {
        var (_, _, svc, htxId, memberTenantId) = await SetupAddMemberAsync(memberStatus: TenantStatus.Pending);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddMemberAsync(htxId.Value, memberTenantId.Value, MembershipType.OfficialMember, 1_000_000m, Guid.NewGuid()));
        Assert.Contains("must be verified", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMemberAsync_HtxWithoutProfile_Throws()
    {
        var (_, _, svc, htxId, memberTenantId) = await SetupAddMemberAsync(htxWithProfile: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddMemberAsync(htxId.Value, memberTenantId.Value, MembershipType.OfficialMember, 1_000_000m, Guid.NewGuid()));
        Assert.Contains("not an HTX", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddMemberAsync_Duplicate_Throws()
    {
        var (_, _, svc, htxId, memberTenantId) = await SetupAddMemberAsync();
        await svc.AddMemberAsync(htxId.Value, memberTenantId.Value, MembershipType.OfficialMember, 1_000_000m, Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddMemberAsync(htxId.Value, memberTenantId.Value, MembershipType.OfficialMember, 1_000_000m, Guid.NewGuid()));
        Assert.Contains("already a member", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── M4: CollaboratorTenantProvisioningService ──────────────────────────

    [Fact]
    public async Task IsCollaborator_ActiveSalesman_True()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var tenantId = new TenantId(Guid.NewGuid());
        var customerId = Guid.NewGuid();
        db.CommunityRoles.Add(new CommunityRole(tenantId, customerId, CommunityRoleType.Salesman, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var svc = new CollaboratorTenantProvisioningService(db, NullLogger<CollaboratorTenantProvisioningService>.Instance);
        Assert.True(await svc.IsCollaboratorAsync(customerId));
        Assert.False(await svc.IsCollaboratorAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task IsCollaborator_Deactivated_False()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var tenantId = new TenantId(Guid.NewGuid());
        var customerId = Guid.NewGuid();
        var role = new CommunityRole(tenantId, customerId, CommunityRoleType.Shipper, Guid.NewGuid());
        role.Deactivate();
        db.CommunityRoles.Add(role);
        await db.SaveChangesAsync();

        var svc = new CollaboratorTenantProvisioningService(db, NullLogger<CollaboratorTenantProvisioningService>.Instance);
        Assert.False(await svc.IsCollaboratorAsync(customerId));
    }

    [Fact]
    public async Task GetOrCreateProfile_CreatesThenReuses()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var customerId = Guid.NewGuid();
        var svc = new CollaboratorTenantProvisioningService(db, NullLogger<CollaboratorTenantProvisioningService>.Instance);

        var first = await svc.GetOrCreateProfileAsync(customerId, "Nguyễn Văn CTV", "0900000000", "ctv@vanan.vn");
        var second = await svc.GetOrCreateProfileAsync(customerId, "Nguyễn Văn CTV");

        Assert.Equal(first, second); // idempotent — 1 tenant / 1 customer (D5)
        Assert.Equal(1, await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.OwnerCustomerId == customerId));

        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == first);
        Assert.Null(tenant.Type);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(customerId, tenant.OwnerCustomerId);
    }

    // ── M5: HtxProfileService.ListAsync ────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsProfilesWithTenantName()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var htx1 = new TenantId(Guid.NewGuid());
        var htx2 = new TenantId(Guid.NewGuid());
        db.Tenants.Add(TenantAggregate.CreateCompany(htx1, "HTX Một"));
        db.Tenants.Add(TenantAggregate.CreateCompany(htx2, "HTX Hai"));
        db.HtxProfiles.Add(HtxProfile.Create(htx1, "v1.0", "v1.0"));
        db.HtxProfiles.Add(HtxProfile.Create(htx2, "v2.0", "v2.0"));
        await db.SaveChangesAsync();

        var svc = new HtxProfileService(db, NullLogger<HtxProfileService>.Instance);
        var list = await svc.ListAsync();

        Assert.Equal(2, list.Count);
        Assert.Contains(list, p => p.TenantName == "HTX Một" && p.HtxTenantId == htx1.Value);
        Assert.Contains(list, p => p.TenantName == "HTX Hai" && p.CharterVersion == "v2.0");
    }
}
