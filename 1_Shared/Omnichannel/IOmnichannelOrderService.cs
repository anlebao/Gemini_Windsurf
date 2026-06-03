using VanAn.Shared.Domain;

namespace VanAn.Shared.Omnichannel
{
    /// <summary>
    /// Omnichannel order management service interface
    /// Provides cross-device order tracking, status synchronization, and workflow management
    /// </summary>
    public interface IOmnichannelOrderService
    {
        /// <summary>
        /// Create order with omnichannel tracking
        /// </summary>
        Task<OmnichannelOrder> CreateOrderAsync(CreateOrderRequest request, string userId, string deviceId);

        /// <summary>
        /// Get order with full omnichannel context
        /// </summary>
        Task<OmnichannelOrder?> GetOrderAsync(Guid orderId, string userId);

        /// <summary>
        /// Update order with cross-device synchronization
        /// </summary>
        Task<OmnichannelOrder> UpdateOrderAsync(UpdateOrderRequest request, string userId, string deviceId);

        /// <summary>
        /// Update order status with real-time sync
        /// </summary>
        Task<OrderStatusUpdateResult> UpdateOrderStatusAsync(Guid orderId, OrderStatusId status, string userId, string deviceId, string? comment = null);

        /// <summary>
        /// Get order history across all devices
        /// </summary>
        Task<List<OrderHistoryEntry>> GetOrderHistoryAsync(Guid orderId, string userId);

        /// <summary>
        /// Sync order status across all user devices
        /// </summary>
        Task<OrderSyncResult> SyncOrderAcrossDevicesAsync(Guid orderId, string userId);

        /// <summary>
        /// Get orders by status with device filtering
        /// </summary>
        Task<List<OmnichannelOrder>> GetOrdersByStatusAsync(OrderStatusId status, string userId, string? deviceId = null);

        /// <summary>
        /// Get active orders across all devices
        /// </summary>
        Task<List<OmnichannelOrder>> GetActiveOrdersAsync(string userId);

        /// <summary>
        /// Cancel order with conflict resolution
        /// </summary>
        Task<OrderCancellationResult> CancelOrderAsync(Guid orderId, string userId, string deviceId, string? reason = null);

        /// <summary>
        /// Get order analytics across devices
        /// </summary>
        Task<OrderAnalytics> GetOrderAnalyticsAsync(string userId, DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Detect and resolve order conflicts
        /// </summary>
        Task<List<OrderConflict>> DetectOrderConflictsAsync(Guid orderId, string userId);

        /// <summary>
        /// Resolve order conflict using specified strategy
        /// </summary>
        Task<OrderConflictResolution> ResolveOrderConflictAsync(Guid orderId, string conflictId, OrderConflictResolutionStrategy strategy, string userId);

        /// <summary>
        /// Get order workflow status
        /// </summary>
        Task<OrderWorkflowStatus> GetOrderWorkflowStatusAsync(Guid orderId, string userId);

        /// <summary>
        /// Advance order workflow to next step
        /// </summary>
        Task<WorkflowAdvanceResult> AdvanceOrderWorkflowAsync(Guid orderId, string userId, string deviceId, Dictionary<string, object>? parameters = null);
    }

    /// <summary>
    /// Omnichannel order with full cross-device context
    /// </summary>
    public record OmnichannelOrder
    {
        public Guid OrderId { get; init; }
        public CustomerId CustomerId { get; init; }
        public List<OrderItem> Items { get; init; } = [];
        public decimal SubTotal { get; init; }
        public decimal TotalVatAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public OrderStatusId Status { get; init; }
        public string StatusDescription { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
        public string CreatedBy { get; init; } = string.Empty;
        public string CreatedByDevice { get; init; } = string.Empty;
        public string LastUpdatedBy { get; init; } = string.Empty;
        public string LastUpdatedByDevice { get; init; } = string.Empty;
        public OrderPriority Priority { get; init; }
        public string PaymentMethod { get; init; } = string.Empty;
        public string PaymentStatus { get; init; } = string.Empty;
        public string DeliveryAddress { get; init; } = string.Empty;
        public DateTime? EstimatedDeliveryTime { get; init; }
        public DateTime? ActualDeliveryTime { get; init; }
        public List<string> Tags { get; init; } = [];
        public Dictionary<string, object> Metadata { get; init; } = [];
        public bool IsSyncedAcrossDevices { get; init; }
        public string SyncVersion { get; init; } = string.Empty;
        public OrderWorkflowInfo WorkflowInfo { get; init; } = new();
        public List<OrderDeviceTracking> DeviceTracking { get; init; } = [];

        public OmnichannelOrder()
        {
            // Parameterless constructor for backward compatibility
            CustomerId = new CustomerId(Guid.Empty);
            Status = new OrderStatusId(string.Empty);
        }

        public OmnichannelOrder(CustomerId customerId, OrderStatusId status)
        {
            CustomerId = customerId ?? throw new ArgumentNullException(nameof(customerId));
            Status = status;
        }
    }

