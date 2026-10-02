using FluentAssertions;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;

namespace VanAn.Core.Tests.Domain;

/// <summary>
/// Membership Infrastructure Phase 6 (2026-09-29): Domain tests cho Membership module.
/// Covers Data Integrity Contract (master_plan mục 6):
/// - Polymorphic party: exactly-one (customer XOR tenant) + self-membership guard.
/// - State machine: ApplicationStatus + MemberStatus — mọi transition sai throw.
/// - Cấm xóa member/application vật lý (không có Delete method).
/// - Events: MembershipApplicationSubmittedEvent/ApprovedEvent/RejectedEvent, MemberActivatedEvent, MemberStatusChangedEvent.
/// </summary>
public class MembershipDomainTests
{
    private static readonly TenantId HtxId = new(Guid.NewGuid());
    private static readonly TenantId OtherTenantId = new(Guid.NewGuid());
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ReviewerId = Guid.NewGuid();

    // ── MembershipApplication factory ────────────────────────────────────────

    [Fact(DisplayName = "Create_ProducesDraft_NoEvent")]
    public void Create_ProducesDraft_NoEvent()
    {
        var app = CreateApplication();

        app.Status.Should().Be(ApplicationStatus.Draft);
        app.ApplicantCustomerId.Should().Be(CustomerId);
        app.BusinessTenantId.Should().BeNull();
        app.TenantId.Should().Be(HtxId);
        app.IsActiveReview().Should().BeTrue();
        app.DomainEvents.Should().BeEmpty();
    }

    [Fact(DisplayName = "Create_WithBusinessTenant_SetsParty")]
    public void Create_WithBusinessTenant_SetsParty()
    {
        var app = MembershipApplication.Create(
            HtxId, CustomerId, MembershipType.OfficialMember,
            "Nguyễn Văn A", "0901234567", null, null, ExpectedRole.Seller,
            businessTenantId: OtherTenantId,
            charterVersion: "v2026-09-01", consentVersion: "v2026-09-01");

        app.BusinessTenantId.Should().Be(OtherTenantId);
        app.CharterVersion.Should().Be("v2026-09-01");
    }

