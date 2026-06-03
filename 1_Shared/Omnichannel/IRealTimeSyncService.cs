using VanAn.Shared.Domain;

namespace VanAn.Shared.Omnichannel
{
    /// <summary>
    /// Real-time synchronization service for inventory and customer data
    /// Provides instant data synchronization across all connected devices and platforms
    /// </summary>
    public interface IRealTimeSyncService
    {
        /// <summary>
        /// Subscribe to real-time inventory updates
        /// </summary>
        Task<SubscriptionResult> SubscribeToInventoryUpdatesAsync(string shopId, string deviceId, Func<InventoryUpdate, Task> onInventoryUpdate);

        /// <summary>
        /// Subscribe to real-time customer data updates
        /// </summary>
        Task<SubscriptionResult> SubscribeToCustomerUpdatesAsync(string shopId, string deviceId, Func<CustomerUpdate, Task> onCustomerUpdate);

        /// <summary>
        /// Publish inventory update to all connected devices
        /// </summary>
        Task<PublishResult> PublishInventoryUpdateAsync(InventoryUpdate update, string publisherDeviceId);

        /// <summary>
        /// Publish customer data update to all connected devices
        /// </summary>
        Task<PublishResult> PublishCustomerUpdateAsync(CustomerUpdate update, string publisherDeviceId);

        /// <summary>
        /// Get current inventory sync status
        /// </summary>
        Task<InventorySyncStatus> GetInventorySyncStatusAsync(string shopId, ProductId productId);

        /// <summary>
        /// Get current customer data sync status
        /// </summary>
        Task<CustomerSyncStatus> GetCustomerSyncStatusAsync(string shopId, CustomerId customerId);

        /// <summary>
        /// Force sync inventory data across all devices
        /// </summary>
        Task<ForceSyncResult> ForceSyncInventoryAsync(string shopId, ProductId productId, string requesterDeviceId);

        /// <summary>
        /// Force sync customer data across all devices
        /// </summary>
        Task<ForceSyncResult> ForceSyncCustomerAsync(string shopId, CustomerId customerId, string requesterDeviceId);

        /// <summary>
        /// Detect and resolve real-time sync conflicts
        /// </summary>
        Task<List<RealTimeSyncConflict>> DetectSyncConflictsAsync(string shopId);

        /// <summary>
        /// Resolve real-time sync conflict
        /// </summary>
        Task<ConflictResolutionResult> ResolveSyncConflictAsync(string conflictId, RealTimeConflictResolutionStrategy strategy, string resolverDeviceId);

        /// <summary>
        /// Get real-time sync analytics
        /// </summary>
        Task<RealTimeSyncAnalytics> GetSyncAnalyticsAsync(string shopId, DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Get connected devices for a shop
        /// </summary>
        Task<List<ConnectedDevice>> GetConnectedDevicesAsync(string shopId);

        /// <summary>
        /// Disconnect device from real-time sync
        /// </summary>
        Task<DisconnectResult> DisconnectDeviceAsync(string shopId, string deviceId);

        /// <summary>
        /// Get sync performance metrics
        /// </summary>
        Task<SyncPerformanceMetrics> GetPerformanceMetricsAsync(string shopId);
    }

    /// <summary>
    /// Real-time inventory update
    /// </summary>
    public record InventoryUpdate
    {
        public string UpdateId { get; init; } = Guid.NewGuid().ToString();
        public string ShopId { get; init; } = string.Empty;
        public ProductId ProductId { get; init; }
        public InventoryUpdateType UpdateType { get; init; }
        public decimal PreviousQuantity { get; init; }
        public decimal NewQuantity { get; init; }
        public decimal QuantityChange { get; init; }
        public string UpdatedBy { get; init; } = string.Empty;
        public string UpdatedByDevice { get; init; } = string.Empty;
        public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
        public string UpdateReason { get; init; } = string.Empty;
        public Dictionary<string, object> Metadata { get; init; } = [];
        public string SyncVersion { get; init; } = string.Empty;
        public List<string> AffectedDevices { get; init; } = [];

        public InventoryUpdate(ProductId productId)
        {
            ProductId = productId;
        }
    }

