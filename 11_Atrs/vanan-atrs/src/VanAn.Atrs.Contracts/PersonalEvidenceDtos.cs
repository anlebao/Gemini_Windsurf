namespace VanAn.Atrs.Contracts;

// ═══ ATRS-6 subset (Wave 4) — Personal evidence + consent DTOs (FR-006, §11) ═══

/// <summary>
/// Evidence item phía user — Value đã mask (không trả plaintext cho field nhạy cảm).
/// HasValue=false nghĩa là user chưa khai báo field này.
/// </summary>
public sealed record PersonalEvidenceItemDto(
    string FieldKey,               // FULL_NAME | CONTACT_PHONE | RECOVERY_NOTE
    bool HasValue,
    string? MaskedValue,
    string Source,                 // PersonalEvidenceSource
    string VerificationStatus,     // PersonalEvidenceVerificationStatus
    string PrivacyNoticeVersion,
    DateTime? UpdatedAt);

/// <summary>User upsert một field (chỉ field trong allowlist; luôn USER_DECLARED/UNVERIFIED).</summary>
public sealed record UpsertPersonalEvidenceRequest(
    string FieldKey,
    string? Value,
    string PrivacyNoticeVersion);

/// <summary>Consent record phía user (SRS §11.2).</summary>
public sealed record ConsentDto(
    Guid Id,
    string Purpose,
    string PrivacyNoticeVersion,
    DateTime GrantedAt,
    DateTime? WithdrawnAt);

/// <summary>User grant consent cho một purpose + notice version.</summary>
public sealed record GrantConsentRequest(
    string Purpose,
    string PrivacyNoticeVersion);
