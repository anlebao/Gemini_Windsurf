using System.Text.Json;
using VanAn.Atrs.Contracts;

namespace VanAn.Atrs.Infra;

/// <summary>
/// WebAuthn/passkey ceremony service (FR-004, ATRS-5). Dựa trên Fido2 lib (không tự viết crypto).
/// Challenge one-time, TTL ngắn, binding subject + origin + RP ID + purpose; replay → reject.
/// KHÔNG lưu private key (enforced: entity không có field private key).
/// </summary>
public interface IWebAuthnService
{
    /// <summary>Begin registration — tạo challenge + options; challenge lưu server-side chờ verify.</summary>
    Task<BeginRegistrationResultDto> BeginRegistrationAsync(
        Guid trustSubjectId, Guid? deviceRecordId, string origin, string rpId, CancellationToken ct = default);

    /// <summary>Finish registration — verify attestation (origin/RP ID/challenge), lưu credential.</summary>
    Task<FinishRegistrationResultDto> FinishRegistrationAsync(
        Guid trustSubjectId, string challenge, string origin, string rpId, JsonElement attestationResponse,
        Guid? deviceRecordId, CancellationToken ct = default);

    /// <summary>Begin authentication — options với allowCredentials = các credential ACTIVE của subject.</summary>
    Task<BeginAuthenticationResultDto> BeginAuthenticationAsync(
        Guid trustSubjectId, string origin, string rpId, CancellationToken ct = default);

    /// <summary>Finish authentication — verify assertion (signature + counter + origin), cập nhật lastUsed.</summary>
    Task<FinishAuthenticationResultDto> FinishAuthenticationAsync(
        Guid trustSubjectId, string challenge, string origin, string rpId, JsonElement assertionResponse,
        CancellationToken ct = default);

    /// <summary>Danh sách credential ACTIVE của subject (U06/U08) — không expose public key.</summary>
    Task<IReadOnlyList<DeviceCredentialSummaryDto>> ListCredentialsAsync(Guid trustSubjectId, CancellationToken ct = default);

    /// <summary>Thu hồi 1 credential — mọi credential của device bị REVOKED (ATRS-3 step-up gate do caller enforce).</summary>
    Task<bool> RevokeCredentialAsync(Guid trustSubjectId, Guid credentialId, CancellationToken ct = default);

    /// <summary>Thu hồi toàn bộ credential của 1 device (U08 revoke device / report lost).</summary>
    Task<int> RevokeAllForDeviceAsync(Guid trustSubjectId, Guid deviceRecordId, CancellationToken ct = default);
}

/// <summary>WebAuthn RP config (RP ID + origin allowlist).</summary>
public sealed class WebAuthnOptions
{
    public string RpName { get; set; } = "Vạn An";
    /// <summary>RP ID mặc định (apex domain). Multi-subdomain → passkey share cross-subdomain.</summary>
    public string RpId { get; set; } = "timlathay.com";
    /// <summary>Origin allowlist — KHÔNG có origin nào → chặn mọi ceremony.</summary>
    public List<string> Origins { get; set; } = [];
    public int ChallengeTtlMinutes { get; set; } = 5;
    public uint TimeoutMs { get; set; } = 300000;
    public string UserVerification { get; set; } = "Preferred";
    public string ResidentKey { get; set; } = "Preferred";
    public string AttestationConveyance { get; set; } = "None";
}