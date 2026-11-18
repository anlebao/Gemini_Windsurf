using System.Text.Json;

namespace VanAn.Atrs.Contracts;

/// <summary>
/// ATRS-21 — discoverable passkey LOGIN (subject-unknown ceremony).
/// Khác với Authentication flow (subject-scoped, step-up): login không biết subject trước —
/// challenge lưu với TrustSubjectId = Guid.Empty sentinel, credential resolve sau verify.
/// </summary>

/// <summary>Kết quả begin passkey login — AssertionOptions với allowCredentials rỗng (discoverable).</summary>
public sealed record BeginPasskeyLoginResultDto(
    string Challenge,
    JsonElement Options,        // AssertionOptions — client truyền vào navigator.credentials.get
    DateTime ExpiresAt);

/// <summary>
/// Kết quả finish passkey login — thành công kèm customer_token do ShopERP mint.
/// Reason codes generic cho client (anti-enumeration): Success | InvalidChallenge | UnknownCredential
/// | RevokedCredential | InvalidAssertion | NoCustomerIdentity | TokenIssuanceFailed.
/// </summary>
public sealed record FinishPasskeyLoginResultDto(
    bool Success,
    string ReasonCode,
    string? CustomerToken,
    Guid? CustomerId,
    bool CustomerCreated);

/// <summary>Kết quả ShopERP mint customer token (internal call atrs → ShopERP).</summary>
public sealed record PasskeyTokenResult(
    bool Success,
    string? ReasonCode,
    string? CustomerToken,
    Guid? CustomerId,
    bool CustomerCreated);

/// <summary>
/// Abstraction atrs → ShopERP customer token issuance (ATRS-21).
/// Impl: ShopErpTokenIssuer (named HttpClient "shoperp" + X-Internal-Api-Key).
/// </summary>
public interface IPasskeyLoginTokenIssuer
{
    /// <summary>subject có TrustSubjectLink Customer → customerId đã biết.</summary>
    Task<PasskeyTokenResult> IssueByCustomerIdAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>guest DeviceCluster → ShopERP find-or-create customer stub theo deviceId.</summary>
    Task<PasskeyTokenResult> IssueByDeviceAsync(string deviceId, Guid? tenantId, CancellationToken ct = default);
}
