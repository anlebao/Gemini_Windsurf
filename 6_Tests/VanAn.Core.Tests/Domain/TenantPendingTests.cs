using FluentAssertions;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using Xunit;

namespace VanAn.Core.Tests.Domain;

/// <summary>
/// Crawl-to-Onboard Phase 8 (2026-08-26): Domain tests for Pending tenant lifecycle.
/// Covers: CreateUnverified factory, Verify transition, MarkPotentialDuplicateOf,
/// UpdateSlug guard (correction C4 — unchanged), TenantClaimRequest aggregate.
/// Verifies corrections: H1 (Pending=5), H4 (Verify throws on duplicate), C4 (guard unchanged).
/// </summary>
public class TenantPendingTests
{
    private static readonly TenantId TestTenantId = new(Guid.NewGuid());
    private const string ValidPendingSlug = "pending-0106463914-a3f2";

    private static TenantSettings TestSettings() => new(
        contactEmail: null, contactPhone: null, address: "123 Lê Lợi",
        taxCode: "0106463914", crawledPhone: "0901234567");

    // ── CreateUnverified factory ───────────────────────────────────────────

    [Fact(DisplayName = "CreateUnverified_ProducesPendingTenant")]
    public void CreateUnverified_ProducesPendingTenant()
    {
        var tenant = Tenant.CreateUnverified(TestTenantId, "Cafe ABC", TestSettings(), ValidPendingSlug);

        tenant.Status.Should().Be(TenantStatus.Pending);
        tenant.Name.Should().Be("Cafe ABC");
        tenant.Id.Should().Be(TestTenantId);
        tenant.IsPending().Should().BeTrue();
    }

    [Fact(DisplayName = "CreateUnverified_RaisesTenantPendingEvent_NotTenantCreatedEvent")]
    public void CreateUnverified_RaisesTenantPendingEvent_NotTenantCreatedEvent()
    {
        var tenant = Tenant.CreateUnverified(TestTenantId, "Cafe ABC", TestSettings(), ValidPendingSlug);

        var events = tenant.DomainEvents;
        events.Should().ContainSingle(e => e is TenantPendingEvent);
        events.Should().NotContain(e => e is TenantCreatedEvent);
    }

    [Fact(DisplayName = "CreateUnverified_SetsPendingSlug_OnSettings")]
    public void CreateUnverified_SetsPendingSlug_OnSettings()
    {
        var tenant = Tenant.CreateUnverified(TestTenantId, "Cafe ABC", TestSettings(), ValidPendingSlug);

        tenant.Settings.Slug.Should().Be(ValidPendingSlug);
    }

    [Fact(DisplayName = "CreateUnverified_PreservesCrawledPhone_FromSettings")]
    public void CreateUnverified_PreservesCrawledPhone_FromSettings()
    {
        var settings = TestSettings();
        tenant_crawled_phone_preserved(settings);
    }

    private static void tenant_crawled_phone_preserved(TenantSettings settings)
    {
        var tenant = Tenant.CreateUnverified(TestTenantId, "Cafe ABC", settings, ValidPendingSlug);
        tenant.Settings.CrawledPhone.Should().Be("0901234567");
        // M3: ContactPhone stays null (consented phone comes from Claim form after Verify)
        tenant.Settings.ContactPhone.Should().BeNull();
    }