    /// <summary>
    /// Real-time customer data update
    /// </summary>
    public record CustomerUpdate
    {
        public string UpdateId { get; init; } = Guid.NewGuid().ToString();
        public string ShopId { get; init; } = string.Empty;
        public CustomerId CustomerId { get; init; }
        public CustomerUpdateType UpdateType { get; init; }
        public string PropertyName { get; init; } = string.Empty;
        public object? PreviousValue { get; init; }
        public object? NewValue { get; init; }
        public string UpdatedBy { get; init; } = string.Empty;
        public string UpdatedByDevice { get; init; } = string.Empty;
        public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
        public string UpdateReason { get; init; } = string.Empty;
        public string SyncVersion { get; init; } = string.Empty;
        public List<string> AffectedDevices { get; init; } = [];

        public CustomerUpdate(CustomerId customerId)
        {
            CustomerId = customerId;
        }
    }

    /// <summary>
    /// Subscription result
    /// </summary>
    public record SubscriptionResult
    {
        public bool Success { get; init; }
        public string SubscriptionId { get; init; } = string.Empty;
        public string ShopId { get; init; } = string.Empty;
        public string DeviceId { get; init; } = string.Empty;
        public DateTime SubscribedAt { get; init; } = DateTime.UtcNow;
        public string SubscriptionType { get; init; } = string.Empty;
        public List<string> Errors { get; init; } = [];
        public TimeSpan Latency { get; init; }
    }

    /// <summary>
    /// Publish result
    /// </summary>
    public record PublishResult
    {
        public bool Success { get; init; }
        public string UpdateId { get; init; } = string.Empty;
        public int TotalSubscribers { get; init; }
        public int SuccessfulDeliveries { get; init; }
        public List<string> SuccessfulDevices { get; init; } = [];
        public List<DeliveryError> FailedDeliveries { get; init; } = [];
        public DateTime PublishedAt { get; init; } = DateTime.UtcNow;
        public TimeSpan PublishDuration { get; init; }
        public string SyncVersion { get; init; } = string.Empty;
    }

    /// <summary>
    /// Delivery error
    /// </summary>
    public record DeliveryError
    {
        public string DeviceId { get; init; } = string.Empty;
        public string DeviceType { get; init; } = string.Empty;
        public string ErrorMessage { get; init; } = string.Empty;
        public DateTime ErrorOccurredAt { get; init; } = DateTime.UtcNow;
        public bool IsRetriable { get; init; }
        public int RetryCount { get; init; }
    }

    /// <summary>
    /// Inventory sync status
    /// </summary>
    public record InventorySyncStatus
    {
        public string ShopId { get; init; } = string.Empty;
        public ProductId ProductId { get; init; }
        public decimal CurrentQuantity { get; init; }
        public string LastSyncVersion { get; init; } = string.Empty;
        public DateTime LastSyncAt { get; init; } = DateTime.UtcNow;
        public List<string> SyncedDevices { get; init; } = [];
        public List<string> PendingDevices { get; init; } = [];
        public bool IsFullySynced { get; init; }
        public SyncHealth Health { get; init; }
        public TimeSpan AverageSyncLatency { get; init; }
        public int ConflictCount { get; init; }

        public InventorySyncStatus()
        {
            // Parameterless constructor for backward compatibility
            ProductId = new ProductId(Guid.Empty);
        }

        public InventorySyncStatus(ProductId productId)
        {
            ProductId = productId;
        }
    }

    /// <summary>
    /// Customer sync status
    /// </summary>
    public record CustomerSyncStatus
    {
        public string ShopId { get; init; } = string.Empty;
        public CustomerId CustomerId { get; init; }
        public string LastSyncVersion { get; init; } = string.Empty;
        public DateTime LastSyncAt { get; init; } = DateTime.UtcNow;
        public List<string> SyncedDevices { get; init; } = [];
        public List<string> PendingDevices { get; init; } = [];
        public bool IsFullySynced { get; init; }
        public SyncHealth Health { get; init; }
        public TimeSpan AverageSyncLatency { get; init; }
        public int ConflictCount { get; init; }
        public Dictionary<string, object> CurrentData { get; init; } = [];

        public CustomerSyncStatus()
        {
            // Parameterless constructor for backward compatibility
            CustomerId = new CustomerId(Guid.Empty);
        }

        public CustomerSyncStatus(CustomerId customerId)
        {
            CustomerId = customerId;
        }
    }

