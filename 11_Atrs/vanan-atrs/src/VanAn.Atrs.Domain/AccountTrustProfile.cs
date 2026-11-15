namespace VanAn.Atrs.Domain;

/// <summary>
/// AccountTrustProfile — hồ sơ ATRS của một TrustSubject (FR-001/FR-002, §7.1).
/// 1-1 với TrustSubject. RowVersion chống ghi đè đồng thời.
/// </summary>
public sealed class AccountTrustProfile
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public TrustProfileStatus Status { get; private set; }
    public AssuranceState AssuranceState { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819:Properties should not return arrays", Justification = "EF Core concurrency token (rowversion).")]
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private AccountTrustProfile() { }

    public AccountTrustProfile(Guid id, Guid trustSubjectId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty)
            throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        Id = id;
        TrustSubjectId = trustSubjectId;
        Status = TrustProfileStatus.PENDING_SETUP; // FR-002: tạo ở PENDING_SETUP
        AssuranceState = AssuranceState.BASIC;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetStatus(TrustProfileStatus status)
    {
        if (status == Status)
            return;
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Chỉ tăng assurance state — không tự downgrade (downgrade phải qua flow có audit).</summary>
    public void ElevateAssurance(AssuranceState state)
    {
        if (state <= AssuranceState)
            return;
        AssuranceState = state;
        UpdatedAt = DateTime.UtcNow;
    }
}