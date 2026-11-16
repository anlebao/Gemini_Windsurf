using System.Text.Json;

namespace VanAn.Atrs.Contracts;

/// <summary>
/// WebAuthn DTOs (FR-004, ATRS-5). Options trả về dạng JSON chuẩn WebAuthn mà browser cần
/// (CredentialCreateOptions/AssertionOptions serialize đúng shape — Fido2.Models đã có [JsonPropertyName]).
/// </summary>

/// <summary>Kết quả begin registration — options JSON (client truyền thẳng vào navigator.credentials.create).</summary>
public sealed record BeginRegistrationResultDto(
    string Challenge,           // base64url — để client gửi kèm khi finish (server tra challenge store)
    JsonElement Options,        // CredentialCreateOptions (rp/user/challenge/pubKeyCredParams/...)
    DateTime ExpiresAt);

/// <summary>Kết quả begin authentication — options JSON (navigator.credentials.get).</summary>
public sealed record BeginAuthenticationResultDto(
    string Challenge,
    JsonElement Options,        // AssertionOptions (challenge/allowCredentials/...)
    DateTime ExpiresAt);

/// <summary>Credential an toàn cho UI (U06/U08) — KHÔNG public key/private key/log payload.</summary>
public sealed record DeviceCredentialSummaryDto(
    Guid Id,
    string CredentialId,        // base64url — cần cho revoke
    string RpId,
    string[]? Transports,
    bool? BackupEligible,
    bool? BackupState,
    string Status,              // CredentialStatus
    DateTime CreatedAt,
    DateTime? LastUsedAt);

/// <summary>Kết quả finish authentication — thành công + issue step-up token (amr=passkey).</summary>
public sealed record FinishAuthenticationResultDto(
    bool Success,
    string Reason,              // Success | InvalidChallenge | ExpiredChallenge | InvalidAssertion | RevokedCredential | WrongSubject
    Guid? CredentialId,
    string? StepUpToken);

/// <summary>Kết quả finish registration — credential mới (U03 success).</summary>
public sealed record FinishRegistrationResultDto(
    bool Success,
    string Reason,              // Success | InvalidChallenge | ExpiredChallenge | InvalidAttestation | DuplicateCredential | OriginMismatch
    DeviceCredentialSummaryDto? Credential,
    Guid? DeviceRecordId);

/// <summary>Device cho U07 list (SRS §7.2 an toàn — không lộ fingerprint nhạy cảm).</summary>
public sealed record UserDeviceSummaryDto(
    Guid Id,
    string DeviceId,            // server-issued
    string DeviceType,          // DeviceType
    string? Platform,
    string? FriendlyName,
    string RegistrationMethod,  // RegistrationMethod
    string Status,              // DeviceStatus
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    DateTime? RevokedAt,
    int ActiveCredentialCount);

/// <summary>Device detail U08 — kèm danh sách credential an toàn.</summary>
public sealed record UserDeviceDetailDto(
    UserDeviceSummaryDto Device,
    IReadOnlyList<DeviceCredentialSummaryDto> Credentials);