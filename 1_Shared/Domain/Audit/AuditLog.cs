using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Audit;

/// <summary>
/// Types of audit actions tracked in the system
/// </summary>
public enum AuditActionType
{
    Create = 1,           // Táº¡o má»›i
    Update = 2,           // Cáº­p nháº­t
    Delete = 3,           // XÃ³a (soft delete)
    PeriodClose = 4,      // ÄÃ³ng ká»³ káº¿ toÃ¡n
    PeriodReopen = 5,     // Má»Ÿ láº¡i ká»³ káº¿ toÃ¡n
    Correction = 6,       // Äiá»u chá»‰nh bÃºt toÃ¡n
    Reversal = 7,         // BÃºt toÃ¡n Ä‘áº£o ngÆ°á»£c
    Export = 8,           // Xuáº¥t dá»¯ liá»‡u
    Login = 9,            // ÄÄƒng nháº­p
    Logout = 10,          // ÄÄƒng xuáº¥t
    PermissionChange = 11 // Thay Ä‘á»•i quyá»n
}

/// <summary>
/// Entity types that can be audited
/// </summary>
public enum AuditableEntityType
{
    AccountingEntry = 1,
    PeriodClosing = 2,
    Customer = 3,
    Order = 4,
    Product = 5,
    Inventory = 6,
    Shop = 7,
    User = 8,
    Tenant = 9,
    SocialCampaign = 10,
    LoyaltyRewards = 11
}

/// <summary>
/// Immutable Audit Log Entry - Append Only, Never Modified
/// Compliance requirement: Audit logs must be immutable for financial auditing
/// </summary>
public sealed class AuditLog : BaseEntity
{
    // Immutable properties - set once at creation
    public AuditActionType Action { get; private set; }
    public AuditableEntityType EntityType { get; private set; }
    public Guid EntityId { get; private set; }
    public string? OldValues { get; private set; }  // JSON serialized
    public string? NewValues { get; private set; }  // JSON serialized
    public string? Reason { get; private set; }      // For period close/reopen/correction
    public string? CorrelationId { get; private set; } // For tracking related operations
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    
    // Timestamp is from BaseEntity.CreatedAt
    public DateTime Timestamp => CreatedAt;

    // Private constructor - only factory methods can create
    private AuditLog() : base() { }

    /// <summary>
    /// Factory method: Create audit log for entity creation
    /// </summary>
    public static AuditLog ForCreate(
        TenantId tenantId,
        AuditableEntityType entityType,
        Guid entityId,
        string newValues,
        string userId,
        string? userName = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            Action = AuditActionType.Create,
            EntityType = entityType,
            EntityId = entityId,
            NewValues = newValues,
            OldValues = null,
            Reason = null,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method: Create audit log for entity update
    /// </summary>
    public static AuditLog ForUpdate(
        TenantId tenantId,
        AuditableEntityType entityType,
        Guid entityId,
        string oldValues,
        string newValues,
        string userId,
        string? userName = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            Action = AuditActionType.Update,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            Reason = null,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method: Create audit log for entity deletion (soft delete)
    /// </summary>
    public static AuditLog ForDelete(
        TenantId tenantId,
        AuditableEntityType entityType,
        Guid entityId,
        string oldValues,
        string userId,
        string? userName = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            Action = AuditActionType.Delete,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = null,
            Reason = null,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method: Create audit log for period closing
    /// </summary>
    public static AuditLog ForPeriodClose(
        TenantId tenantId,
        int year,
        int month,
        string reason,
        string userId,
        string? userName = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            Action = AuditActionType.PeriodClose,
            EntityType = AuditableEntityType.PeriodClosing,
            EntityId = Guid.NewGuid(), // Synthetic ID for period closing event
            OldValues = null,
            NewValues = $"{{\"Year\":{year},\"Month\":{month}}}",
            Reason = reason,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method: Create audit log for period reopening
    /// </summary>
    public static AuditLog ForPeriodReopen(
        TenantId tenantId,
        int year,
        int month,
        string reason,
        string userId,
        string? userName = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            Action = AuditActionType.PeriodReopen,
            EntityType = AuditableEntityType.PeriodClosing,
            EntityId = Guid.NewGuid(), // Synthetic ID for period reopening event
            OldValues = $"{{\"Year\":{year},\"Month\":{month},\"Status\":\"Closed\"}}",
            NewValues = $"{{\"Year\":{year},\"Month\":{month},\"Status\":\"Reopened\"}}",
            Reason = reason,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method: Create audit log for correction entry
    /// </summary>
    public static AuditLog ForCorrection(
        TenantId tenantId,
        Guid originalEntryId,
        string correctionReason,
        string userId,
        string? userName = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            Action = AuditActionType.Correction,
            EntityType = AuditableEntityType.AccountingEntry,
            EntityId = originalEntryId,
            OldValues = null,
            NewValues = null,
            Reason = correctionReason,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method: Create audit log for reversal entry
    /// </summary>
    public static AuditLog ForReversal(
        TenantId tenantId,
        Guid originalEntryId,
        Guid reversalEntryId,
        string reversalReason,
        string userId,
        string? userName = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            Action = AuditActionType.Reversal,
            EntityType = AuditableEntityType.AccountingEntry,
            EntityId = originalEntryId,
            OldValues = $"{{\"OriginalEntryId\":\"{originalEntryId}\"}}",
            NewValues = $"{{\"ReversalEntryId\":\"{reversalEntryId}\"}}",
            Reason = reversalReason,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Query filter options for audit logs
/// </summary>
public record AuditLogQuery
{
    public TenantId TenantId { get; init; } = null!;
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public AuditActionType? Action { get; init; }
    public AuditableEntityType? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? UserId { get; init; }
    public string? CorrelationId { get; init; }
    public string? SearchTerm { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// Paged result for audit log queries
/// </summary>
public record AuditLogPagedResult
{
    public IReadOnlyList<AuditLog> Items { get; init; } = new List<AuditLog>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
