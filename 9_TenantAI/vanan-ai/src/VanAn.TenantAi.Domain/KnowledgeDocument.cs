namespace VanAn.TenantAi.Domain;

/// <summary>SRS v1.1 [G5] — retrieval visibility class. Public chat NEVER retrieves non-Public docs.</summary>
public enum DocumentClassification
{
    Public,
    Internal,
    Confidential,
    Restricted
}

/// <summary>SRS v1.1 [G14] — approval gate: public retrieval reads Approved only.</summary>
public enum ApprovalStatus
{
    PendingReview,
    Approved,
    Rejected
}

/// <summary>Row lifecycle (soft delete keeps audit trail).</summary>
public enum DocumentStatus
{
    Active,
    Deleted
}

/// <summary>
/// SRS v1.1 §7 — curated knowledge document (faq | story | policy | menu-note | recipe).
/// Ingest lands as PendingReview [G14]; content is chunked + embedded only after approve.
/// Content column stored here so approval-time chunking has the source text.
/// </summary>
public sealed class KnowledgeDocument
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DocumentClassification Classification { get; private set; }
    public ApprovalStatus ApprovalStatus { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public DocumentStatus Status { get; private set; }

    /// <summary>T3 — ingest sanitizer stripped instruction-like lines from Content.</summary>
    public bool Sanitized { get; private set; }

    public string? SourceRef { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private KnowledgeDocument() { } // EF Core

    public static KnowledgeDocument Create(
        Guid tenantId,
        string type,
        string title,
        string content,
        DocumentClassification classification,
        string? sourceRef = null,
        bool sanitized = false)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Type = type,
            Title = title,
            Content = content,
            Classification = classification,
            ApprovalStatus = ApprovalStatus.PendingReview,
            Status = DocumentStatus.Active,
            Sanitized = sanitized,
            SourceRef = sourceRef,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    /// <summary>
    /// AI-P11 — owner chọn visibility lúc approve. Chỉ đổi được khi PendingReview;
    /// Restricted fail-closed [G5]; sau approve classification immutable.
    /// </summary>
    public void SetClassification(DocumentClassification classification)
    {
        if (ApprovalStatus != ApprovalStatus.PendingReview)
            throw new InvalidOperationException("Classification chỉ đổi được khi pending_review.");
        if (classification == DocumentClassification.Restricted)
            throw new ArgumentException("Restricted không được phép.", nameof(classification));
        Classification = classification;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve(string approvedBy)
    {
        ApprovalStatus = ApprovalStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAtUtc = DateTime.UtcNow;
        RejectionReason = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject(string rejectedBy, string reason)
    {
        ApprovalStatus = ApprovalStatus.Rejected;
        ApprovedBy = rejectedBy;
        ApprovedAtUtc = DateTime.UtcNow;
        RejectionReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkDeleted()
    {
        Status = DocumentStatus.Deleted;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
