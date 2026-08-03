using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Bug 1 fix (Phase 3): Unit tests for Tenant.ChangeBusinessType() domain method.
/// Verifies guards + side effects (TenantType sync, event raised).
/// </summary>
public class TenantChangeBusinessTypeTests
{
    private static Tenant CreateHkdTenant()
    {
        var id = new TenantId(Guid.NewGuid());
        return Tenant.CreateHouseholdBusiness(id, "Test HKD", HKDGroup.Group1);
    }

    private static Tenant CreateCompanyTenant()
    {
        var id = new TenantId(Guid.NewGuid());
        var tenant = Tenant.CreateCompany(id, "Test Company");
        tenant.SetTenantType(TenantType.Enterprise_SME, AccountingStandard.TT133_2016);
        return tenant;
    }

    [Fact]
    public void ChangeBusinessType_HKD_to_Company_Succeeds()
    {
        var tenant = CreateHkdTenant();
        tenant.ChangeBusinessType(BusinessType.Company, null, "Created wrong type");
        Assert.Equal(BusinessType.Company, tenant.BusinessType);
        Assert.Null(tenant.HKDGroup);
        // TenantType stays HKD (we don't force-change it for Company — admin can call SetTenantType separately)
        // But BusinessType is the source of truth for HKD vs Company classification
    }

    [Fact]
    public void ChangeBusinessType_Company_to_HKD_Succeeds_With_HKDGroup()
    {
        var tenant = CreateCompanyTenant();
        tenant.ChangeBusinessType(BusinessType.HouseholdBusiness, HKDGroup.Group2, "Correction");
        Assert.Equal(BusinessType.HouseholdBusiness, tenant.BusinessType);
        Assert.Equal(HKDGroup.Group2, tenant.HKDGroup);
        // Side effect: TenantType synced to HKD for feature flag routing
        Assert.Equal(TenantType.HKD, tenant.Type);
    }

    [Fact]
    public void ChangeBusinessType_Company_to_HKD_Without_HKDGroup_Throws()
    {
        var tenant = CreateCompanyTenant();
        Assert.Throws<ArgumentException>(() =>
            tenant.ChangeBusinessType(BusinessType.HouseholdBusiness, null, "Correction"));
    }

    [Fact]
    public void ChangeBusinessType_HKD_to_Company_With_HKDGroup_Throws()
    {
        var tenant = CreateHkdTenant();
        Assert.Throws<ArgumentException>(() =>
            tenant.ChangeBusinessType(BusinessType.Company, HKDGroup.Group1, "Correction"));
    }

    [Fact]
    public void ChangeBusinessType_InactiveTenant_Throws()
    {
        var tenant = CreateHkdTenant();
        tenant.Deactivate("Test");
        Assert.Throws<InvalidOperationException>(() =>
            tenant.ChangeBusinessType(BusinessType.Company, null, "Correction"));
    }

    [Fact]
    public void ChangeBusinessType_ConvertedTenant_Throws()
    {
        var tenant = CreateHkdTenant();
        var successorId = new TenantId(Guid.NewGuid());
        tenant.MarkConvertedTo(successorId);
        Assert.Throws<InvalidOperationException>(() =>
            tenant.ChangeBusinessType(BusinessType.Company, null, "Correction"));
    }

    [Fact]
    public void ChangeBusinessType_EmptyReason_Throws()
    {
        var tenant = CreateHkdTenant();
        Assert.Throws<ArgumentException>(() =>
            tenant.ChangeBusinessType(BusinessType.Company, null, ""));
        Assert.Throws<ArgumentException>(() =>
            tenant.ChangeBusinessType(BusinessType.Company, null, "   "));
    }

    [Fact]
    public void ChangeBusinessType_Raises_DomainEvent()
    {
        var tenant = CreateHkdTenant();
        tenant.ClearDomainEvents(); // Clear TenantCreatedEvent
        tenant.ChangeBusinessType(BusinessType.Company, null, "Correction");
        var events = tenant.DomainEvents.OfType<TenantBusinessTypeChangedEvent>().ToList();
        Assert.Single(events);
        Assert.Equal(tenant.Id.Value, events[0].TenantId);
        Assert.Equal(BusinessType.Company, events[0].NewBusinessType);
        Assert.Null(events[0].NewHkdGroup);
        Assert.Equal("Correction", events[0].Reason);
    }
}
