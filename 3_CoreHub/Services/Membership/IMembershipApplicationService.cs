namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Lifecycle của hồ sơ xin gia nhập HTX (SRS §7, §16).
    /// Template: TenantClaimService (submit/approve/reject + guard status).
    ///
    /// Approve/Reject/RequestMoreInfo phải được gọi bởi HTX Membership Officer (HTX quyết định —
    /// Vạn An KHÔNG tự quyết — SRS §3.1). Authorization ở tầng API (Phase 4).
    /// </summary>
    public interface IMembershipApplicationService
    {
        /// <summary>Tạo hồ sơ Draft (Assisted Registration cần lưu nháp). Duplicate guard: 1 (htx, applicant) chỉ 1 hồ sơ active.</summary>
        Task<Guid> CreateApplicationAsync(CreateMembershipApplicationRequest request, CancellationToken ct = default);

        /// <summary>Applicant nộp hồ sơ: Draft → Submitted.</summary>
        Task SubmitAsync(Guid applicationId, CancellationToken ct = default);

        /// <summary>Applicant bổ sung thông tin: NeedInfo → Submitted.</summary>
        Task ResubmitAsync(Guid applicationId, CancellationToken ct = default);

        /// <summary>HTX yêu cầu bổ sung: Submitted → NeedInfo.</summary>
        Task RequestMoreInfoAsync(Guid applicationId, RequestMoreInfoRequest request, CancellationToken ct = default);

        /// <summary>
        /// HTX duyệt: Submitted → Approved + tạo Member Active trong cùng unit of work (D2).
        /// Trả về MemberId.
        /// </summary>
        Task<Guid> ApproveAsync(Guid applicationId, Guid reviewedByUserId, CancellationToken ct = default);

        /// <summary>HTX từ chối: Submitted → Rejected (kèm lý do — audit trail).</summary>
        Task RejectAsync(Guid applicationId, RejectApplicationRequest request, CancellationToken ct = default);

        /// <summary>Queue xét duyệt của HTX (SRS §15 dashboard).</summary>
        Task<IReadOnlyList<MembershipApplicationDto>> ListForHtxAsync(Guid htxTenantId, string? status = null, CancellationToken ct = default);

        /// <summary>Chi tiết 1 hồ sơ (applicant hoặc HTX officer).</summary>
        Task<MembershipApplicationDto?> GetAsync(Guid applicationId, CancellationToken ct = default);

        /// <summary>Danh sách hồ sơ của 1 applicant (my applications — cross-tenant read có chủ đích).</summary>
        Task<IReadOnlyList<MembershipApplicationDto>> ListForApplicantAsync(Guid applicantCustomerId, CancellationToken ct = default);
    }
}
