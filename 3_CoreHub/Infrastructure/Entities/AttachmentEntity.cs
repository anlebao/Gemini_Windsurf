namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// SRS v1.2 W2/C1 (G-H3, M10 — approved 2026-10-31): đính kèm chứng từ — link file
/// (R2/Cloudinary URL) tới entity kế toán (AccountingEntry/voucher...). Trước mắt
/// field + API nội bộ; upload endpoint Phase sau (card §2.5).
/// </summary>
public class AttachmentEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }

    /// <summary>Loại entity đính kèm: "AccountingEntry" · "JournalEntry" · "Order" ...</summary>
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }

    public string FileUrl { get; private set; } = string.Empty;
    public string? FileName { get; private set; }
    public string? MimeType { get; private set; }
    public string? UploadedBy { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private AttachmentEntity() { }

    public AttachmentEntity(
        Guid tenantId,
        string entityType,
        Guid entityId,
        string fileUrl,
        string? fileName = null,
        string? mimeType = null,
        string? uploadedBy = null)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("EntityType is required.", nameof(entityType));
        if (string.IsNullOrWhiteSpace(fileUrl))
            throw new ArgumentException("FileUrl is required.", nameof(fileUrl));

        TenantId = tenantId;
        EntityType = entityType;
        EntityId = entityId;
        FileUrl = fileUrl;
        FileName = fileName;
        MimeType = mimeType;
        UploadedBy = uploadedBy;
    }
}
