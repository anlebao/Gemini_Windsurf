namespace VanAn.Atrs.Domain;

/// <summary>
/// ConsentRecord — chứng minh đồng ý xử lý dữ liệu cho một mục đích (SRS §11.2, NFR-002).
/// Rút lại consent = set WithdrawnAt, KHÔNG xóa row (bằng chứng lịch sử).
/// </summary>
public sealed class ConsentRecord
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string PrivacyNoticeVersion { get; private set; } = string.Empty;
    public DateTime GrantedAt { get; private set; }
    public DateTime? WithdrawnAt { get; private set; }
    public string? EvidenceRef { get; private set; }

    private ConsentRecord() { }

    public ConsentRecord(Guid id, Guid trustSubjectId, string purpose, string privacyNoticeVersion, string? evidenceRef = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty)
            throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (string.IsNullOrWhiteSpace(purpose))
            throw new ArgumentException("Purpose cannot be empty.", nameof(purpose));
        if (string.IsNullOrWhiteSpace(privacyNoticeVersion))
            throw new ArgumentException("PrivacyNoticeVersion cannot be empty.", nameof(privacyNoticeVersion));
        Id = id;
        TrustSubjectId = trustSubjectId;
        Purpose = purpose;
        PrivacyNoticeVersion = privacyNoticeVersion;
        GrantedAt = DateTime.UtcNow;
        EvidenceRef = evidenceRef;
    }

    /// <summary>Rút lại đồng ý — idempotent; việc xử lý đã thực hiện hợp pháp trước đó không mất hiệu lực.</summary>
    public void Withdraw()
    {
        WithdrawnAt ??= DateTime.UtcNow;
    }
}