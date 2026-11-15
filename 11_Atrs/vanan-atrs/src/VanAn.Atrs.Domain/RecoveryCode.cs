namespace VanAn.Atrs.Domain;

/// <summary>
/// RecoveryCode — một code dùng một lần (FR-008). Chỉ lưu HASH (BCrypt password-style),
/// không bao giờ lưu plaintext. Single-use: UsedAt set-once.
/// </summary>
public sealed class RecoveryCode
{
    public Guid Id { get; private set; }
    public Guid SetId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime? UsedAt { get; private set; }
    public Guid? UsedFromDeviceId { get; private set; }

    private RecoveryCode() { }

    public RecoveryCode(Guid id, Guid setId, string codeHash)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (setId == Guid.Empty)
            throw new ArgumentException("SetId cannot be empty.", nameof(setId));
        if (string.IsNullOrWhiteSpace(codeHash))
            throw new ArgumentException("CodeHash cannot be empty.", nameof(codeHash));
        Id = id;
        SetId = setId;
        CodeHash = codeHash;
    }

    /// <summary>Đánh dấu đã dùng — set-once (single-use, FR-008).</summary>
    public void MarkUsed(Guid? deviceId)
    {
        if (UsedAt.HasValue)
            throw new InvalidOperationException($"RecoveryCode {Id} is already used — single-use.");
        UsedAt = DateTime.UtcNow;
        UsedFromDeviceId = deviceId;
    }
}