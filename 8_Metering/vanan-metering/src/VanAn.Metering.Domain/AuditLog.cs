namespace VanAn.Metering.Domain;

/// <summary>
/// SRS NFR-5 — append-only audit trail for every admin mutation (rate card, wallet,
/// topup config…). OldJson/NewJson capture full before/after state for legal review.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string? OldJson { get; private set; }
    public string? NewJson { get; private set; }
    public DateTime AtUtc { get; private set; }

    private AuditLog() { } // EF Core

    public static AuditLog Create(
        string actor,
        string action,
        string entityType,
        Guid entityId,
        string? oldJson,
        string? newJson,
        DateTime atUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            Actor = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldJson = oldJson,
            NewJson = newJson,
            AtUtc = atUtc
        };
}
