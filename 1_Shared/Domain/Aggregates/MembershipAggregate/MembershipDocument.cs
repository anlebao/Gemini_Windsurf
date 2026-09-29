using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.MembershipAggregate
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Tài liệu đính kèm hồ sơ xin gia nhập (SRS §17.2 MembershipDocument).
    ///
    /// Các cách xác nhận đồng tham gia HTX:
    /// - Signature (chữ ký online vẽ tay): canvas capture → ảnh → StorageReference. EVIDENCE — theo SRS §14
    ///   KHÔNG được coi là chữ ký số đáp ứng yêu cầu pháp lý cụ thể.
    /// - PaperApplication (form giấy): in form → ký tay → scan/ảnh → StorageReference. Assisted Registration (SRS §26-27).
    /// - Chữ ký số thật: Phase 3 (SRS §40) — provider bên ngoài, KHÔNG nằm entity này.
    ///
    /// Append-only (evidence): KHÔNG có Update/Delete method — tài liệu là bằng chứng pháp lý.
    /// Hash: SHA-256 của file (toàn vẹn bằng chứng).
    /// BaseEntity.TenantId = htx_id; ApplicationId = FK MembershipApplications.Id.
    /// </summary>
    public class MembershipDocument : BaseEntity
    {
        public Guid ApplicationId { get; private set; }   // FK MembershipApplications.Id
        public MembershipDocumentType DocumentType { get; private set; }
        public string DocumentVersion { get; private set; } = string.Empty;
        public string StorageReference { get; private set; } = string.Empty;  // URL Cloudinary / storage
        public string? Hash { get; private set; }          // SHA-256 — toàn vẹn bằng chứng

        // EF Core requires parameterless constructor
        private MembershipDocument() { }

        /// <summary>
        /// Factory: đính kèm tài liệu vào hồ sơ xin gia nhập (append-only evidence).
        /// </summary>
        /// <param name="htxTenantId">HTX chủ quản (BaseEntity.TenantId).</param>
        /// <param name="applicationId">Hồ sơ xin gia nhập (FK MembershipApplications.Id).</param>
        /// <param name="documentType">Signature / PaperApplication / Charter / IdProof.</param>
        /// <param name="documentVersion">Phiên bản tài liệu (VD: "v2026-09-01").</param>
        /// <param name="storageReference">URL tham chiếu file đã upload (Cloudinary).</param>
        /// <param name="hash">SHA-256 file (optional — toàn vẹn bằng chứng).</param>
        public static MembershipDocument Create(
            TenantId htxTenantId,
            Guid applicationId,
            MembershipDocumentType documentType,
            string documentVersion,
            string storageReference,
            string? hash = null)
        {
            if (applicationId == Guid.Empty)
                throw new ArgumentException("ApplicationId cannot be empty.", nameof(applicationId));
            ArgumentException.ThrowIfNullOrWhiteSpace(storageReference);

            var document = new MembershipDocument
            {
                ApplicationId = applicationId,
                DocumentType = documentType,
                DocumentVersion = documentVersion ?? string.Empty,
                StorageReference = storageReference,
                Hash = hash
            };
            document.SetTenantId(htxTenantId);
            return document;
        }
    }
}
