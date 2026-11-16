namespace VanAn.Atrs.Domain;

/// <summary>
/// UserDevice — device registry (SRS §7.2, FR-003). Một TrustSubject có nhiều device.
/// DeviceId = ID do Vạn An cấp (risk signal, KHÔNG phải proof-of-identity badge).
/// Không lưu fingerprint nhạy cảm trong RiskMetadata (chỉ metadata tối thiểu).
/// </summary>
public sealed class UserDevice
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }

    /// <summary>ID do server cấp (UUID/string random) — identity chính của device.</summary>
    public string DeviceId { get; private set; } = null!;

    public DeviceType DeviceType { get; private set; } = DeviceType.Unknown;
    public string? Platform { get; private set; }
    public string? FriendlyName { get; private set; }
    public RegistrationMethod RegistrationMethod { get; private set; } = RegistrationMethod.OTHER;
    public DeviceStatus Status { get; private set; } = DeviceStatus.PENDING;

    public DateTime FirstSeenAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public string? RiskMetadataJson { get; private set; }

    private UserDevice() { }

    public UserDevice(Guid id, Guid trustSubjectId, string deviceId,
        RegistrationMethod registrationMethod, DeviceType deviceType = DeviceType.Unknown,
        string? platform = null, string? friendlyName = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty)
            throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId cannot be empty.", nameof(deviceId));

        Id = id;
        TrustSubjectId = trustSubjectId;
        DeviceId = deviceId;
        RegistrationMethod = registrationMethod;
        DeviceType = deviceType;
        Platform = platform;
        FriendlyName = friendlyName;
        Status = DeviceStatus.PENDING;
        FirstSeenAt = DateTime.UtcNow;
        LastSeenAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = DeviceStatus.ACTIVE;
        LastSeenAt = DateTime.UtcNow;
    }

    public void Touch(string? platform = null)
    {
        LastSeenAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(platform))
            Platform ??= platform;
    }

    public void Rename(string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
            throw new ArgumentException("FriendlyName cannot be empty.", nameof(friendlyName));
        FriendlyName = friendlyName.Trim();
    }

    public void Revoke(string? reasonCode)
    {
        if (Status == DeviceStatus.REVOKED)
            return;
        Status = DeviceStatus.REVOKED;
        RevokedAt = DateTime.UtcNow;
        RevokedReason = reasonCode;
    }

    public void MarkLost()
    {
        if (Status == DeviceStatus.REVOKED)
            return;
        Status = DeviceStatus.LOST;
    }

    public void MarkSuspicious() => Status = DeviceStatus.SUSPICIOUS;
}

/// <summary>Loại thiết bị (SRS §7.2).</summary>
public enum DeviceType
{
    Unknown = 0,
    Mobile = 1,
    Desktop = 2
}

/// <summary>Cách device được đăng ký (FR-004/FR-005).</summary>
public enum RegistrationMethod
{
    OTHER = 0,
    WEBAUTHN = 1,
    INSTALLATION_ID = 2
}

/// <summary>Status vòng đời device (SRS §7.2).</summary>
public enum DeviceStatus
{
    PENDING = 0,
    ACTIVE = 1,
    SUSPICIOUS = 2,
    LOST = 3,
    REVOKED = 4
}