using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.MembershipAggregate
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Hồ sơ xin gia nhập HTX (SRS §7, §13).
    /// Template: TenantClaimRequest (guard-based lifecycle + events + Single-Identity Pattern).
    ///
    /// Lifecycle: Draft → Submitted → NeedInfo ⇄ Submitted → Approved | Rejected.
    ///
    /// - BaseEntity.TenantId = htx_id (FK Tenants.Id — HTX chủ quản; multi-tenancy tự động).
    /// - ApplicantCustomerId = Vạn An Network ID (FK Customers.Id) — applicant luôn là CÁ NHÂN.
    /// - BusinessTenantId (nullable) = xin gia nhập với TƯ CÁCH HKD/DN (FK Tenants.Id).
    ///   Polymorphic theo Data Integrity Contract: member sau khi duyệt = customer XOR tenant.
    ///
    /// Data Integrity (master_plan mục 6):
    /// - Cấm xóa vật lý — chỉ chuyển trạng thái.
    /// - Guard self-membership: BusinessTenantId != TenantId (HTX không xin gia nhập chính nó).
    /// - Approve() KHÔNG tự tạo Member — service layer làm (D2) trong cùng unit of work.
    /// </summary>
    public class MembershipApplication : AggregateRoot
    {
        // ── Party (polymorphic — applicant luôn là cá nhân; tư cách business qua BusinessTenantId) ──
        public Guid ApplicantCustomerId { get; private set; }   // FK Customers.Id — Vạn An Network ID
        // FK Tenants.Id (PK là TenantId VO — pattern Tenant.PredecessorTenantId) — xin gia nhập với tư cách HKD/DN
        public TenantId? BusinessTenantId { get; private set; }

        public MembershipType MembershipType { get; private set; }
        public ApplicationStatus Status { get; private set; } = ApplicationStatus.Draft;

        // ── Applicant Profile snapshot (SRS §13) — lưu tại thời điểm tạo, không đọc live từ Customer ──
        public string FullName { get; private set; } = string.Empty;
        public string PhoneNumber { get; private set; } = string.Empty;
        public string? Email { get; private set; }
        public string? Region { get; private set; }
        public ExpectedRole ExpectedRole { get; private set; }

        // ── Xác thực + consent (SRS §11, §14) ──
        public IdentityVerificationLevel IdentityVerificationLevel { get; private set; } = IdentityVerificationLevel.Level1Otp;
        public string ConsentVersion { get; private set; } = string.Empty;
        public string CharterVersion { get; private set; } = string.Empty;
        public CapitalFeeStatus CapitalFeeStatus { get; private set; } = CapitalFeeStatus.None;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        public DateTime? SubmittedAt { get; private set; }
        public Guid? ReviewedByUserId { get; private set; }
        public DateTime? ReviewedAt { get; private set; }
        public string? RejectionReason { get; private set; }
        public string? NeedInfoReason { get; private set; }

        // EF Core requires parameterless constructor
        private MembershipApplication() { }

        /// <summary>
        /// Factory: tạo hồ sơ xin gia nhập (trạng thái Draft — Assisted Registration cần lưu nháp).
        /// KHÔNG raise event (chưa có gì để notify).
        /// </summary>
        /// <param name="htxTenantId">HTX chủ quản (BaseEntity.TenantId — FK Tenants.Id).</param>
        /// <param name="applicantCustomerId">Vạn An Network ID (FK Customers.Id).</param>
        /// <param name="businessTenantId">Tư cách HKD/DN (FK Tenants.Id) — null nếu đăng ký cá nhân.</param>
        public static MembershipApplication Create(
            TenantId htxTenantId,
            Guid applicantCustomerId,
            MembershipType membershipType,
            string fullName,
            string phoneNumber,
            string? email,
            string? region,
            ExpectedRole expectedRole,
            TenantId? businessTenantId = null,
            string? charterVersion = null,
            string? consentVersion = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
            ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
            if (applicantCustomerId == Guid.Empty)
                throw new ArgumentException("ApplicantCustomerId cannot be empty.", nameof(applicantCustomerId));
            if (businessTenantId is not null && businessTenantId == htxTenantId)
                throw new InvalidOperationException("A business cannot apply to join its own HTX (self-membership).");

            var application = new MembershipApplication
            {
                ApplicantCustomerId = applicantCustomerId,
                BusinessTenantId = businessTenantId,
                MembershipType = membershipType,
                FullName = fullName,
                PhoneNumber = phoneNumber,
                Email = email,
                Region = region,
                ExpectedRole = expectedRole,
                CharterVersion = charterVersion ?? string.Empty,
                ConsentVersion = consentVersion ?? string.Empty,
                Status = ApplicationStatus.Draft
            };
            application.SetTenantId(htxTenantId);
            return application;
        }

        // ── Lifecycle transitions (guard-based — template TenantClaimRequest) ────

        /// <summary>Applicant nộp hồ sơ: Draft → Submitted. Raise MembershipApplicationSubmittedEvent.</summary>
        public void Submit()
        {
            if (Status != ApplicationStatus.Draft)
                throw new InvalidOperationException($"Cannot submit application in status {Status}. Only Draft applications can be submitted.");

            Status = ApplicationStatus.Submitted;
            SubmittedAt = DateTime.UtcNow;
            UpdateAudit();
            AddDomainEvent(new MembershipApplicationSubmittedEvent(
                TenantId.Value, Id, ApplicantCustomerId, DateTime.UtcNow));
        }

        /// <summary>
        /// HTX yêu cầu bổ sung thông tin: Submitted → NeedInfo.
        /// Applicant bổ sung rồi gọi Resubmit() → Submitted.
        /// </summary>
        public void RequestMoreInfo(Guid reviewedByUserId, string reason)
        {
            if (Status != ApplicationStatus.Submitted)
                throw new InvalidOperationException($"Cannot request more info for application in status {Status}. Only Submitted applications accept info requests.");
            if (reviewedByUserId == Guid.Empty)
                throw new ArgumentException("ReviewedByUserId cannot be empty.", nameof(reviewedByUserId));
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            Status = ApplicationStatus.NeedInfo;
            ReviewedByUserId = reviewedByUserId;
            ReviewedAt = DateTime.UtcNow;
            NeedInfoReason = reason;
            UpdateAudit();
        }

        /// <summary>Applicant bổ sung thông tin: NeedInfo → Submitted.</summary>
        public void Resubmit()
        {
            if (Status != ApplicationStatus.NeedInfo)
                throw new InvalidOperationException($"Cannot resubmit application in status {Status}. Only NeedInfo applications can be resubmitted.");

            Status = ApplicationStatus.Submitted;
            NeedInfoReason = null;
            UpdateAudit();
        }

        /// <summary>
        /// HTX duyệt: Submitted → Approved. Raise MembershipApplicationApprovedEvent.
        /// KHÔNG tạo Member ở đây — service layer gọi Member.CreateActive trong cùng unit of work (D2).
        /// </summary>
        public void Approve(Guid reviewedByUserId)
        {
            if (Status != ApplicationStatus.Submitted)
                throw new InvalidOperationException($"Cannot approve application in status {Status}. Only Submitted applications can be approved.");
            if (reviewedByUserId == Guid.Empty)
                throw new ArgumentException("ReviewedByUserId cannot be empty.", nameof(reviewedByUserId));

            Status = ApplicationStatus.Approved;
            ReviewedByUserId = reviewedByUserId;
            ReviewedAt = DateTime.UtcNow;
            UpdateAudit();
            AddDomainEvent(new MembershipApplicationApprovedEvent(
                TenantId.Value, Id, reviewedByUserId, DateTime.UtcNow));
        }

        /// <summary>HTX từ chối: Submitted → Rejected, kèm lý do (audit trail — SRS §28).</summary>
        public void Reject(Guid reviewedByUserId, string reason)
        {
            if (Status != ApplicationStatus.Submitted)
                throw new InvalidOperationException($"Cannot reject application in status {Status}. Only Submitted applications can be rejected.");
            if (reviewedByUserId == Guid.Empty)
                throw new ArgumentException("ReviewedByUserId cannot be empty.", nameof(reviewedByUserId));
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            Status = ApplicationStatus.Rejected;
            ReviewedByUserId = reviewedByUserId;
            ReviewedAt = DateTime.UtcNow;
            RejectionReason = reason;
            UpdateAudit();
            AddDomainEvent(new MembershipApplicationRejectedEvent(
                TenantId.Value, Id, reviewedByUserId, reason, DateTime.UtcNow));
        }

        // ── Query helpers ─────────────────────────────────────────────────────
        public bool IsDraft() => Status == ApplicationStatus.Draft;
        public bool IsSubmitted() => Status == ApplicationStatus.Submitted;
        public bool IsNeedInfo() => Status == ApplicationStatus.NeedInfo;
        public bool IsApproved() => Status == ApplicationStatus.Approved;
        public bool IsRejected() => Status == ApplicationStatus.Rejected;

        /// <summary>Application đang trong luồng review (chưa kết thúc) — dùng cho duplicate guard.</summary>
        public bool IsActiveReview() => Status is ApplicationStatus.Draft or ApplicationStatus.Submitted or ApplicationStatus.NeedInfo;
    }
}
