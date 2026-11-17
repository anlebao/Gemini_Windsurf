namespace VanAn.Atrs.Domain;

/// <summary>
/// AccountNotification (ATRS-13, FR-011/FR-015) — in-app inbox cho user.
/// KHÔNG chứa secret/code/evidence: body chỉ thông báo trạng thái an toàn.
/// Append-only; ReadAt = đã đọc. Per-subject (TrustSubjectId).
/// Email channel (Brevo) = gap — phụ thuộc P4 email-first (ghi card ATRS-13).
/// </summary>
public sealed class AccountNotification
{
    public Guid Id { get; private set; }
    public Guid TrustSubjectId { get; private set; }
    /// <summary>Event type nguồn: recovery.approved / dispute.status_changed / device.revoked / …</summary>
    public string EventType { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private AccountNotification() { }

    public AccountNotification(Guid id, Guid trustSubjectId, string eventType, string title, string body, string? link = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (trustSubjectId == Guid.Empty) throw new ArgumentException("TrustSubjectId cannot be empty.", nameof(trustSubjectId));
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("EventType cannot be empty.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Body cannot be empty.", nameof(body));
        if (eventType.Length > 64) throw new ArgumentException("EventType too long.", nameof(eventType));
        if (title.Length > 200) throw new ArgumentException("Title too long.", nameof(title));
        if (body.Length > 1000) throw new ArgumentException("Body too long.", nameof(body));

        Id = id;
        TrustSubjectId = trustSubjectId;
        EventType = eventType;
        Title = title.Trim();
        Body = body.Trim();
        Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkRead()
    {
        if (ReadAt is null)
            ReadAt = DateTime.UtcNow;
    }
}