    [Fact(DisplayName = "Create_SelfMembership_Throws")]
    public void Create_SelfMembership_Throws()
    {
        // Data Integrity Contract mục 4: HTX không thể xin gia nhập chính nó
        var act = () => MembershipApplication.Create(
            HtxId, CustomerId, MembershipType.OfficialMember,
            "Nguyễn Văn A", "0901234567", null, null, ExpectedRole.Seller,
            businessTenantId: HtxId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*self-membership*");
    }

    [Fact(DisplayName = "Create_EmptyApplicant_Throws")]
    public void Create_EmptyApplicant_Throws()
    {
        var act = () => MembershipApplication.Create(
            HtxId, Guid.Empty, MembershipType.OfficialMember,
            "Nguyễn Văn A", "0901234567", null, null, ExpectedRole.Seller);

        act.Should().Throw<ArgumentException>();
    }

    // ── Application lifecycle ────────────────────────────────────────────────

    [Fact(DisplayName = "Submit_DraftToSubmitted_RaisesEvent")]
    public void Submit_DraftToSubmitted_RaisesEvent()
    {
        var app = CreateApplication();
        app.Submit();

        app.Status.Should().Be(ApplicationStatus.Submitted);
        app.SubmittedAt.Should().NotBeNull();
        app.DomainEvents.Should().ContainSingle(e => e is MembershipApplicationSubmittedEvent);
        app.IsActiveReview().Should().BeTrue();
    }

    [Fact(DisplayName = "Submit_FromSubmitted_Throws")]
    public void Submit_FromSubmitted_Throws()
    {
        var app = CreateApplication();
        app.Submit();

        var act = () => app.Submit();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Approve_SubmittedToApproved_RaisesEvent")]
    public void Approve_SubmittedToApproved_RaisesEvent()
    {
        var app = CreateApplication();
        app.Submit();
        app.Approve(ReviewerId);

        app.Status.Should().Be(ApplicationStatus.Approved);
        app.ReviewedByUserId.Should().Be(ReviewerId);
        app.ReviewedAt.Should().NotBeNull();
        app.DomainEvents.Should().ContainSingle(e => e is MembershipApplicationApprovedEvent);
        // D2: Approve KHÔNG tự tạo Member — service layer làm
        app.IsActiveReview().Should().BeFalse();
    }

    [Fact(DisplayName = "Approve_FromDraft_Throws")]
    public void Approve_FromDraft_Throws()
    {
        var app = CreateApplication();
        var act = () => app.Approve(ReviewerId);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Reject_SubmittedToRejected_WithReason")]
    public void Reject_SubmittedToRejected_WithReason()
    {
        var app = CreateApplication();
        app.Submit();
        app.Reject(ReviewerId, "Thiếu giấy tờ xác minh");

        app.Status.Should().Be(ApplicationStatus.Rejected);
        app.RejectionReason.Should().Be("Thiếu giấy tờ xác minh");
        app.DomainEvents.Should().ContainSingle(e => e is MembershipApplicationRejectedEvent);
    }

    [Fact(DisplayName = "Reject_EmptyReason_Throws")]
    public void Reject_EmptyReason_Throws()
    {
        var app = CreateApplication();
        app.Submit();

        var act = () => app.Reject(ReviewerId, "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "RequestMoreInfo_Then_Resubmit_LoopsBackToSubmitted")]
    public void RequestMoreInfo_Then_Resubmit_LoopsBackToSubmitted()
    {
        var app = CreateApplication();
        app.Submit();
        app.RequestMoreInfo(ReviewerId, "Cần bổ sung CMND");

        app.Status.Should().Be(ApplicationStatus.NeedInfo);
        app.NeedInfoReason.Should().Be("Cần bổ sung CMND");

        app.Resubmit();
        app.Status.Should().Be(ApplicationStatus.Submitted);
        app.NeedInfoReason.Should().BeNull();
    }

    [Fact(DisplayName = "RequestMoreInfo_FromDraft_Throws")]
    public void RequestMoreInfo_FromDraft_Throws()
    {
        var app = CreateApplication();
        var act = () => app.RequestMoreInfo(ReviewerId, "Bổ sung");
        act.Should().Throw<InvalidOperationException>();
    }

    // ── Member factory — polymorphic party (Data Integrity Contract mục 1) ──

    [Fact(DisplayName = "CreateActive_CustomerMember_Active_WithEvent")]
    public void CreateActive_CustomerMember_Active_WithEvent()
    {
        var member = Member.CreateActive(HtxId, "MEM-HTX-Q1-000001", MembershipType.OfficialMember,
            memberCustomerId: CustomerId);

        member.Status.Should().Be(MemberStatus.Active);
        member.MemberCustomerId.Should().Be(CustomerId);
        member.MemberTenantId.Should().BeNull();
        member.JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        member.ApprovedAt.Should().NotBeNull();
        member.DomainEvents.Should().ContainSingle(e => e is MemberActivatedEvent);
    }

    [Fact(DisplayName = "CreateActive_BusinessMember_Active")]
    public void CreateActive_BusinessMember_Active()
    {
        var member = Member.CreateActive(HtxId, "MEM-HTX-Q1-000002", MembershipType.LinkedCapitalMember,
            memberTenantId: OtherTenantId);

        member.MemberCustomerId.Should().BeNull();
        member.MemberTenantId.Should().Be(OtherTenantId);
    }

    [Fact(DisplayName = "CreateActive_BothParties_Throws_ExactlyOne")]
    public void CreateActive_BothParties_Throws_ExactlyOne()
    {
        // Data Integrity Contract mục 1: exactly-one — cả 2 cùng set bị cấm
        var act = () => Member.CreateActive(HtxId, "MEM-1", MembershipType.OfficialMember,
            memberCustomerId: CustomerId, memberTenantId: OtherTenantId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly one party*");
    }

    [Fact(DisplayName = "CreateActive_NoParty_Throws_ExactlyOne")]
    public void CreateActive_NoParty_Throws_ExactlyOne()
    {
        var act = () => Member.CreateActive(HtxId, "MEM-1", MembershipType.OfficialMember);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly one party*");
    }

    [Fact(DisplayName = "CreateActive_SelfMembership_Throws")]
    public void CreateActive_SelfMembership_Throws()
    {
        // Data Integrity Contract mục 4: tenant không thể là member của chính nó
        var act = () => Member.CreateActive(HtxId, "MEM-1", MembershipType.OfficialMember,
            memberTenantId: HtxId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*self-membership*");
    }

    // ── Member lifecycle (cấm xóa vật lý — status transition only) ──────────

    [Fact(DisplayName = "Member_Suspend_Reactivate_Lifecycle")]
    public void Member_Suspend_Reactivate_Lifecycle()
    {
        var member = CreateCustomerMember();

        member.Suspend("Vi phạm Điều lệ");
        member.Status.Should().Be(MemberStatus.Suspended);
        member.StatusReason.Should().Be("Vi phạm Điều lệ");
        member.DomainEvents.Should().ContainSingle(e => e is MemberStatusChangedEvent
            && ((MemberStatusChangedEvent)e).OldStatus == MemberStatus.Active
            && ((MemberStatusChangedEvent)e).NewStatus == MemberStatus.Suspended);

        member.Reactivate();
        member.Status.Should().Be(MemberStatus.Active);
        member.IsActive().Should().BeTrue();
    }

    [Fact(DisplayName = "Member_Suspend_FromTerminated_Throws")]
    public void Member_Suspend_FromTerminated_Throws()
    {
        var member = CreateCustomerMember();
        member.Terminate("Chấm dứt tư cách");

        var act = () => member.Suspend("Suspending terminated member");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Member_Resign_SetsResigned")]
    public void Member_Resign_SetsResigned()
    {
        var member = CreateCustomerMember();
        member.Resign("Tự nguyện rút");

        member.Status.Should().Be(MemberStatus.Resigned);
        member.IsActive().Should().BeFalse();
    }

    [Fact(DisplayName = "Member_Terminate_SetsTerminatedAt")]
    public void Member_Terminate_SetsTerminatedAt()
    {
        var member = CreateCustomerMember();
        member.Terminate("Chấm dứt theo Điều lệ");

        member.Status.Should().Be(MemberStatus.Terminated);
        member.TerminatedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "Member_Resign_FromResigned_Throws")]
    public void Member_Resign_FromResigned_Throws()
    {
        var member = CreateCustomerMember();
        member.Resign("Rút");

        var act = () => member.Terminate("Terminate after resign");
        act.Should().Throw<InvalidOperationException>();
    }

    // ── ConsentRecord ────────────────────────────────────────────────────────

    [Fact(DisplayName = "ConsentRecord_Create_SnapshotFields")]
    public void ConsentRecord_Create_SnapshotFields()
    {
        var record = ConsentRecord.Create(HtxId, CustomerId, ConsentDocumentType.Charter, "v2026-09-01", "ip:1.2.3.4");

        record.DocumentType.Should().Be(ConsentDocumentType.Charter);
        record.DocumentVersion.Should().Be("v2026-09-01");
        record.EvidenceReference.Should().Be("ip:1.2.3.4");
        record.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "ConsentRecord_Create_EmptyVersion_Throws")]
    public void ConsentRecord_Create_EmptyVersion_Throws()
    {
        var act = () => ConsentRecord.Create(HtxId, CustomerId, ConsentDocumentType.Terms, " ");
        act.Should().Throw<ArgumentException>();
    }

    // ── HtxProfile ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "HtxProfile_Create_And_UpdateCharter")]
    public void HtxProfile_Create_And_UpdateCharter()
    {
        var profile = HtxProfile.Create(HtxId, "v2026-09-01", "v2026-09-01");
        profile.CharterVersion.Should().Be("v2026-09-01");

        profile.UpdateCharter("v2026-10-01", "https://vanan.vn/charter-v2.pdf");
        profile.CharterVersion.Should().Be("v2026-10-01");
        profile.CharterUrl.Should().Be("https://vanan.vn/charter-v2.pdf");
    }

    // ── TT 71/2024 — Tenant.MarkAsHtx (2026-10-01) ──────────────────────────

    [Fact(DisplayName = "TT71-MH1: MarkAsHtx từ Type=null → HTX + AccountingStandard=TT71_2024")]
    public void MarkAsHtx_FromUnclassified_SetsHtxAndTt71()
    {
        var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX Sản Xuất Nông Nghiệp A");

        tenant.MarkAsHtx();

        tenant.Type.Should().Be(TenantType.HTX);
        tenant.AccountingStandard.Should().Be(AccountingStandard.TT71_2024);
    }

    [Fact(DisplayName = "TT71-MH2: MarkAsHtx cho phép chuyển từ HKD/Enterprise (HtxProfile là marker chính thức)")]
    public void MarkAsHtx_FromClassifiedTenant_AllowsTransition()
    {
        var hkd = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateHouseholdBusiness(new TenantId(Guid.NewGuid()), "HKD Test", HKDGroup.Group1);
        hkd.MarkAsHtx();
        hkd.Type.Should().Be(TenantType.HTX);

        var dn = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "DN Test");
        dn.SetTenantType(TenantType.Enterprise_SME, AccountingStandard.TT133_2016);
        dn.MarkAsHtx();
        dn.Type.Should().Be(TenantType.HTX);
        dn.AccountingStandard.Should().Be(AccountingStandard.TT71_2024);
    }

    [Fact(DisplayName = "TT71-MH3: MarkAsHtx idempotent — gọi 2 lần không throw, giữ HTX")]
    public void MarkAsHtx_Idempotent()
    {
        var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX B");
        tenant.MarkAsHtx();
        tenant.MarkAsHtx();
        tenant.Type.Should().Be(TenantType.HTX);
    }

    [Fact(DisplayName = "TT71-MH4: MarkAsHtx trên tenant Inactive → throw")]
    public void MarkAsHtx_Inactive_Throws()
    {
        var tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "HTX C");
        tenant.Deactivate("test");
        var act = () => tenant.MarkAsHtx();
        act.Should().Throw<InvalidOperationException>();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static MembershipApplication CreateApplication()
        => MembershipApplication.Create(
            HtxId, CustomerId, MembershipType.OfficialMember,
            "Nguyễn Văn A", "0901234567", null, null, ExpectedRole.Seller);

    private static Member CreateCustomerMember()
        => Member.CreateActive(HtxId, "MEM-HTX-Q1-000001", MembershipType.OfficialMember,
            memberCustomerId: CustomerId);
}