    /// <summary>
    /// Force sync result
    /// </summary>
    public record ForceSyncResult
    {
        public bool Success { get; init; }
        public string ShopId { get; init; } = string.Empty;
        public string EntityType { get; init; } = string.Empty;
        public string EntityId { get; init; } = string.Empty;
        public int TotalDevices { get; init; }
        public int SyncedDevices { get; init; }
        public List<string> SuccessfulDevices { get; init; } = [];
        public List<SyncError> FailedDevices { get; init; } = [];
        public DateTime SyncStartedAt { get; init; } = DateTime.UtcNow;
        public DateTime SyncCompletedAt { get; init; } = DateTime.UtcNow;
        public TimeSpan SyncDuration { get; init; }
        public string SyncVersion { get; init; } = string.Empty;
        public List<RealTimeSyncConflict> ResolvedConflicts { get; init; } = [];
    }

    /// <summary>
    /// Sync error
    /// </summary>
    public record SyncError
    {
        public string DeviceId { get; init; } = string.Empty;
        public string DeviceType { get; init; } = string.Empty;
        public string ErrorMessage { get; init; } = string.Empty;
        public DateTime ErrorOccurredAt { get; init; } = DateTime.UtcNow;
        public bool IsRetriable { get; init; }
        public string ErrorCode { get; init; } = string.Empty;
    }

    /// <summary>
    /// Real-time sync conflict
    /// </summary>
    public record RealTimeSyncConflict
    {
        public string ConflictId { get; init; } = Guid.NewGuid().ToString();
        public string ShopId { get; init; } = string.Empty;
        public string EntityType { get; init; } = string.Empty;
        public string EntityId { get; init; } = string.Empty;
        public string PropertyName { get; init; } = string.Empty;
        public object? LocalValue { get; init; }
        public object? RemoteValue { get; init; }
        public string LocalDeviceId { get; init; } = string.Empty;
        public string RemoteDeviceId { get; init; } = string.Empty;
        public DateTime LocalTimestamp { get; init; }
        public DateTime RemoteTimestamp { get; init; }
        public RealTimeConflictSeverity Severity { get; init; }
        public string Description { get; init; } = string.Empty;
        public bool RequiresUserIntervention { get; init; }
        public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
        public string ConflictType { get; init; } = string.Empty;
    }

    /// <summary>
    /// Conflict resolution result
    /// </summary>
    public record ConflictResolutionResult
    {
        public bool Success { get; init; }
        public string ConflictId { get; init; } = string.Empty;
        public string ShopId { get; init; } = string.Empty;
        public RealTimeConflictResolutionStrategy Strategy { get; init; }
        public object? ResolvedValue { get; init; }
        public string ResolvedBy { get; init; } = string.Empty;
        public string ResolvedByDevice { get; init; } = string.Empty;
        public DateTime ResolvedAt { get; init; } = DateTime.UtcNow;
        public string? ResolutionDescription { get; init; } = string.Empty;
        public List<string> AffectedDevices { get; init; } = [];
        public string AppliedSyncVersion { get; init; } = string.Empty;
    }

    /// <summary>
    /// Real-time sync analytics
    /// </summary>
    public record RealTimeSyncAnalytics
    {
        public string ShopId { get; init; } = string.Empty;
        public DateTime PeriodStart { get; init; }
        public DateTime PeriodEnd { get; init; }
        public int TotalUpdates { get; init; }
        public int InventoryUpdates { get; init; }
        public int CustomerUpdates { get; init; }
        public int SuccessfulDeliveries { get; init; }
        public int FailedDeliveries { get; init; }
        public decimal SuccessRate { get; init; }
        public TimeSpan AverageLatency { get; init; }
        public TimeSpan MaxLatency { get; init; }
        public TimeSpan MinLatency { get; init; }
        public Dictionary<string, int> UpdatesByDevice { get; init; } = [];
        public Dictionary<string, decimal> LatencyByDevice { get; init; } = [];
        public List<HourlySyncMetrics> HourlyMetrics { get; init; } = [];
        public int ActiveSubscribers { get; init; }
        public int ConflictsDetected { get; init; }
        public int ConflictsResolved { get; init; }
    }

    /// <summary>
    /// Hourly sync metrics
    /// </summary>
    public record HourlySyncMetrics
    {
        public DateTime Hour { get; init; }
        public int UpdateCount { get; init; }
        public int SuccessCount { get; init; }
        public int FailureCount { get; init; }
        public TimeSpan AverageLatency { get; init; }
        public int ActiveSubscribers { get; init; }
    }

    /// <summary>
    /// Connected device
    /// </summary>
    public record ConnectedDevice
    {
        public string DeviceId { get; init; } = string.Empty;
        public string DeviceType { get; init; } = string.Empty;
        public string ShopId { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
        public DateTime LastActivityAt { get; init; } = DateTime.UtcNow;
        public bool IsActive { get; init; }
        public List<string> Subscriptions { get; init; } = [];
        public TimeSpan ConnectionDuration { get; init; }
        public Dictionary<string, object> DeviceMetadata { get; init; } = [];
    }

