namespace VanAn.Atrs.Domain;

/// <summary>
/// Loại ghi chú staff trên recovery request (A16 — add note / recommend / escalate).
/// Recommendation KHÔNG phải quyết định cuối: approve/reject vẫn cần permission
/// recovery.approve (maker-checker khi policy yêu cầu).
/// </summary>
public enum RecoveryNoteType
{
    INTERNAL_NOTE = 0,
    RECOMMEND_APPROVE = 1,
    RECOMMEND_REJECT = 2,
    ESCALATION_NOTE = 3
}

/// <summary>
/// RecoveryCaseNote — internal note staff trên recovery request (A16, FR-014).
/// Append-only; KHÔNG lộ cho user (user-facing DTO chỉ expose status + reason code).
/// </summary>
public sealed class RecoveryCaseNote
{
    public Guid Id { get; private set; }
    public Guid RecoveryRequestId { get; private set; }
    /// <summary>DemoUserId của staff viết note (sub claim).</summary>
    public Guid AuthorId { get; private set; }
    public RecoveryNoteType NoteType { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private RecoveryCaseNote() { }

    public RecoveryCaseNote(Guid id, Guid recoveryRequestId, Guid authorId, RecoveryNoteType noteType, string body)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (recoveryRequestId == Guid.Empty) throw new ArgumentException("RecoveryRequestId cannot be empty.", nameof(recoveryRequestId));
        if (authorId == Guid.Empty) throw new ArgumentException("AuthorId cannot be empty.", nameof(authorId));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Body cannot be empty.", nameof(body));
        if (body.Length > 2000) throw new ArgumentException("Body exceeds 2000 characters.", nameof(body));

        Id = id;
        RecoveryRequestId = recoveryRequestId;
        AuthorId = authorId;
        NoteType = noteType;
        Body = body.Trim();
        CreatedAt = DateTime.UtcNow;
    }
}