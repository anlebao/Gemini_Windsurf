namespace VanAn.Atrs.Domain;

/// <summary>
/// DisputeEvidenceItem — bằng chứng đính kèm dispute (FR-012, D9).
/// APPEND-ONLY: không update/delete — sai thì thêm item mới.
/// IntegrityHash = SHA-256 do uploader cung cấp (client-side hash của file);
/// SourceReference = R2 key/URL hoặc reference nội bộ (order id...).
/// KHÔNG lưu file nhị phân trong DB — chỉ metadata + hash.
/// </summary>
public sealed class DisputeEvidenceItem
{
    public Guid Id { get; private set; }
    public Guid DisputeCaseId { get; private set; }
    public DisputeEvidenceType EvidenceType { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string? SourceReference { get; private set; }
    public DateTime? CapturedAt { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public string? IntegrityHash { get; private set; }
    public string SubmittedBy { get; private set; } = string.Empty;
    public EvidenceReliabilityClass ReliabilityClass { get; private set; }
    public string? Description { get; private set; }
    public EvidenceAccessClass AccessClassification { get; private set; }

    private DisputeEvidenceItem() { }

    public DisputeEvidenceItem(
        Guid id, Guid disputeCaseId, DisputeEvidenceType evidenceType,
        string sourceSystem, string? sourceReference, DateTime? capturedAt,
        string? integrityHash, string submittedBy,
        EvidenceReliabilityClass reliabilityClass = EvidenceReliabilityClass.USER_CLAIM,
        string? description = null,
        EvidenceAccessClass accessClassification = EvidenceAccessClass.USER_VISIBLE)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (disputeCaseId == Guid.Empty) throw new ArgumentException("DisputeCaseId cannot be empty.", nameof(disputeCaseId));
        if (string.IsNullOrWhiteSpace(sourceSystem))
            throw new ArgumentException("SourceSystem cannot be empty.", nameof(sourceSystem));
        if (string.IsNullOrWhiteSpace(submittedBy))
            throw new ArgumentException("SubmittedBy cannot be empty.", nameof(submittedBy));
        if (integrityHash is not null && integrityHash.Length != 64)
            throw new ArgumentException("IntegrityHash must be SHA-256 hex (64 chars).", nameof(integrityHash));

        Id = id;
        DisputeCaseId = disputeCaseId;
        EvidenceType = evidenceType;
        SourceSystem = sourceSystem.Trim();
        SourceReference = sourceReference;
        CapturedAt = capturedAt;
        RecordedAt = DateTime.UtcNow;
        IntegrityHash = integrityHash?.ToUpperInvariant();
        SubmittedBy = submittedBy.Trim();
        ReliabilityClass = reliabilityClass;
        Description = description;
        AccessClassification = accessClassification;
    }
}
