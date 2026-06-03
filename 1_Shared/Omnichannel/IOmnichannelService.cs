namespace VanAn.Shared.Omnichannel
{
    /// <summary>
    /// Omnichannel service interface for cross-platform operations
    /// Ensures consistent user experience across mobile, web, and desktop platforms
    /// </summary>
    public interface IOmnichannelService
    {
        /// <summary>
        /// Sync user preferences across all devices
        /// </summary>
        Task SyncUserPreferencesAsync(string userId, UserPreferences preferences);

        /// <summary>
        /// Get user preferences with offline fallback
        /// </summary>
        Task<UserPreferences> GetUserPreferencesAsync(string userId);

        /// <summary>
        /// Sync order status across all devices in real-time
        /// </summary>
        Task SyncOrderStatusAsync(Guid orderId, OrderStatus status);

        /// <summary>
        /// Get real-time order updates
        /// </summary>
        Task<OrderStatus> GetOrderStatusAsync(Guid orderId);

        /// <summary>
        /// Sync inventory levels across all platforms
        /// </summary>
        Task SyncInventoryAsync(Guid productId, int quantity);

        /// <summary>
        /// Get current inventory with offline support
        /// </summary>
        Task<InventoryStatus> GetInventoryStatusAsync(Guid productId);

        /// <summary>
        /// Establish real-time connection for live updates
        /// </summary>
        Task ConnectRealtimeAsync(string userId);

        /// <summary>
        /// Disconnect real-time connection
        /// </summary>
        Task DisconnectRealtimeAsync(string userId);

        /// <summary>
        /// Handle offline mode - queue operations for later sync
        /// </summary>
        Task QueueOfflineOperationAsync(OfflineOperation operation);

        /// <summary>
        /// Process queued offline operations when connection restored
        /// </summary>
        Task ProcessOfflineQueueAsync(string userId);
    }

    /// <summary>
    /// User preferences for omnichannel experience
    /// </summary>
    public record UserPreferences
    {
        public string UserId { get; init; } = string.Empty;
        public string Language { get; init; } = "vi";
        public string Theme { get; init; } = "light";
        public string TimeZone { get; init; } = "Asia/Ho_Chi_Minh";
        public bool EnableNotifications { get; init; } = true;
        public bool EnableAutoSync { get; init; } = true;
        public Dictionary<string, object> CustomSettings { get; init; } = [];
        public DateTime LastSyncedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Order status for omnichannel tracking
    /// </summary>
    public record OrderStatus
    {
        public Guid OrderId { get; init; }
        public string Status { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
        public string UpdatedBy { get; init; } = string.Empty;
        public Dictionary<string, object> Metadata { get; init; } = [];
    }

    /// <summary>
    /// Inventory status for real-time sync
    /// </summary>
    public record InventoryStatus
    {
        public Guid ProductId { get; init; }
        public int AvailableQuantity { get; init; }
        public int ReservedQuantity { get; init; }
        public int TotalQuantity { get; init; }
        public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
        public string Location { get; init; } = string.Empty;
        public bool IsLowStock { get; init; }
    }

    /// <summary>
    /// Offline operation for queue and sync
    /// </summary>
    public record OfflineOperation
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string UserId { get; init; } = string.Empty;
        public string OperationType { get; init; } = string.Empty;
        public object Data { get; init; } = new();
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public bool IsProcessed { get; init; }
        public int RetryCount { get; init; }
    }
}
