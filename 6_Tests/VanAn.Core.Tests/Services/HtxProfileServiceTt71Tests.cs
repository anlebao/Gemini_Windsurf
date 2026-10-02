using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Membership;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// TT 71/2024 (2026-10-01): HtxProfileService hook — tạo HtxProfile đồng thời
/// phân loại tenant: Type=HTX + AccountingStandard=TT71_2024 (Chế độ kế toán HTX).
/// </summary>
public class HtxProfileServiceTt71Tests
{
    [Fact(DisplayName = "TT71-HS1: GetOrCreateAsync tạo profile + mark tenant HTX/TT71")]
    public async Task GetOrCreateAsync_CreatesProfile_AndMarksTenantHtx()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX Sản Xuất Nông Nghiệp A");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var svc = new HtxProfileService(db, NullLogger<HtxProfileService>.Instance);
        var dto = await svc.GetOrCreateAsync(tenant.Id.Value, "v2026-10-01", "v2026-10-01");

        Assert.NotNull(dto);
        var reloaded = await db.Tenants
            .IgnoreQueryFilters()
            .SingleAsync(t => t.Id == tenant.Id);
        Assert.Equal(TenantType.HTX, reloaded.Type);
        Assert.Equal(AccountingStandard.TT71_2024, reloaded.AccountingStandard);
    }

    [Fact(DisplayName = "TT71-HS2: GetOrCreateAsync idempotent — gọi lại không đổi trạng thái")]
    public async Task GetOrCreateAsync_Twice_Idempotent()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX Dịch Vụ B");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var svc = new HtxProfileService(db, NullLogger<HtxProfileService>.Instance);
        await svc.GetOrCreateAsync(tenant.Id.Value, "v1", "v1");
        await svc.GetOrCreateAsync(tenant.Id.Value, "v1", "v1");

        var reloaded = await db.Tenants
            .IgnoreQueryFilters()
            .SingleAsync(t => t.Id == tenant.Id);
        Assert.Equal(TenantType.HTX, reloaded.Type);
        Assert.Equal(AccountingStandard.TT71_2024, reloaded.AccountingStandard);
        Assert.Equal(1, await db.HtxProfiles.IgnoreQueryFilters().CountAsync());
    }

    [Fact(DisplayName = "TT71-HS3: Tenant HKD có sẵn (Type=HKD) vẫn chuyển được sang HTX khi tạo profile")]
    public async Task GetOrCreateAsync_FromHkdTenant_MarksHtx()
    {
        using TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        var hkd = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateHouseholdBusiness(new TenantId(Guid.NewGuid()), "HKD Cũ Nay Là HTX", HKDGroup.Group1);
        db.Tenants.Add(hkd);
        await db.SaveChangesAsync();

        var svc = new HtxProfileService(db, NullLogger<HtxProfileService>.Instance);
        await svc.GetOrCreateAsync(hkd.Id.Value, "v1", "v1");

        var reloaded = await db.Tenants
            .IgnoreQueryFilters()
            .SingleAsync(t => t.Id == hkd.Id);
        Assert.Equal(TenantType.HTX, reloaded.Type);
        Assert.Equal(AccountingStandard.TT71_2024, reloaded.AccountingStandard);
    }
}
