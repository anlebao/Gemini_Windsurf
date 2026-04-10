namespace VanAn.Shared.Omnichannel;

/// <summary>
/// Synchronization strategy interface for multi-device data consistency
/// Provides different sync strategies based on data type and network conditions
/// </summary>
public interface ISyncStrategy
{
    /// <summary>
    /// Synchronize data using appropriate strategy
    /// </summary>
    Task<SyncResult> SynchronizeAsync(SyncRequest request);

    /// <summary>
    /// Detect conflicts between local and remote data
    /// </summary>
    Task<List<SyncConflict>> DetectConflictsAsync(string entityType, string entityId, object localData, object remoteData);

    /// <summary>
    /// Resolve conflicts using configured resolution strategy
    /// </summary>
    Task<ConflictResolution> ResolveConflictAsync(SyncConflict conflict, ConflictResolutionStrategy strategy);

    /// <summary>
    /// Calculate delta between local and remote data for efficient sync
    /// </summary>
    Task<SyncDelta> CalculateDeltaAsync(string entityType, object localData, object remoteData);

    /// <summary>
    /// Apply delta to local data
    /// </summary>
    Task<object> ApplyDeltaAsync(object targetData, SyncDelta delta);

    /// <summary>
    /// Get sync strategy for specific entity type
    /// </summary>
    Task<SyncStrategyType> GetStrategyTypeAsync(string entityType);

    /// <summary>
    /// Validate data integrity before and after sync
    /// </summary>
    Task<DataIntegrityResult> ValidateDataIntegrityAsync(string entityType, object data);
}

/// <summary>
/// Sync request containing all necessary information for synchronization
/// </summary>
public record SyncRequest
{
    public string UserId { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public object LocalData { get; init; } = new();
    public object? RemoteData { get; init; }
    public DateTime LastSyncTimestamp { get; init; } = DateTime.MinValue;
    public SyncPriority Priority { get; init; } = SyncPriority.Normal;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public string DeviceId { get; init; } = string.Empty;
}

/// <summary>
/// Sync result containing synchronization outcome
/// </summary>
public record SyncResult
{
    public bool Success { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public SyncOutcome Outcome { get; init; }
    public object? SyncedData { get; init; }
    public List<SyncConflict> ResolvedConflicts { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public DateTime SyncTimestamp { get; init; } = DateTime.UtcNow;
    public long DataSizeBytes { get; init; }
    public TimeSpan Duration { get; init; }
    public SyncStrategyType StrategyUsed { get; init; }
}

/// <summary>
/// Sync conflict between local and remote data
/// </summary>
public record SyncConflict
{
    public string ConflictId { get; init; } = Guid.NewGuid().ToString();
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string PropertyName { get; init; } = string.Empty;
    public object? LocalValue { get; init; }
    public object? RemoteValue { get; init; }
    public DateTime LocalTimestamp { get; init; }
    public DateTime RemoteTimestamp { get; init; }
    public ConflictType Type { get; init; }
    public ConflictSeverity Severity { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Conflict resolution result
/// </summary>
public record ConflictResolution
{
    public string ConflictId { get; init; } = string.Empty;
    public ConflictResolutionStrategy Strategy { get; init; }
    public object? ResolvedValue { get; init; }
    public bool RequiresUserIntervention { get; init; }
    public string? ResolutionDescription { get; init; }
    public DateTime ResolvedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Sync delta for efficient data synchronization
/// </summary>
public record SyncDelta
{
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public List<PropertyDelta> PropertyDeltas { get; init; } = new();
    public DeltaType Type { get; init; }
    public DateTime BaseTimestamp { get; init; }
    public DateTime TargetTimestamp { get; init; }
    public long DeltaSizeBytes { get; init; }
}

/// <summary>
/// Property-level delta for fine-grained synchronization
/// </summary>
public record PropertyDelta
{
    public string PropertyName { get; init; } = string.Empty;
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
    public DeltaOperation Operation { get; init; }
    public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Data integrity validation result
/// </summary>
public record DataIntegrityResult
{
    public bool IsValid { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public List<IntegrityViolation> Violations { get; init; } = new();
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;
    public string Checksum { get; init; } = string.Empty;
}

/// <summary>
/// Data integrity violation
/// </summary>
public record IntegrityViolation
{
    public string PropertyName { get; init; } = string.Empty;
    public string ViolationType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ViolationSeverity Severity { get; init; }
}

/// <summary>
/// Sync strategy types
/// </summary>
public enum SyncStrategyType
{
    FullSync,        // Complete data synchronization
    DeltaSync,       // Only changed data
    ConflictResolution, // Focus on conflict resolution
    PrioritySync,    // High-priority data first
    BatchSync,       // Batch multiple entities
    RealTimeSync     // Real-time synchronization
}

/// <summary>
/// Sync outcome types
/// </summary>
public enum SyncOutcome
{
    Success,         // Sync completed successfully
    Conflict,        // Conflicts detected and resolved
    Failure,         // Sync failed
    Partial,         // Partial sync completed
    Skipped,         // Sync skipped (no changes)
    RequiresIntervention // Requires user intervention
}

/// <summary>
/// Conflict types
/// </summary>
public enum ConflictType
{
    UpdateUpdate,    // Both local and remote updated
    DeleteUpdate,    // One deleted, other updated
    CreateCreate,    // Both created same entity
    VersionMismatch,  // Version numbers don't match
    ConstraintViolation, // Data constraint violation
    BusinessRuleConflict // Business rule conflict
}

/// <summary>
/// Conflict severity levels
/// </summary>
public enum ConflictSeverity
{
    Low,             // Minor conflict, auto-resolvable
    Medium,          // Requires attention but resolvable
    High,            // Major conflict, may need intervention
    Critical         // Critical conflict, requires immediate attention
}

/// <summary>
/// Conflict resolution strategies
/// </summary>
public enum ConflictResolutionStrategy
{
    LocalWins,       // Keep local version
    RemoteWins,      // Keep remote version
    LastWriteWins,   // Keep most recent version
    Merge,           // Attempt to merge changes
    UserChoice,      // Let user decide
    Skip,            // Skip this entity
    CreateBoth       // Keep both versions
}

/// <summary>
/// Sync priority levels
/// </summary>
public enum SyncPriority
{
    Low,             // Background sync
    Normal,          // Regular priority
    High,            // Important data
    Critical,        // Urgent sync
    RealTime         // Immediate sync
}

/// <summary>
/// Delta operation types
/// </summary>
public enum DeltaOperation
{
    Add,             // Property added
    Update,          // Property updated
    Delete,          // Property deleted
    Replace          // Property replaced
}

/// <summary>
/// Delta types
/// </summary>
public enum DeltaType
{
    Full,            // Full entity delta
    Partial,         // Partial entity delta
    Property,        // Single property delta
    Metadata         // Metadata only
}

/// <summary>
/// Violation severity levels
/// </summary>
public enum ViolationSeverity
{
    Info,            // Informational
    Warning,         // Warning
    Error,           // Error
    Critical         // Critical error
}