    /// <summary>
    /// Disconnect result
    /// </summary>
    public record DisconnectResult
    {
        public bool Success { get; init; }
        public string DeviceId { get; init; } = string.Empty;
        public string ShopId { get; init; } = string.Empty;
        public DateTime DisconnectedAt { get; init; } = DateTime.UtcNow;
        public List<string> RemovedSubscriptions { get; init; } = [];
        public List<string> Errors { get; init; } = [];
        public TimeSpan ConnectionDuration { get; init; }
    }

    /// <summary>
    /// Sync performance metrics
    /// </summary>
    public record SyncPerformanceMetrics
    {
        public string ShopId { get; init; } = string.Empty;
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
        public int ActiveConnections { get; init; }
        public int TotalSubscriptions { get; init; }
        public TimeSpan CurrentLatency { get; init; }
        public TimeSpan AverageLatency { get; init; }
        public TimeSpan P95Latency { get; init; }
        public TimeSpan P99Latency { get; init; }
        public decimal MessagesPerSecond { get; init; }
        public decimal BytesPerSecond { get; init; }
        public int QueueSize { get; init; }
        public int ErrorCount { get; init; }
        public decimal ErrorRate { get; init; }
        public Dictionary<string, DevicePerformanceMetrics> DeviceMetrics { get; init; } = [];
    }

    /// <summary>
    /// Device performance metrics
    /// </summary>
    public record DevicePerformanceMetrics
    {
        public string DeviceId { get; init; } = string.Empty;
        public string DeviceType { get; init; } = string.Empty;
        public TimeSpan Latency { get; init; }
        public int MessagesReceived { get; init; }
        public int MessagesSent { get; init; }
        public decimal BytesReceived { get; init; }
        public decimal BytesSent { get; init; }
        public DateTime LastActivityAt { get; init; } = DateTime.UtcNow;
        public bool IsHealthy { get; init; }
    }

    /// <summary>
    /// Inventory update type enum
    /// </summary>
    public enum InventoryUpdateType
    {
        StockIn,          // Stock added
        StockOut,         // Stock removed
        Adjustment,       // Manual adjustment
        Sale,            // Stock deducted due to sale
        Return,          // Stock added due to return
        Transfer,        // Stock transferred between locations
        Count,           // Stock count adjustment
        Expiry,          // Stock expired
        Damage,          // Stock damaged
        Reorder,         // Reorder point reached
        Sync             // Synchronization update
    }

    /// <summary>
    /// Customer update type enum
    /// </summary>
    public enum CustomerUpdateType
    {
        ProfileCreated,   // New customer profile created
        ProfileUpdated,   // Customer profile updated
        LoyaltyUpdated,   // Loyalty points updated
        TierChanged,      // Customer tier changed
        PreferencesUpdated, // Customer preferences updated
        ContactUpdated,    // Contact information updated
        AddressUpdated,    // Address information updated
        PaymentUpdated,     // Payment information updated
        OrderPlaced,        // New order placed
        OrderCompleted,     // Order completed
        StatusChanged,       // Customer status changed
        NotesAdded,          // Notes added to customer
        TagsUpdated,         // Customer tags updated
        Sync                 // Synchronization update
    }

    /// <summary>
    /// Sync health enum
    /// </summary>
    public enum SyncHealth
    {
        Excellent,        // All devices synced, low latency
        Good,            // Most devices synced, acceptable latency
        Fair,            // Some devices not synced, high latency
        Poor,            // Many devices not synced, very high latency
        Critical         // Critical sync issues
    }

    /// <summary>
    /// Real-time conflict severity enum
    /// </summary>
    public enum RealTimeConflictSeverity
    {
        Low,             // Low severity conflict
        Medium,          // Medium severity conflict
        High,            // High severity conflict
        Critical         // Critical conflict requiring immediate attention
    }

    /// <summary>
    /// Real-time conflict resolution strategy enum
    /// </summary>
    public enum RealTimeConflictResolutionStrategy
    {
        LocalWins,       // Keep local version
        RemoteWins,      // Keep remote version
        LastWriteWins,   // Keep most recent version
        Merge,           // Attempt to merge values
        UserChoice,      // Let user decide
        Skip,            // Skip this update
        QueueForReview,  // Queue for manual review
        AutoResolve      // Automatic resolution based on rules
    }
}
