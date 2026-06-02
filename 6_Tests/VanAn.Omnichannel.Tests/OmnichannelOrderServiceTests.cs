using Xunit;
using Moq;
using System.Threading.Tasks;
using VanAn.Shared.Omnichannel;
using VanAn.Shared.Domain;

namespace VanAn.Omnichannel.Tests;

public class OmnichannelOrderServiceTests
{
    private readonly Mock<IOmnichannelOrderService> _mockOrderService;

    public OmnichannelOrderServiceTests()
    {
        _mockOrderService = new Mock<IOmnichannelOrderService>();
    }

    [Fact(DisplayName = "TDD: Create Order with Omnichannel Tracking")]
    public async Task OmnichannelOrder_CreateOrder_ShouldReturnOrderWithTracking()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new CreateOrderRequest
        {
            CustomerId = new CustomerId(Guid.NewGuid()),
            Items = new List<OrderItem>
            {
                new OrderItem(new TenantId(Guid.NewGuid()), orderId, Guid.NewGuid(), 2, 15000)
            },
            PaymentMethod = "CASH",
            DeliveryAddress = "123 Main St, HCM",
            Priority = OrderPriority.Normal
        };

        var userId = "user-456";
        var deviceId = "mobile-001";

        var expectedOrder = new OmnichannelOrder
        {
            OrderId = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Items = request.Items,
            SubTotal = 30000,
            TotalVatAmount = 3000,
            TotalAmount = 33000,
            Status = new OrderStatusId("pending"),
            CreatedBy = userId,
            CreatedByDevice = deviceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PaymentMethod = request.PaymentMethod,
            DeliveryAddress = request.DeliveryAddress,
            Priority = request.Priority,
            PaymentStatus = "Pending",
            IsSyncedAcrossDevices = false,
            SyncVersion = "1.0.0",
            DeviceTracking = new List<OrderDeviceTracking>
            {
                new OrderDeviceTracking
                {
                    DeviceId = deviceId,
                    DeviceType = "Mobile",
                    FirstAccessedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    LastAction = "CREATE",
                    IsActive = true
                }
            }
        };

        _mockOrderService.Setup(x => x.CreateOrderAsync(request, userId, deviceId))
                  .ReturnsAsync(expectedOrder);