    /// <summary>
    /// Order device tracking information
    /// </summary>
    public record OrderDeviceTracking
    {
        public string DeviceId { get; init; } = string.Empty;
        public string DeviceType { get; init; } = string.Empty;
        public DateTime FirstAccessedAt { get; init; } = DateTime.UtcNow;
        public DateTime LastAccessedAt { get; init; } = DateTime.UtcNow;
        public string LastAction { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public Dictionary<string, object> DeviceMetadata { get; init; } = [];
    }

    /// <summary>
    /// Order workflow information
    /// </summary>
    public record OrderWorkflowInfo
    {
        public string CurrentStep { get; init; } = string.Empty;
        public List<string> CompletedSteps { get; init; } = [];
        public List<string> PendingSteps { get; init; } = [];
        public DateTime? WorkflowStartedAt { get; init; }
        public DateTime? WorkflowCompletedAt { get; init; }
        public bool IsWorkflowComplete { get; init; }
        public Dictionary<string, object> WorkflowData { get; init; } = [];
    }

    /// <summary>
    /// Create order request
    /// </summary>
    public record CreateOrderRequest
    {
        public CustomerId CustomerId { get; init; }
        public List<OrderItem> Items { get; init; } = [];
        public string PaymentMethod { get; init; } = string.Empty;
        public string DeliveryAddress { get; init; } = string.Empty;
        public OrderPriority Priority { get; init; }
        public DateTime? EstimatedDeliveryTime { get; init; }
        public Dictionary<string, object> Metadata { get; init; } = [];
        public List<string> Tags { get; init; } = [];

        public CreateOrderRequest(CustomerId customerId)
        {
            CustomerId = customerId ?? throw new ArgumentNullException(nameof(customerId));
        }
    }

    /// <summary>
    /// Update order request
    /// </summary>
    public record UpdateOrderRequest
    {
        public Guid OrderId { get; init; }
        public List<OrderItem>? Items { get; init; }
        public string? DeliveryAddress { get; init; }
        public OrderPriority? Priority { get; init; }
        public DateTime? EstimatedDeliveryTime { get; init; }
        public Dictionary<string, object>? Metadata { get; init; }
        public List<string>? Tags { get; init; }
        public string? Comment { get; init; }
    }

    /// <summary>
    /// Order status update result
    /// </summary>
    public record OrderStatusUpdateResult
    {
        public bool Success { get; init; }
        public Guid OrderId { get; init; }
        public OrderStatusId PreviousStatus { get; init; }
        public OrderStatusId NewStatus { get; init; }
        public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
        public string UpdatedBy { get; init; } = string.Empty;
        public string UpdatedByDevice { get; init; } = string.Empty;
        public List<string> SyncedDevices { get; init; } = [];
        public List<string> Errors { get; init; } = [];
        public string? Comment { get; init; }

        public OrderStatusUpdateResult()
        {
            // Parameterless constructor for backward compatibility
            PreviousStatus = new OrderStatusId(string.Empty);
            NewStatus = new OrderStatusId(string.Empty);
        }

        public OrderStatusUpdateResult(OrderStatusId previousStatus, OrderStatusId newStatus)
        {
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
        }
    }

