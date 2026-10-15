using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Domain;

/// <summary>
/// 2026-10-15 (user directive): RevertTenantType — chuyển ngược HTX → Công ty/HKD.
/// MarkAsHtx là chuyển 1 chiều vào HTX; RevertTenantType là chiều ngược lại (có guard + audit).
/// </summary>
public class TenantRevertTypeTests
{
    [Fact(DisplayName = "REVERT-1: HTX → HKD — Type=HKD, AccountingStandard=null (TT 152 implied)")]
    public void Revert_HtxToHkd_SetsHkdTypeAndNullStandard()
    {
        Tenant tenant = Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX Test");
        tenant.MarkAsHtx();
        Assert.Equal(TenantType.HTX, tenant.Type);
        Assert.Equal(AccountingStandard.TT71_2024, tenant.AccountingStandard);

        tenant.RevertTenantType(TenantType.HKD, null);

        Assert.Equal(TenantType.HKD, tenant.Type);
        Assert.Null(tenant.AccountingStandard);
    }

    [Fact(DisplayName = "REVERT-2: HTX → Enterprise_SME — Type=SME, standard=TT133")]
    public void Revert_HtxToEnterpriseSme_SetsSmeAndTt133()
    {
        Tenant tenant = Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX Test");
        tenant.MarkAsHtx();

        tenant.RevertTenantType(TenantType.Enterprise_SME, AccountingStandard.TT133_2016);

        Assert.Equal(TenantType.Enterprise_SME, tenant.Type);
        Assert.Equal(AccountingStandard.TT133_2016, tenant.AccountingStandard);
    }

    [Fact(DisplayName = "REVERT-3: HTX → Enterprise_Large — standard=TT99")]
    public void Revert_HtxToEnterpriseLarge_SetsTt99()
    {
        Tenant tenant = Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX Test");
        tenant.MarkAsHtx();

        tenant.RevertTenantType(TenantType.Enterprise_Large, AccountingStandard.TT99_2025);

        Assert.Equal(TenantType.Enterprise_Large, tenant.Type);
        Assert.Equal(AccountingStandard.TT99_2025, tenant.AccountingStandard);
    }

    [Fact(DisplayName = "REVERT-4: Tenant KHÔNG phải HTX → throw")]
    public void Revert_NotHtx_Throws()
    {
        Tenant hkd = Tenant.CreateHouseholdBusiness(new TenantId(Guid.NewGuid()), "HKD", HKDGroup.Group1);
        Tenant company = Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "Công ty");

        Assert.Throws<InvalidOperationException>(() => hkd.RevertTenantType(TenantType.Enterprise_SME, AccountingStandard.TT133_2016));
        Assert.Throws<InvalidOperationException>(() => company.RevertTenantType(TenantType.Enterprise_SME, AccountingStandard.TT133_2016));
    }

    [Fact(DisplayName = "REVERT-5: Target = HTX → throw (phải dùng MarkAsHtx)")]
    public void Revert_ToHtx_Throws()
    {
        Tenant tenant = Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX Test");
        tenant.MarkAsHtx();

        Assert.Throws<InvalidOperationException>(() => tenant.RevertTenantType(TenantType.HTX, AccountingStandard.TT71_2024));
    }

    [Fact(DisplayName = "REVERT-6: Tenant inactive → throw")]
    public void Revert_Inactive_Throws()
    {
        Tenant tenant = Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX Test");
        tenant.MarkAsHtx();
        tenant.Deactivate("test");

        Assert.Throws<InvalidOperationException>(() => tenant.RevertTenantType(TenantType.HKD, null));
    }

    [Fact(DisplayName = "REVERT-7: MarkAsHtx vẫn hoạt động sau revert (vòng tròn HTX→HKD→HTX)")]
    public void Revert_ThenMarkAsHtxAgain_RoundTrip()
    {
        Tenant tenant = Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX Test");
        tenant.MarkAsHtx();
        tenant.RevertTenantType(TenantType.HKD, null);
        tenant.MarkAsHtx();

        Assert.Equal(TenantType.HTX, tenant.Type);
        Assert.Equal(AccountingStandard.TT71_2024, tenant.AccountingStandard);
    }
}