    [Fact(DisplayName = "CreateUnverified_RejectsInvalidSlug_Throws")]
    public void CreateUnverified_RejectsInvalidSlug_Throws()
    {
        var act = () => Tenant.CreateUnverified(TestTenantId, "Cafe", TestSettings(), "INVALID Slug!");
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "CreateUnverified_RejectsEmptyName_Throws")]
    public void CreateUnverified_RejectsEmptyName_Throws()
    {
        var act = () => Tenant.CreateUnverified(TestTenantId, "", TestSettings(), ValidPendingSlug);
        act.Should().Throw<ArgumentException>();
    }

    // ── Verify transition ──────────────────────────────────────────────────

    [Fact(DisplayName = "Verify_FromPending_TransitionsToActive")]
    public void Verify_FromPending_TransitionsToActive()
    {
        var tenant = Tenant.CreateUnverified(TestTenantId, "Cafe ABC", TestSettings(), ValidPendingSlug);

        tenant.Verify();

        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact(DisplayName = "Verify_RaisesTenantVerifiedEvent")]
    public void Verify_RaisesTenantVerifiedEvent()
    {
        var tenant = Tenant.CreateUnverified(TestTenantId, "Cafe ABC", TestSettings(), ValidPendingSlug);

        tenant.Verify();

        tenant.DomainEvents.Should().Contain(e => e is TenantVerifiedEvent);
    }

    [Fact(DisplayName = "Verify_FromActive_Throws")]
    public void Verify_FromActive_Throws()
    {
        var tenant = Tenant.CreateCompany(TestTenantId, "Cafe ABC", TestSettings());
        // tenant.Status == Active by default from CreateCompany

        var act = () => tenant.Verify();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Pending*");
    }

    [Fact(DisplayName = "Verify_FromInactive_Throws")]
    public void Verify_FromInactive_Throws()
    {
        var tenant = Tenant.CreateCompany(TestTenantId, "Cafe ABC", TestSettings());
        tenant.Deactivate("test");

        var act = () => tenant.Verify();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Verify_WithPotentialDuplicateOf_Throws (correction H4)")]
    public void Verify_WithPotentialDuplicateOf_Throws()
    {
        var tenant = Tenant.CreateUnverified(TestTenantId, "Cafe ABC", TestSettings(), ValidPendingSlug);
        tenant.MarkPotentialDuplicateOf(Guid.NewGuid());

        var act = () => tenant.Verify();
        act.Should().Throw<InvalidOperationException>().WithMessage("*duplicate*");
    }

    // ── MarkPotentialDuplicateOf ───────────────────────────────────────────

    [Fact(DisplayName = "MarkPotentialDuplicateOf_SetsFlag")]
    public void MarkPotentialDuplicateOf_SetsFlag()
    {
        var tenant = Tenant.CreateUnverified(TestTenantId, "Cafe ABC", TestSettings(), ValidPendingSlug);
        var otherId = Guid.NewGuid();

        tenant.MarkPotentialDuplicateOf(otherId);

        tenant.PotentialDuplicateOf.Should().Be(otherId);
    }

    [Fact(DisplayName = "MarkPotentialDuplicateOf_RejectsEmptyGuid")]
    public void MarkPotentialDuplicateOf_RejectsEmptyGuid()
    {
        var tenant = Tenant.CreateUnverified(TestTenantId, "Cafe ABC", TestSettings(), ValidPendingSlug);

        var act = () => tenant.MarkPotentialDuplicateOf(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    // ── UpdateSlug guard (correction C4 — guard UNCHANGED) ─────────────────

    [Fact(DisplayName = "UpdateSlug_OnActiveTenant_Succeeds")]
    public void UpdateSlug_OnActiveTenant_Succeeds()
    {
        var tenant = Tenant.CreateCompany(TestTenantId, "Cafe ABC", TestSettings());

        tenant.UpdateSlug("clean-slug");

        tenant.Settings.Slug.Should().Be("clean-slug");
    }

    [Fact(DisplayName = "UpdateSlug_OnSuspendedTenant_Succeeds (correction C4 — guard unchanged)")]
    public void UpdateSlug_OnSuspendedTenant_Succeeds()
    {
        var tenant = Tenant.CreateCompany(TestTenantId, "Cafe ABC", TestSettings());
        tenant.Suspend("test");

        // C4: guard only blocks Inactive, NOT Suspended — so this should succeed
        tenant.UpdateSlug("new-slug");
        tenant.Settings.Slug.Should().Be("new-slug");
    }

    [Fact(DisplayName = "UpdateSlug_OnInactiveTenant_Throws")]
    public void UpdateSlug_OnInactiveTenant_Throws()
    {
        var tenant = Tenant.CreateCompany(TestTenantId, "Cafe ABC", TestSettings());
        tenant.Deactivate("test");

        var act = () => tenant.UpdateSlug("new-slug");
        act.Should().Throw<InvalidOperationException>().WithMessage("*inactive*");
    }

    // ── TenantClaimRequest aggregate ───────────────────────────────────────

    [Fact(DisplayName = "TenantClaimRequest_Create_RaisesEvent")]
    public void TenantClaimRequest_Create_RaisesEvent()
    {
        var claim = TenantClaimRequest.Create(
            TestTenantId, "Nguyễn Văn A", "0901234567",
            "https://cloudinary.com/gpkd.jpg", "0106463914");

        claim.Status.Should().Be(TenantClaimRequest.ClaimStatus.Submitted);
        claim.ClaimantName.Should().Be("Nguyễn Văn A");
        claim.DomainEvents.Should().Contain(e => e is TenantClaimRequestedEvent);
    }

    [Fact(DisplayName = "TenantClaimRequest_Approve_TransitionsStatus")]
    public void TenantClaimRequest_Approve_TransitionsStatus()
    {
        var claim = TenantClaimRequest.Create(
            TestTenantId, "Nguyễn Văn A", "0901234567",
            "https://cloudinary.com/gpkd.jpg", "0106463914");

        claim.Approve(Guid.NewGuid(), Guid.NewGuid());

        claim.Status.Should().Be(TenantClaimRequest.ClaimStatus.Approved);
        claim.ReviewedAt.Should().NotBeNull();
        claim.DomainEvents.Should().Contain(e => e is TenantClaimApprovedEvent);
    }

    [Fact(DisplayName = "TenantClaimRequest_Reject_SetsReason")]
    public void TenantClaimRequest_Reject_SetsReason()
    {
        var claim = TenantClaimRequest.Create(
            TestTenantId, "Nguyễn Văn A", "0901234567",
            "https://cloudinary.com/gpkd.jpg", "0106463914");

        claim.Reject(Guid.NewGuid(), "GPKD không rõ nét");

        claim.Status.Should().Be(TenantClaimRequest.ClaimStatus.Rejected);
        claim.RejectionReason.Should().Be("GPKD không rõ nét");
    }

    [Fact(DisplayName = "TenantClaimRequest_Approve_OnAlreadyApproved_Throws")]
    public void TenantClaimRequest_Approve_OnAlreadyApproved_Throws()
    {
        var claim = TenantClaimRequest.Create(
            TestTenantId, "Nguyễn Văn A", "0901234567",
            "https://cloudinary.com/gpkd.jpg", "0106463914");
        claim.Approve(Guid.NewGuid(), Guid.NewGuid());

        var act = () => claim.Approve(Guid.NewGuid(), Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }
}
