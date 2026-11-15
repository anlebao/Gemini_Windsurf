namespace VanAn.Atrs.Contracts;

/// <summary>
/// Trust profile summary — GET /api/v1/account-trust/me (SRS §8).
/// Không expose hash/PII; chỉ state an toàn cho UI (U06 security center).
/// </summary>
public sealed record TrustProfileSummaryDto(
    Guid TrustSubjectId,
    string Status,          // TrustProfileStatus
    string AssuranceState,  // AssuranceState
    int ActiveDeviceCount,
    int ActivePasskeyCount,
    bool HasRecoveryCodes,
    DateTime? LastSecurityActivityAt);