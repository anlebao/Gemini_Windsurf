namespace VanAn.Shared.Omnichannel
{
    /// <summary>
    /// Data versioning and audit trail interface for omnichannel synchronization
    /// Provides comprehensive version tracking and change history
    /// </summary>
    public interface IDataVersioning
    {
        /// <summary>
        /// Create new version of entity data
        /// </summary>
        Task<DataVersion> CreateVersionAsync(string entityType, string entityId, object data, string userId, string deviceId);

        /// <summary>
        /// Get current version of entity
        /// </summary>
        Task<DataVersion?> GetCurrentVersionAsync(string entityType, string entityId);

        /// <summary>
        /// Get version history for entity
        /// </summary>
        Task<List<DataVersion>> GetVersionHistoryAsync(string entityType, string entityId, int maxVersions = 50);

        /// <summary>
        /// Compare two versions and return differences
        /// </summary>
        Task<VersionComparison> CompareVersionsAsync(string entityType, string entityId, string versionId1, string versionId2);

        /// <summary>
        /// Revert entity to specific version
        /// </summary>
        Task<RevertResult> RevertToVersionAsync(string entityType, string entityId, string versionId, string userId);

        /// <summary>
        /// Get audit trail for entity
        /// </summary>
        Task<List<AuditEntry>> GetAuditTrailAsync(string entityType, string entityId, DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Create audit entry for entity operation
        /// </summary>
        Task<AuditEntry> CreateAuditEntryAsync(string entityType, string entityId, AuditOperation operation, string userId, string deviceId, object? changes = null);

        /// <summary>
        /// Validate version integrity
        /// </summary>
        Task<VersionIntegrityResult> ValidateVersionIntegrityAsync(string entityType, string entityId);

        /// <summary>
        /// Cleanup old versions based on retention policy
        /// </summary>
        Task<CleanupResult> CleanupOldVersionsAsync(string entityType, TimeSpan retentionPeriod);
    }

    /// <summary>
    /// Data version containing complete state information
    /// </summary>
    public record DataVersion
    {
        public string VersionId { get; init; } = Guid.NewGuid().ToString();
        public string EntityType { get; init; } = string.Empty;
        public string EntityId { get; init; } = string.Empty;
        public int VersionNumber { get; init; }
        public object Data { get; init; } = new();
        public string DataHash { get; init; } = string.Empty;
        public string CreatedBy { get; init; } = string.Empty;
        public string CreatedByDevice { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public string ParentVersionId { get; init; } = string.Empty;
        public VersionType Type { get; init; }
        public string? Comment { get; init; }
        public Dictionary<string, object> Metadata { get; init; } = [];
        public long DataSizeBytes { get; init; }
        public bool IsDeleted { get; init; }
        public List<string> Tags { get; init; } = [];
    }

    /// <summary>
    /// Version comparison result
    /// </summary>
    public record VersionComparison
    {
        public string EntityType { get; init; } = string.Empty;
        public string EntityId { get; init; } = string.Empty;
        public string VersionId1 { get; init; } = string.Empty;
        public string VersionId2 { get; init; } = string.Empty;
        public List<PropertyDifference> Differences { get; init; } = [];
        public ComparisonResult Result { get; init; }
        public DateTime ComparedAt { get; init; } = DateTime.UtcNow;
        public string ComparedBy { get; init; } = string.Empty;
    }

    /// <summary>
    /// Property difference between versions
    /// </summary>
    public record PropertyDifference
    {
        public string PropertyName { get; init; } = string.Empty;
        public object? Value1 { get; init; }
        public object? Value2 { get; init; }
        public DifferenceType Type { get; init; }
        public string? Description { get; init; }
        public bool IsSignificant { get; init; }
    }

    /// <summary>
    /// Revert operation result
    /// </summary>
    public record RevertResult
    {
        public bool Success { get; init; }
        public string EntityType { get; init; } = string.Empty;
        public string EntityId { get; init; } = string.Empty;
        public string FromVersionId { get; init; } = string.Empty;
        public string ToVersionId { get; init; } = string.Empty;
        public DataVersion? NewVersion { get; init; }
        public List<string> Warnings { get; init; } = [];
        public string? ErrorMessage { get; init; }
        public DateTime RevertedAt { get; init; } = DateTime.UtcNow;
        public string RevertedBy { get; init; } = string.Empty;
    }

    /// <summary>
    /// Audit entry for tracking entity operations
    /// </summary>
    public record AuditEntry
    {
        public string EntryId { get; init; } = Guid.NewGuid().ToString();
        public string EntityType { get; init; } = string.Empty;
        public string EntityId { get; init; } = string.Empty;
        public AuditOperation Operation { get; init; }
        public string UserId { get; init; } = string.Empty;
        public string DeviceId { get; init; } = string.Empty;
        public string? VersionId { get; init; }
        public object? Changes { get; init; }
        public string? Description { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string IpAddress { get; init; } = string.Empty;
        public string UserAgent { get; init; } = string.Empty;
        public AuditSeverity Severity { get; init; }
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public Dictionary<string, object> Context { get; init; } = [];
    }

    /// <summary>
    /// Version integrity validation result
    /// </summary>
    public record VersionIntegrityResult
    {
        public bool IsValid { get; init; }
        public string EntityType { get; init; } = string.Empty;
        public string EntityId { get; init; } = string.Empty;
        public List<IntegrityIssue> Issues { get; init; } = [];
        public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;
        public string ValidatedBy { get; init; } = string.Empty;
        public int TotalVersions { get; init; }
        public int CorruptedVersions { get; init; }
    }

    /// <summary>
    /// Version integrity issue
    /// </summary>
    public record IntegrityIssue
    {
        public string VersionId { get; init; } = string.Empty;
        public string IssueType { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public IssueSeverity Severity { get; init; }
        public bool CanAutoFix { get; init; }
        public string? FixDescription { get; init; }
    }

    /// <summary>
    /// Cleanup operation result
    /// </summary>
    public record CleanupResult
    {
        public bool Success { get; init; }
        public string EntityType { get; init; } = string.Empty;
        public TimeSpan RetentionPeriod { get; init; }
        public int VersionsBeforeCleanup { get; init; }
        public int VersionsAfterCleanup { get; init; }
        public int VersionsDeleted { get; init; }
        public long SpaceSavedBytes { get; init; }
        public List<string> Errors { get; init; } = [];
        public DateTime CleanedAt { get; init; } = DateTime.UtcNow;
        public string CleanedBy { get; init; } = string.Empty;
    }

    /// <summary>
    /// Version types
    /// </summary>
    public enum VersionType
    {
        Create,          // Initial creation
        Update,          // Regular update
        Delete,          // Soft delete
        Restore,         // Restore from deletion
        Merge,           // Merge operation
        Conflict,        // Conflict resolution
        Revert,          // Revert operation
        Import,          // Import operation
        Export,          // Export operation
        System           // System-generated version
    }

    /// <summary>
    /// Comparison result types
    /// </summary>
    public enum ComparisonResult
    {
        Identical,       // No differences found
        MinorChanges,    // Minor differences
        MajorChanges,    // Significant differences
        CompletelyDifferent, // Completely different
        Error            // Error during comparison
    }

    /// <summary>
    /// Difference types between versions
    /// </summary>
    public enum DifferenceType
    {
        Added,           // Property added
        Removed,         // Property removed
        Modified,        // Property modified
        TypeChanged,     // Property type changed
        Moved,           // Property moved
        Copied           // Property copied
    }

    /// <summary>
    /// Audit operation types
    /// </summary>
    public enum AuditOperation
    {
        Create,          // Entity created
        Read,            // Entity read
        Update,          // Entity updated
        Delete,          // Entity deleted
        Restore,         // Entity restored
        Export,          // Entity exported
        Import,          // Entity imported
        Sync,            // Sync operation
        Merge,           // Merge operation
        Revert,          // Revert operation
        Backup,          // Backup operation
        RestoreBackup,    // Restore from backup
        Access,          // Access attempt
        Login,           // User login
        Logout,          // User logout
        PermissionChange, // Permission changed
        ConfigurationChange // Configuration changed
    }

    /// <summary>
    /// Audit severity levels
    /// </summary>
    public enum AuditSeverity
    {
        Info,            // Informational
        Low,             // Low importance
        Medium,          // Medium importance
        High,            // High importance
        Critical         // Critical importance
    }

    /// <summary>
    /// Issue severity levels
    /// </summary>
    public enum IssueSeverity
    {
        Info,            // Informational
        Warning,         // Warning
        Error,           // Error
        Critical         // Critical error
    }
}
