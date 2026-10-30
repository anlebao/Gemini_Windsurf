namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// SRS v1.2 Phase A (R2): trạng thái posting entry→JE — tách khỏi AccountingEntry
/// (giữ immutability hard-stop). Mỗi entry tối đa 1 row; bridge upsert sau mỗi attempt;
/// retry worker đọc Failed/Pending.
/// </summary>
public class EntryPostingStatusEntity
{
    public Guid EntryId { get; private set; }          // PK — AccountingEntry.Id
    public Guid TenantId { get; private set; }
    public int Status { get; private set; }            // EntryPostingStatuses.*
    public string? Reason { get; private set; }
    public int Attempts { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private EntryPostingStatusEntity() { }

    public EntryPostingStatusEntity(Guid entryId, Guid tenantId, int status, string? reason = null)
    {
        EntryId = entryId;
        TenantId = tenantId;
        Status = status;
        Reason = reason;
        Attempts = 1;
    }

    public void Update(int status, string? reason, bool incrementAttempts)
    {
        Status = status;
        Reason = reason;
        if (incrementAttempts) Attempts++;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>EntryPostingStatusEntity.Status values.</summary>
public static class EntryPostingStatuses
{
    public const int Pending = 0;
    public const int Posted = 1;
    public const int Skipped = 2;
    public const int Failed = 3;
}
