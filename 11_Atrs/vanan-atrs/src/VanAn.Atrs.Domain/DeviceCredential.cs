namespace VanAn.Atrs.Domain;

/// <summary>
/// DeviceCredential — WebAuthn/passkey credential của một TrustSubject (SRS §7.3, FR-004).
/// KHÔNG BAO GIỜ có field PrivateKey — chỉ public key + metadata. CredentialId unique toàn hệ.
/// </summary>
public sealed class DeviceCredential
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public Guid? DeviceRecordId { get; private set; }

    /// <summary>Credential ID (base64url, chuẩn WebAuthn) — unique toàn hệ.</summary>
    public string CredentialId { get; private set; } = null!;

    /// <summary>Public key (base64url của COSE key, như Fido2 lib trả về). KHÔNG private key.</summary>
    public string PublicKey { get; private set; } = null!;

    /// <summary>Signature counter (không tin cậy — không dùng làm tiêu chí duy nhất, FR-004).</summary>
    public uint SignCount { get; private set; }

    public string RpId { get; private set; } = null!;
    public Guid? Aaguid { get; private set; }

    /// <summary>Transports JSON array (["usb","nfc","ble","internal",...]) — nullable.</summary>
    public string? TransportsJson { get; private set; }

    public bool? BackupEligible { get; private set; }
    public bool? BackupState { get; private set; }
    public CredentialStatus Status { get; private set; } = CredentialStatus.ACTIVE;

    public DateTime CreatedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }

    private DeviceCredential() { }

    public DeviceCredential(
        Guid id,
        Guid trustSubjectId,
        string credentialId,
        string publicKey,
        uint signCount,
        string rpId,
        Guid? deviceRecordId,
        Guid? aaguid,
        string? transportsJson,
        bool? backupEligible,
        bool? backupState)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty)
            throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (string.IsNullOrWhiteSpace(credentialId))
            throw new ArgumentException("CredentialId cannot be empty.", nameof(credentialId));
        if (string.IsNullOrWhiteSpace(publicKey))
            throw new ArgumentException("PublicKey cannot be empty.", nameof(publicKey));
        if (string.IsNullOrWhiteSpace(rpId))
            throw new ArgumentException("RpId cannot be empty.", nameof(rpId));

        Id = id;
        TrustSubjectId = trustSubjectId;
        CredentialId = credentialId;
        PublicKey = publicKey;
        SignCount = signCount;
        RpId = rpId;
        DeviceRecordId = deviceRecordId;
        Aaguid = aaguid;
        TransportsJson = transportsJson;
        BackupEligible = backupEligible;
        BackupState = backupState;
        Status = CredentialStatus.ACTIVE;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkUsed(uint signCount, bool? backupState)
    {
        SignCount = signCount;
        BackupState = backupState ?? BackupState;
        LastUsedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        if (Status == CredentialStatus.REVOKED)
            return;
        Status = CredentialStatus.REVOKED;
        LastUsedAt = DateTime.UtcNow;
    }
}

/// <summary>Status credential (SRS §7.3: ACTIVE/REVOKED).</summary>
public enum CredentialStatus
{
    ACTIVE = 0,
    REVOKED = 1
}