    /// <summary>
    /// Order sync result
    /// </summary>
    public record OrderSyncResult
    {
        public bool Success { get; init; }
        public Guid OrderId { get; init; }
        public int TotalDevices { get; init; }
        public int SyncedDevices { get; init; }
        public List<string> SuccessfulDevices { get; init; } = [];
        public List<DeviceSyncError> FailedDevices { get; init; } = [];
        public DateTime SyncStartedAt { get; init; } = DateTime.UtcNow;
        public DateTime SyncCompletedAt { get; init; } = DateTime.UtcNow;
        public TimeSpan SyncDuration { get; init; }
        public string SyncVersion { get; init; } = string.Empty;
    }

    /// <summary>
    /// Device sync error
    /// </summary>
    public record DeviceSyncError
    {
        public string DeviceId { get; init; } = string.Empty;
        public string DeviceType { get; init; } = string.Empty;
        public string ErrorMessage { get; init; } = string.Empty;
        public DateTime ErrorOccurredAt { get; init; } = DateTime.UtcNow;
        public bool IsRetriable { get; init; }
    }

    /// <summary>
    /// Order history entry
    /// </summary>
    public record OrderHistoryEntry
    {
        public Guid EntryId { get; init; } = Guid.NewGuid();
        public Guid OrderId { get; init; }
        public OrderHistoryAction Action { get; init; }
        public string Description { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public string DeviceId { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public object? PreviousValue { get; init; }
        public object? NewValue { get; init; }
        public string? Comment { get; init; }
        public Dictionary<string, object> Metadata { get; init; } = [];
    }

    /// <summary>
    /// Order cancellation result
    /// </summary>
    public record OrderCancellationResult
    {
        public bool Success { get; init; }
        public Guid OrderId { get; init; }
        public OrderStatusId PreviousStatus { get; init; }
        public OrderStatusId NewStatus { get; init; }
        public DateTime CancelledAt { get; init; } = DateTime.UtcNow;
        public string CancelledBy { get; init; } = string.Empty;
        public string CancelledByDevice { get; init; } = string.Empty;
        public string? Reason { get; init; }
        public decimal? RefundAmount { get; init; }
        public List<string> SyncedDevices { get; init; } = [];
        public List<string> Errors { get; init; } = [];
        public bool RequiresManualIntervention { get; init; }

        public OrderCancellationResult()
        {
            // Parameterless constructor for backward compatibility
            PreviousStatus = new OrderStatusId(string.Empty);
            NewStatus = new OrderStatusId(string.Empty);
        }

        public OrderCancellationResult(OrderStatusId previousStatus, OrderStatusId newStatus)
        {
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
        }
    }

    /// <summary>
    /// Order analytics
    /// </summary>
    public record OrderAnalytics
    {
        public string UserId { get; init; } = string.Empty;
        public DateTime PeriodStart { get; init; }
        public DateTime PeriodEnd { get; init; }
        public int TotalOrders { get; init; }
        public int CompletedOrders { get; init; }
        public int CancelledOrders { get; init; }
        public int PendingOrders { get; init; }
        public decimal TotalRevenue { get; init; }
        public decimal AverageOrderValue { get; init; }
        public Dictionary<OrderStatusId, int> OrdersByStatus { get; init; } = [];
        public Dictionary<string, int> OrdersByDevice { get; init; } = [];
        public Dictionary<string, decimal> RevenueByDevice { get; init; } = [];
        public List<OrderMetrics> DailyMetrics { get; init; } = [];
        public List<string> TopProducts { get; init; } = [];
        public List<string> TopCategories { get; init; } = [];
    }

    /// <summary>
    /// Order metrics for analytics
    /// </summary>
    public record OrderMetrics
    {
        public DateTime Date { get; init; }
        public int OrderCount { get; init; }
        public decimal Revenue { get; init; }
        public decimal AverageOrderValue { get; init; }
        public int UniqueCustomers { get; init; }
    }

    /// <summary>
    /// Order conflict
    /// </summary>
    public record OrderConflict
    {
        public string ConflictId { get; init; } = Guid.NewGuid().ToString();
        public Guid OrderId { get; init; }
        public string ConflictType { get; init; } = string.Empty;
        public string PropertyName { get; init; } = string.Empty;
        public object? LocalValue { get; init; }
        public object? RemoteValue { get; init; }
        public string LocalDeviceId { get; init; } = string.Empty;
        public string RemoteDeviceId { get; init; } = string.Empty;
        public DateTime LocalTimestamp { get; init; }
        public DateTime RemoteTimestamp { get; init; }
        public OrderConflictSeverity Severity { get; init; }
        public string Description { get; init; } = string.Empty;
        public bool RequiresUserIntervention { get; init; }
        public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Order conflict resolution
    /// </summary>
    public record OrderConflictResolution
    {
        public string ConflictId { get; init; } = string.Empty;
        public Guid OrderId { get; init; }
        public OrderConflictResolutionStrategy Strategy { get; init; }
        public object? ResolvedValue { get; init; }
        public bool Success { get; init; }
        public string ResolvedBy { get; init; } = string.Empty;
        public string ResolvedByDevice { get; init; } = string.Empty;
        public DateTime ResolvedAt { get; init; } = DateTime.UtcNow;
        public string? ResolutionDescription { get; init; }
        public List<string> AffectedDevices { get; init; } = [];
    }

    /// <summary>
    /// Order workflow status
    /// </summary>
    public record OrderWorkflowStatus
    {
        public Guid OrderId { get; init; }
        public string CurrentStep { get; init; } = string.Empty;
        public List<WorkflowStep> CompletedSteps { get; init; } = [];
        public List<WorkflowStep> PendingSteps { get; init; } = [];
        public WorkflowStep? CurrentStepInfo { get; init; }
        public DateTime? WorkflowStartedAt { get; init; }
        public DateTime? WorkflowCompletedAt { get; init; }
        public bool IsWorkflowComplete { get; init; }
        public decimal WorkflowProgress { get; init; }
        public List<string> AvailableActions { get; init; } = [];
    }

    /// <summary>
    /// Workflow step
    /// </summary>
    public record WorkflowStep
    {
        public string StepId { get; init; } = string.Empty;
        public string StepName { get; init; } = string.Empty;
        public string StepDescription { get; init; } = string.Empty;
        public DateTime? StartedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public bool IsCompleted { get; init; }
        public bool IsRequired { get; init; }
        public List<string> Dependencies { get; init; } = [];
        public Dictionary<string, object> StepData { get; init; } = [];
    }

    /// <summary>
    /// Workflow advance result
    /// </summary>
    public record WorkflowAdvanceResult
    {
        public bool Success { get; init; }
        public Guid OrderId { get; init; }
        public string PreviousStep { get; init; } = string.Empty;
        public string NewStep { get; init; } = string.Empty;
        public DateTime AdvancedAt { get; init; } = DateTime.UtcNow;
        public string AdvancedBy { get; init; } = string.Empty;
        public string AdvancedByDevice { get; init; } = string.Empty;
        public List<string> SyncedDevices { get; init; } = [];
        public List<string> Errors { get; init; } = [];
        public Dictionary<string, object> WorkflowData { get; init; } = [];
    }

    /// <summary>
    /// Order priority enum
    /// </summary>
    public enum OrderPriority
    {
        Low,             // Low priority
        Normal,          // Normal priority
        High,            // High priority
        Urgent,          // Urgent priority
        Critical         // Critical priority
    }

    /// <summary>
    /// Order history action enum
    /// </summary>
    public enum OrderHistoryAction
    {
        Created,         // Order created
        Updated,         // Order updated
        StatusChanged,   // Status changed
        PaymentProcessed, // Payment processed
        Cancelled,       // Order cancelled
        Refunded,        // Order refunded
        Delivered,       // Order delivered
        Returned,        // Order returned
        Synced,          // Order synced
        ConflictResolved, // Conflict resolved
        WorkflowAdvanced // Workflow advanced
    }

    /// <summary>
    /// Order conflict severity enum
    /// </summary>
    public enum OrderConflictSeverity
    {
        Low,             // Low severity
        Medium,          // Medium severity
        High,            // High severity
        Critical         // Critical severity
    }

    /// <summary>
    /// Order conflict resolution strategy enum
    /// </summary>
    public enum OrderConflictResolutionStrategy
    {
        LocalWins,       // Keep local version
        RemoteWins,      // Keep remote version
        LastWriteWins,   // Keep most recent version
        Merge,           // Attempt to merge
        UserChoice,      // Let user decide
        Skip,            // Skip this change
        CreateConflict   // Create conflict record
    }
}
