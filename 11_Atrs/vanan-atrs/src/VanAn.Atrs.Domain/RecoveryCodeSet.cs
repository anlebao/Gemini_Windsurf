namespace VanAn.Atrs.Domain;

/// <summary>
/// Trạng thái bộ recovery codes (FR-008).
/// </summary>
public enum RecoveryCodeSetStatus
{
    ACTIVE = 0,
    REVOKED = 1,
    EXHAUSTED = 2
}

/// <summary>
/// RecoveryCodeSet — một bộ recovery codes dùng một lần của TrustSubject (FR-008).
/// Regenerate → bộ cũ REVOKED, bộ mới ACTIVE. Máy chủ chỉ lưu hash từng code.
/// </summary>
public sealed class RecoveryCodeSet
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public RecoveryCodeSetStatus Status { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public DateTime? RegeneratedAt { get; private set; }

    private RecoveryCodeSet() { }

    public RecoveryCodeSet(Guid id, Guid trustSubjectId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty)
            throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        Id = id;
        TrustSubjectId = trustSubjectId;
        Status = RecoveryCodeSetStatus.ACTIVE;
        GeneratedAt = DateTime.UtcNow;
    }

    /// <summary>Revoke bộ code — regenerate/security event. Idempotent.</summary>
    public void Revoke()
    {
        if (Status == RecoveryCodeSetStatus.REVOKED)
            return;
        Status = RecoveryCodeSetStatus.REVOKED;
        RegeneratedAt ??= DateTime.UtcNow;
    }

    /// <summary>Đánh dấu bộ đã dùng hết (không còn code chưa dùng).</summary>
    public void MarkExhausted()
    {
        if (Status == RecoveryCodeSetStatus.ACTIVE)
            Status = RecoveryCodeSetStatus.EXHAUSTED;
    }
}