        // Act
        var result = await _mockOrderService.Object.CreateOrderAsync(request, userId, deviceId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.CustomerId, result.CustomerId);
        Assert.Equal(request.Items.Count, result.Items.Count);
        Assert.Equal(new OrderStatusId("pending"), result.Status);
        Assert.Equal(userId, result.CreatedBy);
        Assert.Equal(deviceId, result.CreatedByDevice);
        Assert.Single(result.DeviceTracking);
        Assert.Equal(deviceId, result.DeviceTracking[0].DeviceId);
        _mockOrderService.Verify(x => x.CreateOrderAsync(request, userId, deviceId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Order with Full Omnichannel Context")]
    public async Task OmnichannelOrder_GetOrder_ShouldReturnOrderWithContext()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = "user-789";

        var expectedOrder = new OmnichannelOrder
        {
            OrderId = orderId,
            CustomerId = new CustomerId(Guid.NewGuid()),
            Status = new OrderStatusId("preparing"),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-30),
            CreatedBy = "staff-001",
            CreatedByDevice = "desktop-001",
            LastUpdatedBy = userId,
            LastUpdatedByDevice = "mobile-002",
            DeviceTracking = new List<OrderDeviceTracking>
            {
                new OrderDeviceTracking { DeviceId = "desktop-001", DeviceType = "Desktop", IsActive = false },
                new OrderDeviceTracking { DeviceId = "mobile-002", DeviceType = "Mobile", IsActive = true }
            },
            WorkflowInfo = new OrderWorkflowInfo
            {
                CurrentStep = "PREPARING",
                CompletedSteps = new List<string> { "CONFIRMED", "PAID" },
                PendingSteps = new List<string> { "READY", "DELIVERED" },
                WorkflowStartedAt = DateTime.UtcNow.AddHours(-2),
                IsWorkflowComplete = false
            }
        };

        _mockOrderService.Setup(x => x.GetOrderAsync(orderId, userId))
                  .ReturnsAsync(expectedOrder);

        // Act
        var result = await _mockOrderService.Object.GetOrderAsync(orderId, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(new OrderStatusId("preparing"), result.Status);
        Assert.Equal(userId, result.LastUpdatedBy);
        Assert.Equal("mobile-002", result.LastUpdatedByDevice);
        Assert.Equal(2, result.DeviceTracking.Count);
        Assert.Equal("PREPARING", result.WorkflowInfo.CurrentStep);
        Assert.Equal(2, result.WorkflowInfo.CompletedSteps.Count);
        Assert.False(result.WorkflowInfo.IsWorkflowComplete);
        _mockOrderService.Verify(x => x.GetOrderAsync(orderId, userId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Update Order Status with Real-Time Sync")]
    public async Task OmnichannelOrder_UpdateOrderStatus_ShouldSyncAcrossDevices()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var newStatus = new OrderStatusId("ready");
        var userId = "staff-123";
        var deviceId = "tablet-001";
        var comment = "Order is ready for pickup";

        var expectedResult = new OrderStatusUpdateResult
        {
            Success = true,
            OrderId = orderId,
            PreviousStatus = new OrderStatusId("preparing"),
            NewStatus = newStatus,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = userId,
            UpdatedByDevice = deviceId,
            SyncedDevices = new List<string> { "mobile-001", "desktop-002", "tablet-001" },
            Comment = comment
        };

        _mockOrderService.Setup(x => x.UpdateOrderStatusAsync(orderId, newStatus, userId, deviceId, comment))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockOrderService.Object.UpdateOrderStatusAsync(orderId, newStatus, userId, deviceId, comment);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(new OrderStatusId("preparing"), result.PreviousStatus);
        Assert.Equal(newStatus, result.NewStatus);
        Assert.Equal(userId, result.UpdatedBy);
        Assert.Equal(deviceId, result.UpdatedByDevice);
        Assert.Equal(3, result.SyncedDevices.Count);
        Assert.Contains("mobile-001", result.SyncedDevices);
        Assert.Equal(comment, result.Comment);
        _mockOrderService.Verify(x => x.UpdateOrderStatusAsync(orderId, newStatus, userId, deviceId, comment), Times.Once);
    }

    [Fact(DisplayName = "TDD: Sync Order Across All User Devices")]
    public async Task OmnichannelOrder_SyncOrderAcrossDevices_ShouldUpdateAllDevices()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = "user-sync-001";

        var expectedResult = new OrderSyncResult
        {
            Success = true,
            OrderId = orderId,
            TotalDevices = 4,
            SyncedDevices = 3,
            SuccessfulDevices = new List<string> { "mobile-001", "tablet-002", "desktop-003" },
            FailedDevices = new List<DeviceSyncError>
            {
                new DeviceSyncError
                {
                    DeviceId = "watch-004",
                    DeviceType = "Wearable",
                    ErrorMessage = "Device offline",
                    IsRetriable = true
                }
            },
            SyncStartedAt = DateTime.UtcNow.AddSeconds(-5),
            SyncCompletedAt = DateTime.UtcNow,
            SyncDuration = TimeSpan.FromSeconds(5),
            SyncVersion = "2.1.0"
        };

        _mockOrderService.Setup(x => x.SyncOrderAcrossDevicesAsync(orderId, userId))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockOrderService.Object.SyncOrderAcrossDevicesAsync(orderId, userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(4, result.TotalDevices);
        Assert.Equal(3, result.SyncedDevices);
        Assert.Equal(3, result.SuccessfulDevices.Count);
        Assert.Single(result.FailedDevices);
        Assert.Equal("watch-004", result.FailedDevices[0].DeviceId);
        Assert.True(result.FailedDevices[0].IsRetriable);
        Assert.Equal("2.1.0", result.SyncVersion);
        _mockOrderService.Verify(x => x.SyncOrderAcrossDevicesAsync(orderId, userId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Order History Across All Devices")]
    public async Task OmnichannelOrder_GetOrderHistory_ShouldReturnCompleteHistory()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = "user-history-001";

        var expectedHistory = new List<OrderHistoryEntry>
        {
            new OrderHistoryEntry
            {
                OrderId = orderId,
                Action = OrderHistoryAction.Created,
                Description = "Order created",
                UserId = "customer-001",
                DeviceId = "mobile-001",
                Timestamp = DateTime.UtcNow.AddHours(-3),
                NewValue = new { Status = "pending" }
            },
            new OrderHistoryEntry
            {
                OrderId = orderId,
                Action = OrderHistoryAction.StatusChanged,
                Description = "Status changed from pending to confirmed",
                UserId = "staff-001",
                DeviceId = "desktop-001",
                Timestamp = DateTime.UtcNow.AddHours(-2),
                PreviousValue = new { Status = "pending" },
                NewValue = new { Status = "confirmed" }
            },
            new OrderHistoryEntry
            {
                OrderId = orderId,
                Action = OrderHistoryAction.Synced,
                Description = "Order synced across devices",
                UserId = "system",
                DeviceId = "cloud-sync",
                Timestamp = DateTime.UtcNow.AddMinutes(-30)
            }
        };

        _mockOrderService.Setup(x => x.GetOrderHistoryAsync(orderId, userId))
                  .ReturnsAsync(expectedHistory);

        // Act
        var result = await _mockOrderService.Object.GetOrderHistoryAsync(orderId, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.All(result, entry => Assert.Equal(orderId, entry.OrderId));
        Assert.Contains(result, entry => entry.Action == OrderHistoryAction.Created);
        Assert.Contains(result, entry => entry.Action == OrderHistoryAction.StatusChanged);
        Assert.Contains(result, entry => entry.Action == OrderHistoryAction.Synced);
        Assert.Equal("customer-001", result[0].UserId);
        Assert.Equal("mobile-001", result[0].DeviceId);
        _mockOrderService.Verify(x => x.GetOrderHistoryAsync(orderId, userId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Cancel Order with Conflict Resolution")]
    public async Task OmnichannelOrder_CancelOrder_ShouldHandleConflicts()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = "customer-cancel-001";
        var deviceId = "mobile-002";
        var reason = "Customer requested cancellation";

        var expectedResult = new OrderCancellationResult
        {
            Success = true,
            OrderId = orderId,
            PreviousStatus = new OrderStatusId("confirmed"),
            NewStatus = new OrderStatusId("cancelled"),
            CancelledAt = DateTime.UtcNow,
            CancelledBy = userId,
            CancelledByDevice = deviceId,
            Reason = reason,
            RefundAmount = 25000,
            SyncedDevices = new List<string> { "mobile-002", "desktop-001", "cloud-sync" },
            RequiresManualIntervention = false
        };

        _mockOrderService.Setup(x => x.CancelOrderAsync(orderId, userId, deviceId, reason))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockOrderService.Object.CancelOrderAsync(orderId, userId, deviceId, reason);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(new OrderStatusId("confirmed"), result.PreviousStatus);
        Assert.Equal(new OrderStatusId("cancelled"), result.NewStatus);
        Assert.Equal(userId, result.CancelledBy);
        Assert.Equal(deviceId, result.CancelledByDevice);
        Assert.Equal(reason, result.Reason);
        Assert.Equal(25000, result.RefundAmount);
        Assert.False(result.RequiresManualIntervention);
        _mockOrderService.Verify(x => x.CancelOrderAsync(orderId, userId, deviceId, reason), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Order Analytics Across Devices")]
    public async Task OmnichannelOrder_GetOrderAnalytics_ShouldProvideInsights()
    {
        // Arrange
        var userId = "user-analytics-001";
        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow;

        var expectedAnalytics = new OrderAnalytics
        {
            UserId = userId,
            PeriodStart = from,
            PeriodEnd = to,
            TotalOrders = 25,
            CompletedOrders = 20,
            CancelledOrders = 3,
            PendingOrders = 2,
            TotalRevenue = 750000,
            AverageOrderValue = 30000,
            OrdersByStatus = new Dictionary<OrderStatusId, int>
            {
                { new OrderStatusId("completed"), 20 },
                { new OrderStatusId("cancelled"), 3 },
                { new OrderStatusId("pending"), 2 }
            },
            OrdersByDevice = new Dictionary<string, int>
            {
                { "mobile-001", 15 },
                { "desktop-002", 8 },
                { "tablet-003", 2 }
            },
            RevenueByDevice = new Dictionary<string, decimal>
            {
                { "mobile-001", 450000 },
                { "desktop-002", 250000 },
                { "tablet-003", 50000 }
            },
            TopProducts = new List<string> { "Trà sữa", "Cà phê", "Bánh mì" },
            TopCategories = new List<string> { "Đồ uống", "Đồ ăn" }
        };

        _mockOrderService.Setup(x => x.GetOrderAnalyticsAsync(userId, from, to))
                  .ReturnsAsync(expectedAnalytics);

        // Act
        var result = await _mockOrderService.Object.GetOrderAnalyticsAsync(userId, from, to);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(25, result.TotalOrders);
        Assert.Equal(20, result.CompletedOrders);
        Assert.Equal(750000, result.TotalRevenue);
        Assert.Equal(30000, result.AverageOrderValue);
        Assert.Equal(3, result.OrdersByStatus.Count);
        Assert.Equal(3, result.OrdersByDevice.Count);
        Assert.Equal(3, result.RevenueByDevice.Count);
        Assert.Equal(15, result.OrdersByDevice["mobile-001"]);
        Assert.Equal(450000, result.RevenueByDevice["mobile-001"]);
        Assert.Equal(3, result.TopProducts.Count);
        Assert.Contains("Trà sữa", result.TopProducts);
        _mockOrderService.Verify(x => x.GetOrderAnalyticsAsync(userId, from, to), Times.Once);
    }

    [Fact(DisplayName = "TDD: Detect Order Conflicts")]
    public async Task OmnichannelOrder_DetectOrderConflicts_ShouldIdentifyConflicts()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = "user-conflict-001";

        var expectedConflicts = new List<OrderConflict>
        {
            new OrderConflict
            {
                OrderId = orderId,
                ConflictType = "StatusMismatch",
                PropertyName = "Status",
                LocalValue = "preparing",
                RemoteValue = "ready",
                LocalDeviceId = "mobile-001",
                RemoteDeviceId = "desktop-002",
                LocalTimestamp = DateTime.UtcNow.AddMinutes(-5),
                RemoteTimestamp = DateTime.UtcNow.AddMinutes(-2),
                Severity = OrderConflictSeverity.Medium,
                Description = "Status mismatch between devices",
                RequiresUserIntervention = false
            },
            new OrderConflict
            {
                OrderId = orderId,
                ConflictType = "DeliveryAddressMismatch",
                PropertyName = "DeliveryAddress",
                LocalValue = "123 Main St",
                RemoteValue = "456 Oak Ave",
                LocalDeviceId = "mobile-001",
                RemoteDeviceId = "tablet-003",
                LocalTimestamp = DateTime.UtcNow.AddMinutes(-10),
                RemoteTimestamp = DateTime.UtcNow.AddMinutes(-3),
                Severity = OrderConflictSeverity.High,
                Description = "Delivery address mismatch",
                RequiresUserIntervention = true
            }
        };

        _mockOrderService.Setup(x => x.DetectOrderConflictsAsync(orderId, userId))
                  .ReturnsAsync(expectedConflicts);

        // Act
        var result = await _mockOrderService.Object.DetectOrderConflictsAsync(orderId, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, conflict => Assert.Equal(orderId, conflict.OrderId));
        Assert.Contains(result, c => c.ConflictType == "StatusMismatch");
        Assert.Contains(result, c => c.ConflictType == "DeliveryAddressMismatch");
        Assert.Contains(result, c => c.Severity == OrderConflictSeverity.Medium);
        Assert.Contains(result, c => c.Severity == OrderConflictSeverity.High);
        Assert.Contains(result, c => c.RequiresUserIntervention == false);
        Assert.Contains(result, c => c.RequiresUserIntervention == true);
        _mockOrderService.Verify(x => x.DetectOrderConflictsAsync(orderId, userId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Resolve Order Conflict")]
    public async Task OmnichannelOrder_ResolveOrderConflict_ShouldApplyResolution()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var conflictId = "conflict-123";
        var strategy = OrderConflictResolutionStrategy.LastWriteWins;
        var userId = "user-resolve-001";

        var expectedResult = new OrderConflictResolution
        {
            ConflictId = conflictId,
            OrderId = orderId,
            Strategy = strategy,
            ResolvedValue = "ready",
            Success = true,
            ResolvedBy = userId,
            ResolvedByDevice = "mobile-001",
            ResolvedAt = DateTime.UtcNow,
            ResolutionDescription = "Applied LastWriteWins strategy",
            AffectedDevices = new List<string> { "mobile-001", "desktop-002", "tablet-003" }
        };

        _mockOrderService.Setup(x => x.ResolveOrderConflictAsync(orderId, conflictId, strategy, userId))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockOrderService.Object.ResolveOrderConflictAsync(orderId, conflictId, strategy, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(conflictId, result.ConflictId);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(strategy, result.Strategy);
        Assert.Equal("ready", result.ResolvedValue);
        Assert.True(result.Success);
        Assert.Equal(userId, result.ResolvedBy);
        Assert.Equal(3, result.AffectedDevices.Count);
        Assert.Contains("mobile-001", result.AffectedDevices);
        _mockOrderService.Verify(x => x.ResolveOrderConflictAsync(orderId, conflictId, strategy, userId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Order Workflow Status")]
    public async Task OmnichannelOrder_GetOrderWorkflowStatus_ShouldReturnWorkflowInfo()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = "user-workflow-001";

        var expectedStatus = new OrderWorkflowStatus
        {
            OrderId = orderId,
            CurrentStep = "PREPARING",
            CurrentStepInfo = new WorkflowStep
            {
                StepId = "PREPARING",
                StepName = "Preparing Order",
                StepDescription = "Order is being prepared by kitchen staff",
                StartedAt = DateTime.UtcNow.AddMinutes(-15),
                IsCompleted = false,
                IsRequired = true
            },
            CompletedSteps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    StepId = "CONFIRMED",
                    StepName = "Order Confirmed",
                    CompletedAt = DateTime.UtcNow.AddMinutes(-30),
                    IsCompleted = true,
                    IsRequired = true
                }
            },
            PendingSteps = new List<WorkflowStep>
            {
                new WorkflowStep { StepId = "READY", StepName = "Ready for Pickup" },
                new WorkflowStep { StepId = "DELIVERED", StepName = "Delivered" }
            },
            WorkflowStartedAt = DateTime.UtcNow.AddMinutes(-45),
            IsWorkflowComplete = false,
            WorkflowProgress = 0.33m, // 1/3 completed
            AvailableActions = new List<string> { "MARK_READY", "CANCEL" }
        };

        _mockOrderService.Setup(x => x.GetOrderWorkflowStatusAsync(orderId, userId))
                  .ReturnsAsync(expectedStatus);

        // Act
        var result = await _mockOrderService.Object.GetOrderWorkflowStatusAsync(orderId, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal("PREPARING", result.CurrentStep);
        Assert.NotNull(result.CurrentStepInfo);
        Assert.Equal("Preparing Order", result.CurrentStepInfo.StepName);
        Assert.Single(result.CompletedSteps);
        Assert.Equal(2, result.PendingSteps.Count);
        Assert.False(result.IsWorkflowComplete);
        Assert.Equal(0.33m, result.WorkflowProgress);
        Assert.Equal(2, result.AvailableActions.Count);
        Assert.Contains("MARK_READY", result.AvailableActions);
        _mockOrderService.Verify(x => x.GetOrderWorkflowStatusAsync(orderId, userId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Advance Order Workflow")]
    public async Task OmnichannelOrder_AdvanceOrderWorkflow_ShouldMoveToNextStep()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = "staff-advance-001";
        var deviceId = "tablet-workflow-001";
        var parameters = new Dictionary<string, object> { ["PreparationTime"] = 15 };

        var expectedResult = new WorkflowAdvanceResult
        {
            Success = true,
            OrderId = orderId,
            PreviousStep = "PREPARING",
            NewStep = "READY",
            AdvancedAt = DateTime.UtcNow,
            AdvancedBy = userId,
            AdvancedByDevice = deviceId,
            SyncedDevices = new List<string> { "tablet-workflow-001", "mobile-customer-002", "desktop-kitchen-003" },
            WorkflowData = parameters
        };

        _mockOrderService.Setup(x => x.AdvanceOrderWorkflowAsync(orderId, userId, deviceId, parameters))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockOrderService.Object.AdvanceOrderWorkflowAsync(orderId, userId, deviceId, parameters);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal("PREPARING", result.PreviousStep);
        Assert.Equal("READY", result.NewStep);
        Assert.Equal(userId, result.AdvancedBy);
        Assert.Equal(deviceId, result.AdvancedByDevice);
        Assert.Equal(3, result.SyncedDevices.Count);
        Assert.Equal(parameters, result.WorkflowData);
        _mockOrderService.Verify(x => x.AdvanceOrderWorkflowAsync(orderId, userId, deviceId, parameters), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Active Orders Across Devices")]
    public async Task OmnichannelOrder_GetActiveOrders_ShouldReturnNonCompletedOrders()
    {
        // Arrange
        var userId = "user-active-001";

        var expectedOrders = new List<OmnichannelOrder>
        {
            new OmnichannelOrder
            {
                OrderId = Guid.NewGuid(),
                CustomerId = new CustomerId(Guid.NewGuid()),
                Status = new OrderStatusId("preparing"),
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                DeviceTracking = new List<OrderDeviceTracking>
                {
                    new OrderDeviceTracking { DeviceId = "mobile-001", IsActive = true }
                }
            },
            new OmnichannelOrder
            {
                OrderId = Guid.NewGuid(),
                CustomerId = new CustomerId(Guid.NewGuid()),
                Status = new OrderStatusId("ready"),
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                DeviceTracking = new List<OrderDeviceTracking>
                {
                    new OrderDeviceTracking { DeviceId = "desktop-002", IsActive = true }
                }
            }
        };

        _mockOrderService.Setup(x => x.GetActiveOrdersAsync(userId))
                  .ReturnsAsync(expectedOrders);

        // Act
        var result = await _mockOrderService.Object.GetActiveOrdersAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, order => Assert.True(order.Status.Value != "completed" && order.Status.Value != "cancelled"));
        Assert.Contains(result, order => order.Status.Value == "preparing");
        Assert.Contains(result, order => order.Status.Value == "ready");
        _mockOrderService.Verify(x => x.GetActiveOrdersAsync(userId), Times.Once);
    }
}
