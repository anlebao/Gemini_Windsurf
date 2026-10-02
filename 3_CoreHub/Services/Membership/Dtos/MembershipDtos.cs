using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.CoreHub.Services.Membership
{
    // ── Membership Infrastructure DTOs (2026-09-29) ─────────────────────────────

    /// <summary>
    /// Create application request (SRS §13). HtxTenantId: HTX chủ quản (tenant đã có HtxProfile).
    /// ApplicantCustomerId: Vạn An Network ID. BusinessTenantId: tư cách HKD/DN (optional).
    /// </summary>
    public record CreateMembershipApplicationRequest(
        Guid HtxTenantId,
        Guid ApplicantCustomerId,
        MembershipType MembershipType,
        string FullName,
        string PhoneNumber,
        string? Email,
        string? Region,
        ExpectedRole ExpectedRole,
        Guid? BusinessTenantId = null,
        string? CharterVersion = null,
        string? ConsentVersion = null);

    /// <summary>HTX yêu cầu bổ sung thông tin (Submitted → NeedInfo).</summary>
    public record RequestMoreInfoRequest(Guid ReviewedByUserId, string Reason);

    /// <summary>HTX từ chối hồ sơ (Submitted → Rejected) — reason bắt buộc (audit, SRS §28).</summary>
    public record RejectApplicationRequest(Guid ReviewedByUserId, string Reason);

    public record MembershipApplicationDto(
        Guid Id,
        Guid HtxTenantId,
        Guid ApplicantCustomerId,
        Guid? BusinessTenantId,
        string MembershipType,
        string Status,
        string FullName,
        string PhoneNumber,
        string? Email,
        string? Region,
        string ExpectedRole,
        string IdentityVerificationLevel,
        string ConsentVersion,
        string CharterVersion,
        string CapitalFeeStatus,
        DateTime? SubmittedAt,
        Guid? ReviewedByUserId,
        DateTime? ReviewedAt,
        string? RejectionReason,
        string? NeedInfoReason,
        DateTime CreatedAt);

    public record MemberDto(
        Guid Id,
        Guid HtxTenantId,
        Guid? MemberCustomerId,
        Guid? MemberTenantId,
        string MembershipType,
        string MemberNumber,
        string Status,
        DateTime JoinedAt,
        DateTime? ApprovedAt,
        DateTime? EffectiveAt,
        DateTime? TerminatedAt,
        string? StatusReason,
        decimal? CapitalContributionAmount); // 2026-10-02: vốn góp cam kết (Luồng 2 — SystemAdmin add)

    /// <summary>
    /// Public status verify (SRS §22) — KHÔNG PII: chỉ status + HTX name + joined date.
    /// Không hiển thị CCCD/địa chỉ/vốn góp cho người không có quyền.
    /// </summary>
    public record MemberStatusDto(
        bool IsActive,
        string Status,
        string MemberNumber,
        string? HtxName,
        DateTime JoinedAt);

    public record ConsentDto(
        Guid Id,
        Guid HtxTenantId,
        Guid ApplicantCustomerId,
        string DocumentType,
        string DocumentVersion,
        DateTime Timestamp,
        string? EvidenceReference);

    public record HtxProfileDto(
        Guid Id,
        Guid HtxTenantId,
        string CharterVersion,
        string TermsVersion,
        string? CharterUrl);

    /// <summary>2026-10-02: summary HTX profile cho SystemAdmin review dropdown.</summary>
    public record HtxProfileSummaryDto(
        Guid HtxTenantId,
        string TenantName,
        string CharterVersion);

    // ── 2026-10-02 (user directive): 2 luồng admin — Luồng 1 tenant→HTX · Luồng 2 member add ──

    /// <summary>Luồng 1: tạo HtxProfile hộ (SystemAdmin) — tài liệu upload tùy chọn qua CharterUrl.</summary>
    public record CreateHtxProfileAdminRequest(
        Guid HtxTenantId,
        string? CharterVersion = null,
        string? TermsVersion = null,
        string? CharterUrl = null);

    /// <summary>Luồng 2: thêm tenant làm thành viên HTX (SystemAdmin) — MembershipType string enum (camelCase).</summary>
    public record AddHtxMemberRequest(
        Guid HtxTenantId,
        Guid MemberTenantId,
        string MembershipType,
        decimal? CapitalContributionAmount = null);

    /// <summary>D4: nâng cấp cộng tác viên (Salesman/Shipper) thành tenant profile (SystemAdmin).</summary>
    public record CollaboratorUpgradeRequest(
        Guid CustomerId,
        string? DisplayName = null);

    /// <summary>Tài liệu đính kèm hồ sơ (SRS §17.2 — chữ ký online, form giấy scan...).</summary>
    public record MembershipDocumentDto(
        Guid Id,
        Guid HtxTenantId,
        Guid ApplicationId,
        string DocumentType,
        string DocumentVersion,
        string StorageReference,
        string? Hash,
        DateTime CreatedAt);

    /// <summary>Request đính kèm tài liệu (applicant sở hữu hồ sơ).</summary>
    public record AttachMembershipDocumentRequest(
        string DocumentType,
        string DocumentVersion,
        string StorageReference,
        string? Hash = null);
}
