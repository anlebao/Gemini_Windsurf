using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.MembershipAggregate
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Bản ghi consent (SRS §14 — Digital Consent).
    /// Mục tiêu: chứng minh người dùng đã xác nhận PHIÊN BẢN tài liệu nào tại THỜI ĐIỂM nào.
    ///
    /// Audit entity (BaseEntity — không lifecycle, không domain events).
    /// KHÔNG lưu nội dung tài liệu — chỉ version + evidence reference (IP/device).
    /// Luật Giao dịch điện tử 2023: tách "electronic evidence/consent" khỏi "chữ ký số" (SRS §14).
    /// </summary>
    public class ConsentRecord : BaseEntity
    {
        public Guid ApplicantCustomerId { get; private set; }   // FK Customers.Id — Vạn An Network ID
        public ConsentDocumentType DocumentType { get; private set; }
        public string DocumentVersion { get; private set; } = string.Empty;
        public DateTime Timestamp { get; private set; }
        public string? EvidenceReference { get; private set; }  // IP/device metadata where appropriate

        // EF Core requires parameterless constructor
        private ConsentRecord() { }

        /// <summary>
        /// Factory: ghi nhận consent của applicant cho 1 phiên bản tài liệu.
        /// </summary>
        /// <param name="htxTenantId">HTX chủ quản (BaseEntity.TenantId).</param>
        /// <param name="applicantCustomerId">Vạn An Network ID (FK Customers.Id).</param>
        /// <param name="documentType">Điều lệ / Điều kiện / Chính sách dữ liệu.</param>
        /// <param name="documentVersion">Phiên bản tài liệu được xác nhận (VD: "v2026-09-01").</param>
        /// <param name="evidenceReference">IP/device metadata (optional).</param>
        public static ConsentRecord Create(
            TenantId htxTenantId,
            Guid applicantCustomerId,
            ConsentDocumentType documentType,
            string documentVersion,
            string? evidenceReference = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(documentVersion);
            if (applicantCustomerId == Guid.Empty)
                throw new ArgumentException("ApplicantCustomerId cannot be empty.", nameof(applicantCustomerId));

            var record = new ConsentRecord
            {
                ApplicantCustomerId = applicantCustomerId,
                DocumentType = documentType,
                DocumentVersion = documentVersion,
                Timestamp = DateTime.UtcNow,
                EvidenceReference = evidenceReference
            };
            record.SetTenantId(htxTenantId);
            return record;
        }
    }
}
