namespace VanAn.Atrs.Domain;

/// <summary>
/// EvidenceAttachment — file đính kèm cho DisputeCase (FR-012, D9).
/// Binary KHÔNG lưu DB — chỉ metadata + StorageKey trỏ Cloudflare R2
/// (signed URL TTL ngắn khi đọc). Ownership = TrustSubjectId của case;
/// không cho gắn attachment vào case người khác (check ở service layer).
/// </summary>
public sealed class EvidenceAttachment
{
    public const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MB

    public Guid Id { get; private set; }
    public Guid DisputeCaseId { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private EvidenceAttachment() { }

    public EvidenceAttachment(
        Guid id, Guid disputeCaseId, Guid trustSubjectId,
        string storageKey, string fileName, string contentType, long sizeBytes)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (disputeCaseId == Guid.Empty) throw new ArgumentException("DisputeCaseId cannot be empty.", nameof(disputeCaseId));
        if (trustSubjectId == Guid.Empty) throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("StorageKey cannot be empty.", nameof(storageKey));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("FileName cannot be empty.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("ContentType cannot be empty.", nameof(contentType));
        if (sizeBytes <= 0 || sizeBytes > MaxSizeBytes)
            throw new ArgumentException($"Attachment size must be 1B..{MaxSizeBytes}B.", nameof(sizeBytes));

        Id = id;
        DisputeCaseId = disputeCaseId;
        TrustSubjectId = trustSubjectId;
        StorageKey = storageKey;
        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        CreatedAt = DateTime.UtcNow;
    }
}
