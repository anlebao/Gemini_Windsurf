using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.MembershipAggregate
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Domain events cho Membership module.
    /// Format theo TenantEvents.cs (TenantAggregate).
    /// </summary>

    /// <summary>
    /// Raised khi applicant nộp hồ sơ (Draft → Submitted).
    /// Trigger: HTX review queue notification.
    /// </summary>
    public sealed record MembershipApplicationSubmittedEvent(
        Guid TenantId,
        Guid ApplicationId,
        Guid ApplicantCustomerId,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised khi HTX duyệt hồ sơ (Submitted → Approved).
    /// KHÔNG kèm MemberId — MemberActivatedEvent (từ Member.CreateActive) mang thông tin member;
    /// service layer tạo Member trong cùng unit of work (D2).
    /// </summary>
    public sealed record MembershipApplicationApprovedEvent(
        Guid TenantId,
        Guid ApplicationId,
        Guid ReviewedByUserId,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised khi HTX từ chối hồ sơ (Submitted → Rejected) — kèm lý do (audit trail).
    /// </summary>
    public sealed record MembershipApplicationRejectedEvent(
        Guid TenantId,
        Guid ApplicationId,
        Guid ReviewedByUserId,
        string Reason,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised khi Member được kích hoạt (thành viên chính thức — SRS §7 ACTIVE).
    /// NetworkCustomerId null khi member là business (HKD/DN) — representative lấy từ application.
    /// </summary>
    public sealed record MemberActivatedEvent(
        Guid TenantId,
        Guid MemberId,
        string MemberNumber,
        Guid? NetworkCustomerId,
        MembershipType MembershipType,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised khi Member đổi trạng thái sau ACTIVE (SUSPENDED/RESIGNED/TERMINATED/Reactivate).
    /// </summary>
    public sealed record MemberStatusChangedEvent(
        Guid TenantId,
        Guid MemberId,
        MemberStatus OldStatus,
        MemberStatus NewStatus,
        string? Reason,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }
}
