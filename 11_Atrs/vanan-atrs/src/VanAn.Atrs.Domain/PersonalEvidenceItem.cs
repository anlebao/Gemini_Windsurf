namespace VanAn.Atrs.Domain;

/// <summary>Nguồn gốc của một personal evidence field (FR-006, ATRS-6).</summary>
public enum PersonalEvidenceSource
{
    USER_DECLARED = 0,       // user tự khai báo — KHÔNG BAO GIỜ thành VERIFIED
    VERIFIED_PROVIDER = 1,   // từ provider đã verify (vd OTP/email)
    STAFF_REVIEWED = 2,      // staff đã duyệt trong manual review
    MIGRATED = 3             // migrate từ hệ thống cũ
}

/// <summary>Trạng thái verify của evidence field — user-declared luôn UNVERIFIED.</summary>
public enum PersonalEvidenceVerificationStatus
{
    UNVERIFIED = 0,
    VERIFIED = 1,
    REJECTED = 2,
    EXPIRED = 3
}

/// <summary>
/// PersonalEvidenceItem — metadata/provenance của một trường thông tin cá nhân
/// hỗ trợ recovery/dispute (FR-006, §7.4). KHÔNG phải kho PII thứ hai:
/// chỉ lưu giá trị user chủ động khai báo cho mục đích trust; field nào đã có
/// nguồn verify (vd phone qua VerifiedChannel) thì Source/Status phản ánh điều đó.
/// PrivacyNoticeVersion ghi version notice tại thời điểm collect (SRS §11).
/// </summary>
public sealed class PersonalEvidenceItem
{
    /// <summary>Field keys hợp lệ v1 (allowlist ở service layer).</summary>
    public const string FieldFullName = "FULL_NAME";
    public const string FieldContactPhone = "CONTACT_PHONE";
    public const string FieldRecoveryNote = "RECOVERY_NOTE";

    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public string? Value { get; private set; }
    public PersonalEvidenceSource Source { get; private set; }
    public PersonalEvidenceVerificationStatus VerificationStatus { get; private set; }
    public string PrivacyNoticeVersion { get; private set; } = string.Empty;
    public Guid? ConsentRecordId { get; private set; }
    public DateTime CollectedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PersonalEvidenceItem() { }

    public PersonalEvidenceItem(
        Guid id, Guid trustSubjectId, string fieldKey, string? value,
        PersonalEvidenceSource source, string privacyNoticeVersion,
        Guid? consentRecordId = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty) throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (string.IsNullOrWhiteSpace(fieldKey)) throw new ArgumentException("FieldKey cannot be empty.", nameof(fieldKey));
        if (string.IsNullOrWhiteSpace(privacyNoticeVersion))
            throw new ArgumentException("PrivacyNoticeVersion cannot be empty.", nameof(privacyNoticeVersion));

        Id = id;
        TrustSubjectId = trustSubjectId;
        FieldKey = fieldKey.Trim().ToUpperInvariant();
        Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        Source = source;
        // FR-006: self-declared KHÔNG BAO GIỜ auto-verify.
        VerificationStatus = source == PersonalEvidenceSource.USER_DECLARED
            ? PersonalEvidenceVerificationStatus.UNVERIFIED
            : PersonalEvidenceVerificationStatus.UNVERIFIED;
        PrivacyNoticeVersion = privacyNoticeVersion.Trim();
        ConsentRecordId = consentRecordId;
        CollectedAt = DateTime.UtcNow;
    }

    /// <summary>User cập nhật giá trị — reset về USER_DECLARED/UNVERIFIED (mất mọi verify trước đó).</summary>
    public void UpdateDeclaredValue(string? value, string privacyNoticeVersion, Guid? consentRecordId = null)
    {
        Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        Source = PersonalEvidenceSource.USER_DECLARED;
        VerificationStatus = PersonalEvidenceVerificationStatus.UNVERIFIED;
        PrivacyNoticeVersion = privacyNoticeVersion.Trim();
        if (consentRecordId.HasValue)
            ConsentRecordId = consentRecordId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Chỉ staff/service được set VERIFIED (không có public path từ user).</summary>
    public void MarkVerified(PersonalEvidenceVerificationStatus status)
    {
        if (status is PersonalEvidenceVerificationStatus.UNVERIFIED)
            throw new ArgumentException("Cannot mark UNVERIFIED via this path.", nameof(status));
        VerificationStatus = status;
        UpdatedAt = DateTime.UtcNow;
    }
}
