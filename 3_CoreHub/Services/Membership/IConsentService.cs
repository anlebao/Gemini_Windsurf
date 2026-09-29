using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Digital Consent (SRS §14).
    /// Mục tiêu: chứng minh người dùng đã xác nhận PHIÊN BẢN tài liệu nào tại THỜI ĐIỂM nào.
    /// Append-only — không sửa/xóa bản ghi consent (evidence).
    /// </summary>
    public interface IConsentService
    {
        /// <summary>Ghi nhận consent của applicant cho 1 phiên bản tài liệu.</summary>
        Task<Guid> RecordConsentAsync(
            Guid htxTenantId,
            Guid applicantCustomerId,
            ConsentDocumentType documentType,
            string documentVersion,
            string? evidenceReference = null,
            CancellationToken ct = default);

        /// <summary>Lịch sử consent của 1 applicant tại 1 HTX (desc theo timestamp).</summary>
        Task<IReadOnlyList<ConsentDto>> ListForApplicantAsync(
            Guid htxTenantId,
            Guid applicantCustomerId,
            CancellationToken ct = default);
    }
}
