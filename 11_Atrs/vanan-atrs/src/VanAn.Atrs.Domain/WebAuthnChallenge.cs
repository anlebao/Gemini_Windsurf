namespace VanAn.Atrs.Domain;

/// <summary>
/// WebAuthnChallenge — challenge one-time cho WebAuthn ceremony (FR-004, AC-003).
/// Ràng buộc với subject + origin + RP ID + purpose; TTL ngắn; replay → reject.
/// </summary>
public sealed class WebAuthnChallenge
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public WebAuthnChallengePurpose Purpose { get; private set; }
    public string Challenge { get; private set; } = null!;      // base64url
    public string Origin { get; private set; } = null!;
    public string RpId { get; private set; } = null!;
    public Guid? DeviceRecordId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }

    /// <summary>Toàn bộ options JSON (CredentialCreateOptions/AssertionOptions) — cần cho verify stage.</summary>
    public string OptionsJson { get; private set; } = null!;

    private WebAuthnChallenge() { }

    public WebAuthnChallenge(
        Guid id,
        Guid trustSubjectId,
        WebAuthnChallengePurpose purpose,
        string challenge,
        string origin,
        string rpId,
        Guid? deviceRecordId,
        DateTime expiresAt,
        string optionsJson)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        // ATRS-21: PasskeyLogin challenge là subject-unknown ceremony — TrustSubjectId = Guid.Empty
        // sentinel (subject resolve sau khi verify assertion qua credentialId). Các purpose khác
        // vẫn bắt buộc subject thật.
        if (trustSubjectId == Guid.Empty && purpose != WebAuthnChallengePurpose.PasskeyLogin)
            throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (string.IsNullOrWhiteSpace(challenge))
            throw new ArgumentException("Challenge cannot be empty.", nameof(challenge));
        if (string.IsNullOrWhiteSpace(origin))
            throw new ArgumentException("Origin cannot be empty.", nameof(origin));
        if (string.IsNullOrWhiteSpace(rpId))
            throw new ArgumentException("RpId cannot be empty.", nameof(rpId));
        if (string.IsNullOrWhiteSpace(optionsJson))
            throw new ArgumentException("OptionsJson cannot be empty.", nameof(optionsJson));

        Id = id;
        TrustSubjectId = trustSubjectId;
        Purpose = purpose;
        Challenge = challenge;
        Origin = origin;
        RpId = rpId;
        DeviceRecordId = deviceRecordId;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
        OptionsJson = optionsJson;
    }

    public bool IsExpired(DateTime now) => now >= ExpiresAt;

    public void Consume(DateTime now)
    {
        if (ConsumedAt.HasValue)
            throw new InvalidOperationException("Challenge đã được dùng (replay).");
        ConsumedAt = now;
    }
}

public enum WebAuthnChallengePurpose
{
    Registration = 1,
    Authentication = 2,
    /// <summary>ATRS-21: discoverable passkey login — subject unknown lúc begin (Guid.Empty sentinel).</summary>
    PasskeyLogin = 3
}