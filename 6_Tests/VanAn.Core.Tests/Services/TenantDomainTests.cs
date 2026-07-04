using FluentAssertions;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Wave 5 — W5-T10: Domain tests for TenantAggregate.
/// Covers: factory creation, lifecycle guards, domain events, settings.
/// Minimum 10 cases required per exit criteria.
/// </summary>
public class TenantDomainTests
{
    private static TenantId NewId() => new(Guid.NewGuid());

    // ── 1. CreateCompany factory ──────────────────────────────────────────────

    [Fact(DisplayName = "W5-D1: CreateCompany produces Active tenant with correct BusinessType")]
    public void CreateCompany_ProducesActiveTenant()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Cong Ty ABC");

        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.BusinessType.Should().Be(BusinessType.Company);
        tenant.Name.Should().Be("Cong Ty ABC");
        tenant.IsActive().Should().BeTrue();
    }

    [Fact(DisplayName = "W5-D2: CreateCompany raises TenantCreatedEvent")]
    public void CreateCompany_RaisesTenantCreatedEvent()
    {
        var settings = new TenantSettings("owner@abc.vn", null, null);
        var tenant = Tenant.CreateCompany(NewId(), "ABC", settings);

        tenant.DomainEvents.Should().ContainSingle(e => e is TenantCreatedEvent);
        var evt = (TenantCreatedEvent)tenant.DomainEvents.First();
        evt.TenantName.Should().Be("ABC");
        evt.ContactEmail.Should().Be("owner@abc.vn");
    }

    // ── 2. CreateHouseholdBusiness factory ───────────────────────────────────

    [Fact(DisplayName = "W5-D3: CreateHouseholdBusiness produces correct HKDGroup")]
    public void CreateHouseholdBusiness_ProducesCorrectHKDGroup()
    {
        var tenant = Tenant.CreateHouseholdBusiness(NewId(), "HKD Quoc Anh", HKDGroup.Group1);

        tenant.IsHouseholdBusiness().Should().BeTrue();
        tenant.HKDGroup.Should().Be(HKDGroup.Group1);
        tenant.Status.Should().Be(TenantStatus.Active);
    }

    // ── 3. Suspend ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "W5-D4: Suspend changes status to Suspended and raises event")]
    public void Suspend_ChangesStatusToSuspended()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Corp");
        tenant.ClearDomainEvents();

        tenant.Suspend("Unpaid invoice");

        tenant.Status.Should().Be(TenantStatus.Suspended);
        tenant.IsSuspended().Should().BeTrue();
        tenant.DomainEvents.Should().ContainSingle(e => e is TenantSuspendedEvent);
    }

    [Fact(DisplayName = "W5-D5: Suspend already-suspended tenant throws InvalidOperationException")]
    public void Suspend_AlreadySuspended_Throws()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Corp");
        tenant.Suspend("reason 1");

        var act = () => tenant.Suspend("reason 2");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*already suspended*");
    }

    [Fact(DisplayName = "W5-D6: Suspend inactive tenant throws InvalidOperationException")]
    public void Suspend_InactiveTenant_Throws()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Corp");
        tenant.Deactivate("closing down");

        var act = () => tenant.Suspend("reason");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*inactive*");
    }

    // ── 4. Reactivate ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "W5-D7: Reactivate restores Active status from Suspended")]
    public void Reactivate_FromSuspended_ReturnsToActive()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Corp");
        tenant.Suspend("test");
        tenant.ClearDomainEvents();

        tenant.Reactivate();

        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.IsActive().Should().BeTrue();
    }

    [Fact(DisplayName = "W5-D8: Reactivate on Active tenant throws InvalidOperationException")]
    public void Reactivate_OnActiveTenant_Throws()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Corp");

        var act = () => tenant.Reactivate();

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*suspended*");
    }

    // ── 5. Deactivate ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "W5-D9: Deactivate sets status to Inactive and raises event")]
    public void Deactivate_SetsInactiveStatus()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Corp");
        tenant.ClearDomainEvents();

        tenant.Deactivate("shutting down");

        tenant.Status.Should().Be(TenantStatus.Inactive);
        tenant.DomainEvents.Should().ContainSingle(e => e is TenantDeactivatedEvent);
    }

    [Fact(DisplayName = "W5-D10: Deactivate already-inactive tenant throws InvalidOperationException")]
    public void Deactivate_AlreadyInactive_Throws()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Corp");
        tenant.Deactivate("reason");

        var act = () => tenant.Deactivate("again");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*already inactive*");
    }

    // ── 6. UpdateProfile ──────────────────────────────────────────────────────

    [Fact(DisplayName = "W5-D11: UpdateProfile changes name and settings")]
    public void UpdateProfile_ChangesNameAndSettings()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Old Name");
        var newSettings = new TenantSettings("new@email.com", "+84-900", "HN");

        tenant.UpdateProfile("New Name", newSettings);

        tenant.Name.Should().Be("New Name");
        tenant.Settings.ContactEmail.Should().Be("new@email.com");
    }

    [Fact(DisplayName = "W5-D12: UpdateProfile on inactive tenant throws")]
    public void UpdateProfile_InactiveTenant_Throws()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Corp");
        tenant.Deactivate("done");

        var act = () => tenant.UpdateProfile("Name", TenantSettings.Empty());

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*inactive*");
    }

    // ── 7. ClearDomainEvents ─────────────────────────────────────────────────

    [Fact(DisplayName = "W5-D13: ClearDomainEvents removes all events")]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var tenant = Tenant.CreateCompany(NewId(), "Corp");
        tenant.Suspend("reason");
        tenant.DomainEvents.Should().HaveCountGreaterThan(0);

        tenant.ClearDomainEvents();

        tenant.DomainEvents.Should().BeEmpty();
    }

    // ── 8. D9: HKD↔DN Conversion (Wave 2) ─────────────────────────────────────

    [Fact(DisplayName = "W2-D1: CreateFromConversion sets PredecessorTenantId, ConvertedAt, AccountingStandard")]
    public void CreateFromConversion_SetsPredecessorAndStandard()
    {
        var predecessorId = NewId();
        var newId = NewId();
        var tenant = Tenant.CreateFromConversion(
            newId, "DN Moi", TenantType.Enterprise_SME,
            predecessorId, AccountingStandard.TT133_2016);

        tenant.Id.Should().Be(newId);
        tenant.BusinessType.Should().Be(BusinessType.Company);
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.PredecessorTenantId.Should().Be(predecessorId);
        tenant.ConvertedAt.Should().NotBeNull();
        tenant.AccountingStandard.Should().Be(AccountingStandard.TT133_2016);
        tenant.IsConversionOf(predecessorId).Should().BeTrue();
    }

    [Fact(DisplayName = "W2-D2: MarkConvertedTo sets Status=Converted, SuccessorTenantId, raises TenantConvertedEvent")]
    public void MarkConvertedTo_SetsStatusConvertedAndSuccessor()
    {
        var hkd = Tenant.CreateHouseholdBusiness(NewId(), "HKD Cu", HKDGroup.Group2);
        hkd.ClearDomainEvents();
        var successorId = NewId();

        hkd.MarkConvertedTo(successorId);

        hkd.Status.Should().Be(TenantStatus.Converted);
        hkd.IsConverted().Should().BeTrue();
        hkd.SuccessorTenantId.Should().Be(successorId);
        hkd.DomainEvents.Should().ContainSingle(e => e is TenantConvertedEvent);
        var evt = (TenantConvertedEvent)hkd.DomainEvents.First();
        evt.SuccessorTenantId.Should().Be(successorId.Value);
    }

    [Fact(DisplayName = "W2-D3: MarkConvertedTo on inactive tenant throws InvalidOperationException")]
    public void MarkConvertedTo_ThrowsOnInactive()
    {
        var hkd = Tenant.CreateHouseholdBusiness(NewId(), "HKD Cu", HKDGroup.Group2);
        hkd.Deactivate("closed");

        var act = () => hkd.MarkConvertedTo(NewId());

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*inactive*");
    }

    [Fact(DisplayName = "W2-D4: MarkConvertedTo on already-converted tenant throws InvalidOperationException")]
    public void MarkConvertedTo_ThrowsOnAlreadyConverted()
    {
        var hkd = Tenant.CreateHouseholdBusiness(NewId(), "HKD Cu", HKDGroup.Group2);
        hkd.MarkConvertedTo(NewId());

        var act = () => hkd.MarkConvertedTo(NewId());

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*already converted*");
    }
}
