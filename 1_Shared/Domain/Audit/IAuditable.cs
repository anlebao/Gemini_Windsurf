namespace VanAn.Shared.Domain.Audit;

/// <summary>
/// Marker interface for entities that require audit trail logging.
/// When an entity implements this, all Create/Update/Delete operations
/// should be automatically logged to AuditLog.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Gets the entity type for audit classification
    /// </summary>
    AuditableEntityType GetAuditableEntityType();
    
    /// <summary>
    /// Gets a snapshot of current values for audit logging
    /// Should return JSON-serializable representation
    /// </summary>
    string GetAuditSnapshot();
}

/// <summary>
/// Extension methods for audit functionality
/// </summary>
public static class AuditableExtensions
{
    /// <summary>
    /// Determines if an entity type should be audited
    /// </summary>
    public static bool ShouldAudit(this Type entityType)
    {
        return typeof(IAuditable).IsAssignableFrom(entityType);
    }
}
