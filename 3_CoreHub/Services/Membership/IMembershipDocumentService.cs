using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Tài liệu đính kèm hồ sơ (SRS §17.2).
    /// Các cách xác nhận đồng tham gia: Signature (chữ ký online — evidence), PaperApplication (form giấy ký tay + scan).
    /// Append-only evidence — không sửa/xóa.
    /// </summary>
    public interface IMembershipDocumentService
    {
        /// <summary>
        /// Applicant đính kèm tài liệu vào hồ sơ của chính mình.
        /// Ownership check: application.ApplicantCustomerId == applicantCustomerId (IDOR-safe).
        /// </summary>
        Task<Guid> AttachAsync(
            Guid applicationId,
            Guid applicantCustomerId,
            MembershipDocumentType documentType,
            string documentVersion,
            string storageReference,
            string? hash = null,
            CancellationToken ct = default);

        /// <summary>Danh sách tài liệu của 1 hồ sơ (officer hoặc applicant sở hữu).</summary>
        Task<IReadOnlyList<MembershipDocumentDto>> ListForApplicationAsync(Guid applicationId, CancellationToken ct = default);
    }
}
