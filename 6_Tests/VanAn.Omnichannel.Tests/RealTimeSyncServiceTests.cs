using Xunit;
using Moq;
using System.Threading.Tasks;
using VanAn.Shared.Omnichannel;
using VanAn.Shared.Domain;

namespace VanAn.Omnichannel.Tests;

public class RealTimeSyncServiceTests
{
    private readonly Mock<IRealTimeSyncService> _mockRealTimeSyncService;

    public RealTimeSyncServiceTests()
    {
        _mockRealTimeSyncService = new Mock<IRealTimeSyncService>();
    }

    [Fact(DisplayName = "TDD: Subscribe to Real-Time Inventory Updates")]
    public async Task RealTimeSync_SubscribeToInventory_ShouldReturnSubscriptionResult()
    {
        // Arrange
        var shopId = "shop-001";
        var deviceId = "mobile-device-123";
        var onInventoryUpdate = new Func<InventoryUpdate, Task>(update => Task.CompletedTask);

        var expectedResult = new SubscriptionResult
        {
            Success = true,
            SubscriptionId = "sub-123",
            ShopId = shopId,
            DeviceId = deviceId,
            SubscribedAt = DateTime.UtcNow,
            SubscriptionType = "Inventory",
            Latency = TimeSpan.FromMilliseconds(25)
        };

        _mockRealTimeSyncService.Setup(x => x.SubscribeToInventoryUpdatesAsync(shopId, deviceId, onInventoryUpdate))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockRealTimeSyncService.Object.SubscribeToInventoryUpdatesAsync(shopId, deviceId, onInventoryUpdate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(shopId, result.ShopId);
        Assert.Equal(deviceId, result.DeviceId);
        Assert.Equal("Inventory", result.SubscriptionType);
        Assert.True(result.Latency.TotalMilliseconds < 100);
        _mockRealTimeSyncService.Verify(x => x.SubscribeToInventoryUpdatesAsync(shopId, deviceId, onInventoryUpdate), Times.Once);
    }

    [Fact(DisplayName = "TDD: Subscribe to Real-Time Customer Updates")]
    public async Task RealTimeSync_SubscribeToCustomer_ShouldReturnSubscriptionResult()
    {
        // Arrange
        var shopId = "shop-002";
        var deviceId = "tablet-device-456";
        var onCustomerUpdate = new Func<CustomerUpdate, Task>(update => Task.CompletedTask);

        var expectedResult = new SubscriptionResult
        {
            Success = true,
            SubscriptionId = "sub-customer-789",
            ShopId = shopId,
            DeviceId = deviceId,
            SubscribedAt = DateTime.UtcNow,
            SubscriptionType = "Customer",
            Latency = TimeSpan.FromMilliseconds(30)
        };

        _mockRealTimeSyncService.Setup(x => x.SubscribeToCustomerUpdatesAsync(shopId, deviceId, onCustomerUpdate))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockRealTimeSyncService.Object.SubscribeToCustomerUpdatesAsync(shopId, deviceId, onCustomerUpdate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(shopId, result.ShopId);
        Assert.Equal(deviceId, result.DeviceId);
        Assert.Equal("Customer", result.SubscriptionType);
        Assert.True(result.Latency.TotalMilliseconds < 100);
        _mockRealTimeSyncService.Verify(x => x.SubscribeToCustomerUpdatesAsync(shopId, deviceId, onCustomerUpdate), Times.Once);
    }

    [Fact(DisplayName = "TDD: Publish Inventory Update to All Devices")]
    public async Task RealTimeSync_PublishInventoryUpdate_ShouldReachAllSubscribers()
    {
        // Arrange
        var update = new InventoryUpdate
        {
            ShopId = "shop-003",
            ProductId = new ProductId(Guid.NewGuid()),
            UpdateType = InventoryUpdateType.Sale,
            PreviousQuantity = 100,
            NewQuantity = 95,
            QuantityChange = -5,
            UpdatedBy = "staff-001",
            UpdatedByDevice = "pos-device-001",
            UpdateReason = "Order #12345",
            SyncVersion = "v1.2.3"
        };

        var publisherDeviceId = "pos-device-001";

        var expectedResult = new PublishResult
        {
            Success = true,
            UpdateId = update.UpdateId,
            TotalSubscribers = 5,
            SuccessfulDeliveries = 4,
            SuccessfulDevices = new List<string> { "mobile-001", "tablet-002", "desktop-003", "kiosk-004" },
            FailedDeliveries = new List<DeliveryError>
            {
                new DeliveryError
                {
                    DeviceId = "watch-005",
                    DeviceType = "Wearable",
                    ErrorMessage = "Device offline",
                    IsRetriable = true,
                    RetryCount = 0
                }
            },
            PublishDuration = TimeSpan.FromMilliseconds(45),
            SyncVersion = update.SyncVersion
        };

        _mockRealTimeSyncService.Setup(x => x.PublishInventoryUpdateAsync(update, publisherDeviceId))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockRealTimeSyncService.Object.PublishInventoryUpdateAsync(update, publisherDeviceId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(update.UpdateId, result.UpdateId);
        Assert.Equal(5, result.TotalSubscribers);
        Assert.Equal(4, result.SuccessfulDeliveries);
        Assert.Single(result.FailedDeliveries);
        Assert.Equal("watch-005", result.FailedDeliveries[0].DeviceId);
        Assert.True(result.PublishDuration.TotalMilliseconds < 200);
        _mockRealTimeSyncService.Verify(x => x.PublishInventoryUpdateAsync(update, publisherDeviceId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Publish Customer Update to All Devices")]
    public async Task RealTimeSync_PublishCustomerUpdate_ShouldReachAllSubscribers()
    {
        // Arrange
        var update = new CustomerUpdate
        {
            ShopId = "shop-004",
            CustomerId = new CustomerId(Guid.NewGuid()),
            UpdateType = CustomerUpdateType.LoyaltyUpdated,
            PropertyName = "LoyaltyPoints",
            PreviousValue = 150,
            NewValue = 200,
            UpdatedBy = "system-loyalty",
            UpdatedByDevice = "server-001",
            UpdateReason = "Order completed - 50 points earned",
            SyncVersion = "v2.1.0"
        };

        var publisherDeviceId = "server-001";

        var expectedResult = new PublishResult
        {
            Success = true,
            UpdateId = update.UpdateId,
            TotalSubscribers = 3,
            SuccessfulDeliveries = 3,
            SuccessfulDevices = new List<string> { "mobile-001", "desktop-002", "tablet-003" },
            FailedDeliveries = new List<DeliveryError>(),
            PublishDuration = TimeSpan.FromMilliseconds(35),
            SyncVersion = update.SyncVersion
        };

        _mockRealTimeSyncService.Setup(x => x.PublishCustomerUpdateAsync(update, publisherDeviceId))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockRealTimeSyncService.Object.PublishCustomerUpdateAsync(update, publisherDeviceId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(update.UpdateId, result.UpdateId);
        Assert.Equal(3, result.TotalSubscribers);
        Assert.Equal(3, result.SuccessfulDeliveries);
        Assert.Empty(result.FailedDeliveries);
        Assert.True(result.PublishDuration.TotalMilliseconds < 100);
        _mockRealTimeSyncService.Verify(x => x.PublishCustomerUpdateAsync(update, publisherDeviceId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Inventory Sync Status")]
    public async Task RealTimeSync_GetInventorySyncStatus_ShouldReturnCurrentStatus()
    {
        // Arrange
        var shopId = "shop-005";
        var productId = new ProductId(Guid.NewGuid());

        var expectedStatus = new InventorySyncStatus
        {
            ShopId = shopId,
            ProductId = productId,
            CurrentQuantity = 75,
            LastSyncVersion = "v3.1.2",
            LastSyncAt = DateTime.UtcNow.AddMinutes(-2),
            SyncedDevices = new List<string> { "mobile-001", "desktop-002", "tablet-003" },
            PendingDevices = new List<string> { "kiosk-004" },
            IsFullySynced = false,
            Health = SyncHealth.Good,
            AverageSyncLatency = TimeSpan.FromMilliseconds(45),
            ConflictCount = 0
        };

        _mockRealTimeSyncService.Setup(x => x.GetInventorySyncStatusAsync(shopId, productId))
                  .ReturnsAsync(expectedStatus);

        // Act
        var result = await _mockRealTimeSyncService.Object.GetInventorySyncStatusAsync(shopId, productId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(shopId, result.ShopId);
        Assert.Equal(productId, result.ProductId);
        Assert.Equal(75, result.CurrentQuantity);
        Assert.Equal("v3.1.2", result.LastSyncVersion);
        Assert.Equal(3, result.SyncedDevices.Count);
        Assert.Single(result.PendingDevices);
        Assert.False(result.IsFullySynced);
        Assert.Equal(SyncHealth.Good, result.Health);
        Assert.Equal(0, result.ConflictCount);
        _mockRealTimeSyncService.Verify(x => x.GetInventorySyncStatusAsync(shopId, productId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Customer Sync Status")]
    public async Task RealTimeSync_GetCustomerSyncStatus_ShouldReturnCurrentStatus()
    {
        // Arrange
        var shopId = "shop-006";
        var customerId = new CustomerId(Guid.NewGuid());

        var expectedStatus = new CustomerSyncStatus
        {
            ShopId = shopId,
            CustomerId = customerId,
            LastSyncVersion = "v4.0.1",
            LastSyncAt = DateTime.UtcNow.AddMinutes(-1),
            SyncedDevices = new List<string> { "mobile-001", "desktop-002" },
            PendingDevices = new List<string>(),
            IsFullySynced = true,
            Health = SyncHealth.Excellent,
            AverageSyncLatency = TimeSpan.FromMilliseconds(25),
            ConflictCount = 0,
            CurrentData = new Dictionary<string, object>
            {
                ["FullName"] = "Nguyen Van A",
                ["LoyaltyPoints"] = 250,
                ["Tier"] = "Silver"
            }
        };

        _mockRealTimeSyncService.Setup(x => x.GetCustomerSyncStatusAsync(shopId, customerId))
                  .ReturnsAsync(expectedStatus);

        // Act
        var result = await _mockRealTimeSyncService.Object.GetCustomerSyncStatusAsync(shopId, customerId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(shopId, result.ShopId);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal("v4.0.1", result.LastSyncVersion);
        Assert.Equal(2, result.SyncedDevices.Count);
        Assert.Empty(result.PendingDevices);
        Assert.True(result.IsFullySynced);
        Assert.Equal(SyncHealth.Excellent, result.Health);
        Assert.Equal(0, result.ConflictCount);
        Assert.Equal(3, result.CurrentData.Count);
        Assert.Equal("Nguyen Van A", result.CurrentData["FullName"]);
        _mockRealTimeSyncService.Verify(x => x.GetCustomerSyncStatusAsync(shopId, customerId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Force Sync Inventory Across Devices")]
    public async Task RealTimeSync_ForceSyncInventory_ShouldSyncAllDevices()
    {
        // Arrange
        var shopId = "shop-007";
        var productId = new ProductId(Guid.NewGuid());
        var requesterDeviceId = "admin-device-001";

        var expectedResult = new ForceSyncResult
        {
            Success = true,
            ShopId = shopId,
            EntityType = "Inventory",
            EntityId = productId.Value.ToString(),
            TotalDevices = 4,
            SyncedDevices = 3,
            SuccessfulDevices = new List<string> { "mobile-001", "desktop-002", "tablet-003" },
            FailedDevices = new List<SyncError>
            {
                new SyncError
                {
                    DeviceId = "kiosk-004",
                    DeviceType = "Kiosk",
                    ErrorMessage = "Device busy",
                    IsRetriable = true,
                    ErrorCode = "DEVICE_BUSY"
                }
            },
            SyncStartedAt = DateTime.UtcNow.AddSeconds(-5),
            SyncCompletedAt = DateTime.UtcNow,
            SyncDuration = TimeSpan.FromSeconds(5),
            SyncVersion = "v5.0.0",
            ResolvedConflicts = new List<RealTimeSyncConflict>()
        };

        _mockRealTimeSyncService.Setup(x => x.ForceSyncInventoryAsync(shopId, productId, requesterDeviceId))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockRealTimeSyncService.Object.ForceSyncInventoryAsync(shopId, productId, requesterDeviceId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(shopId, result.ShopId);
        Assert.Equal("Inventory", result.EntityType);
        Assert.Equal(4, result.TotalDevices);
        Assert.Equal(3, result.SyncedDevices);
        Assert.Single(result.FailedDevices);
        Assert.Equal("kiosk-004", result.FailedDevices[0].DeviceId);
        Assert.Equal("v5.0.0", result.SyncVersion);
        _mockRealTimeSyncService.Verify(x => x.ForceSyncInventoryAsync(shopId, productId, requesterDeviceId), Times.Once);
    }
    [Fact(DisplayName = "TDD: Force Sync Customer Data Across Devices")]
    public async Task RealTimeSync_ForceSyncCustomer_ShouldSyncAllDevices()
    {
        // Arrange
        var shopId = "shop-008";
        var customerId = new CustomerId(Guid.NewGuid());
        var requesterDeviceId = "admin-device-002";

        var expectedResult = new ForceSyncResult
        {
            Success = true,
            ShopId = shopId,
            EntityType = "Customer",
            EntityId = customerId.Value.ToString(),
            TotalDevices = 3,
            SyncedDevices = 3,
            SuccessfulDevices = new List<string> { "mobile-001", "desktop-002", "tablet-003" },
            FailedDevices = new List<SyncError>(),
            SyncStartedAt = DateTime.UtcNow.AddSeconds(-3),
            SyncCompletedAt = DateTime.UtcNow,
            SyncDuration = TimeSpan.FromSeconds(3),
            SyncVersion = "v5.1.0",
            ResolvedConflicts = new List<RealTimeSyncConflict>()
        };

        _mockRealTimeSyncService.Setup(x => x.ForceSyncCustomerAsync(shopId, customerId, requesterDeviceId))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockRealTimeSyncService.Object.ForceSyncCustomerAsync(shopId, customerId, requesterDeviceId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(shopId, result.ShopId);
        Assert.Equal("Customer", result.EntityType);
        Assert.Equal(3, result.TotalDevices);
        Assert.Equal(3, result.SyncedDevices);
        Assert.Empty(result.FailedDevices);
        Assert.Equal("v5.1.0", result.SyncVersion);
        _mockRealTimeSyncService.Verify(x => x.ForceSyncCustomerAsync(shopId, customerId, requesterDeviceId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Detect Real-Time Sync Conflicts")]
    public async Task RealTimeSync_DetectConflicts_ShouldIdentifyConflicts()
    {
        // Arrange
        var shopId = "shop-009";

        var expectedConflicts = new List<RealTimeSyncConflict>
        {
            new RealTimeSyncConflict
            {
                ShopId = shopId,
                EntityType = "Inventory",
                EntityId = Guid.NewGuid().ToString(),
                PropertyName = "Quantity",
                LocalValue = 50,
                RemoteValue = 45,
                LocalDeviceId = "mobile-001",
                RemoteDeviceId = "desktop-002",
                LocalTimestamp = DateTime.UtcNow.AddMinutes(-1),
                RemoteTimestamp = DateTime.UtcNow.AddSeconds(-30),
                Severity = RealTimeConflictSeverity.Medium,
                Description = "Quantity mismatch between devices",
                RequiresUserIntervention = false,
                ConflictType = "ConcurrentUpdate"
            },
            new RealTimeSyncConflict
            {
                ShopId = shopId,
                EntityType = "Customer",
                EntityId = Guid.NewGuid().ToString(),
                PropertyName = "LoyaltyPoints",
                LocalValue = 200,
                RemoteValue = 250,
                LocalDeviceId = "tablet-003",
                RemoteDeviceId = "mobile-004",
                LocalTimestamp = DateTime.UtcNow.AddMinutes(-2),
                RemoteTimestamp = DateTime.UtcNow.AddMinutes(-1),
                Severity = RealTimeConflictSeverity.High,
                Description = "Loyalty points discrepancy",
                RequiresUserIntervention = true,
                ConflictType = "DataInconsistency"
            }
        };

        _mockRealTimeSyncService.Setup(x => x.DetectSyncConflictsAsync(shopId))
                  .ReturnsAsync(expectedConflicts);

        // Act
        var result = await _mockRealTimeSyncService.Object.DetectSyncConflictsAsync(shopId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, conflict => Assert.Equal(shopId, conflict.ShopId));
        Assert.Contains(result, c => c.EntityType == "Inventory");
        Assert.Contains(result, c => c.EntityType == "Customer");
        Assert.Contains(result, c => c.Severity == RealTimeConflictSeverity.Medium);
        Assert.Contains(result, c => c.Severity == RealTimeConflictSeverity.High);
        Assert.Contains(result, c => c.RequiresUserIntervention == false);
        Assert.Contains(result, c => c.RequiresUserIntervention == true);
        _mockRealTimeSyncService.Verify(x => x.DetectSyncConflictsAsync(shopId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Resolve Real-Time Sync Conflict")]
    public async Task RealTimeSync_ResolveConflict_ShouldApplyResolution()
    {
        // Arrange
        var conflictId = "conflict-123";
        var strategy = RealTimeConflictResolutionStrategy.LastWriteWins;
        var resolverDeviceId = "admin-device-001";

        var expectedResult = new ConflictResolutionResult
        {
            Success = true,
            ConflictId = conflictId,
            ShopId = "shop-010",
            Strategy = strategy,
            ResolvedValue = 45,
            ResolvedBy = "admin-001",
            ResolvedByDevice = resolverDeviceId,
            ResolvedAt = DateTime.UtcNow,
            ResolutionDescription = "Applied LastWriteWins strategy",
            AffectedDevices = new List<string> { "mobile-001", "desktop-002" },
            AppliedSyncVersion = "v6.0.0"
        };

        _mockRealTimeSyncService.Setup(x => x.ResolveSyncConflictAsync(conflictId, strategy, resolverDeviceId))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockRealTimeSyncService.Object.ResolveSyncConflictAsync(conflictId, strategy, resolverDeviceId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(conflictId, result.ConflictId);
        Assert.Equal(strategy, result.Strategy);
        Assert.Equal(45, result.ResolvedValue);
        Assert.Equal("admin-001", result.ResolvedBy);
        Assert.Equal(resolverDeviceId, result.ResolvedByDevice);
        Assert.Equal(2, result.AffectedDevices.Count);
        Assert.Equal("v6.0.0", result.AppliedSyncVersion);
        _mockRealTimeSyncService.Verify(x => x.ResolveSyncConflictAsync(conflictId, strategy, resolverDeviceId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Real-Time Sync Analytics")]
    public async Task RealTimeSync_GetSyncAnalytics_ShouldProvideInsights()
    {
        // Arrange
        var shopId = "shop-011";
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;

        var expectedAnalytics = new RealTimeSyncAnalytics
        {
            ShopId = shopId,
            PeriodStart = from,
            PeriodEnd = to,
            TotalUpdates = 1250,
            InventoryUpdates = 800,
            CustomerUpdates = 450,
            SuccessfulDeliveries = 1180,
            FailedDeliveries = 70,
            SuccessRate = 0.944m,
            AverageLatency = TimeSpan.FromMilliseconds(35),
            MaxLatency = TimeSpan.FromMilliseconds(150),
            MinLatency = TimeSpan.FromMilliseconds(5),
            UpdatesByDevice = new Dictionary<string, int>
            {
                { "mobile-001", 500 },
                { "desktop-002", 300 },
                { "tablet-003", 200 },
                { "kiosk-004", 250 }
            },
            LatencyByDevice = new Dictionary<string, decimal>
            {
                { "mobile-001", 25.5m },
                { "desktop-002", 30.2m },
                { "tablet-003", 45.8m },
                { "kiosk-004", 55.1m }
            },
            ActiveSubscribers = 15,
            ConflictsDetected = 25,
            ConflictsResolved = 22
        };

        _mockRealTimeSyncService.Setup(x => x.GetSyncAnalyticsAsync(shopId, from, to))
                  .ReturnsAsync(expectedAnalytics);

        // Act
        var result = await _mockRealTimeSyncService.Object.GetSyncAnalyticsAsync(shopId, from, to);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(shopId, result.ShopId);
        Assert.Equal(1250, result.TotalUpdates);
        Assert.Equal(800, result.InventoryUpdates);
        Assert.Equal(450, result.CustomerUpdates);
        Assert.Equal(1180, result.SuccessfulDeliveries);
        Assert.Equal(70, result.FailedDeliveries);
        Assert.Equal(0.944m, result.SuccessRate);
        Assert.Equal(35, result.AverageLatency.TotalMilliseconds);
        Assert.Equal(4, result.UpdatesByDevice.Count);
        Assert.Equal(4, result.LatencyByDevice.Count);
        Assert.Equal(15, result.ActiveSubscribers);
        Assert.Equal(25, result.ConflictsDetected);
        Assert.Equal(22, result.ConflictsResolved);
        _mockRealTimeSyncService.Verify(x => x.GetSyncAnalyticsAsync(shopId, from, to), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Connected Devices")]
    public async Task RealTimeSync_GetConnectedDevices_ShouldReturnActiveDevices()
    {
        // Arrange
        var shopId = "shop-012";

        var expectedDevices = new List<ConnectedDevice>
        {
            new ConnectedDevice
            {
                DeviceId = "mobile-001",
                DeviceType = "Mobile",
                ShopId = shopId,
                UserId = "user-001",
                ConnectedAt = DateTime.UtcNow.AddHours(-2),
                LastActivityAt = DateTime.UtcNow.AddMinutes(-5),
                IsActive = true,
                Subscriptions = new List<string> { "Inventory", "Customer" },
                ConnectionDuration = TimeSpan.FromHours(2)
            },
            new ConnectedDevice
            {
                DeviceId = "desktop-002",
                DeviceType = "Desktop",
                ShopId = shopId,
                UserId = "user-002",
                ConnectedAt = DateTime.UtcNow.AddHours(-1),
                LastActivityAt = DateTime.UtcNow.AddMinutes(-1),
                IsActive = true,
                Subscriptions = new List<string> { "Inventory" },
                ConnectionDuration = TimeSpan.FromHours(1)
            },
            new ConnectedDevice
            {
                DeviceId = "tablet-003",
                DeviceType = "Tablet",
                ShopId = shopId,
                UserId = "user-003",
                ConnectedAt = DateTime.UtcNow.AddMinutes(-30),
                LastActivityAt = DateTime.UtcNow.AddMinutes(-2),
                IsActive = true,
                Subscriptions = new List<string> { "Customer" },
                ConnectionDuration = TimeSpan.FromMinutes(30)
            }
        };

        _mockRealTimeSyncService.Setup(x => x.GetConnectedDevicesAsync(shopId))
                  .ReturnsAsync(expectedDevices);

        // Act
        var result = await _mockRealTimeSyncService.Object.GetConnectedDevicesAsync(shopId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.All(result, device => Assert.Equal(shopId, device.ShopId));
        Assert.All(result, device => Assert.True(device.IsActive));
        Assert.Contains(result, d => d.DeviceType == "Mobile");
        Assert.Contains(result, d => d.DeviceType == "Desktop");
        Assert.Contains(result, d => d.DeviceType == "Tablet");
        Assert.Contains(result, d => d.Subscriptions.Contains("Inventory"));
        Assert.Contains(result, d => d.Subscriptions.Contains("Customer"));
        _mockRealTimeSyncService.Verify(x => x.GetConnectedDevicesAsync(shopId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Disconnect Device from Real-Time Sync")]
    public async Task RealTimeSync_DisconnectDevice_ShouldRemoveSubscriptions()
    {
        // Arrange
        var shopId = "shop-013";
        var deviceId = "mobile-001";

        var expectedResult = new DisconnectResult
        {
            Success = true,
            DeviceId = deviceId,
            ShopId = shopId,
            DisconnectedAt = DateTime.UtcNow,
            RemovedSubscriptions = new List<string> { "Inventory", "Customer" },
            Errors = new List<string>(),
            ConnectionDuration = TimeSpan.FromHours(3)
        };

        _mockRealTimeSyncService.Setup(x => x.DisconnectDeviceAsync(shopId, deviceId))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockRealTimeSyncService.Object.DisconnectDeviceAsync(shopId, deviceId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(deviceId, result.DeviceId);
        Assert.Equal(shopId, result.ShopId);
        Assert.Equal(2, result.RemovedSubscriptions.Count);
        Assert.Contains("Inventory", result.RemovedSubscriptions);
        Assert.Contains("Customer", result.RemovedSubscriptions);
        Assert.Equal(TimeSpan.FromHours(3), result.ConnectionDuration);
        _mockRealTimeSyncService.Verify(x => x.DisconnectDeviceAsync(shopId, deviceId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Sync Performance Metrics")]
    public async Task RealTimeSync_GetPerformanceMetrics_ShouldReturnDetailedMetrics()
    {
        // Arrange
        var shopId = "shop-014";

        var expectedMetrics = new SyncPerformanceMetrics
        {
            ShopId = shopId,
            GeneratedAt = DateTime.UtcNow,
            ActiveConnections = 12,
            TotalSubscriptions = 25,
            CurrentLatency = TimeSpan.FromMilliseconds(28),
            AverageLatency = TimeSpan.FromMilliseconds(35),
            P95Latency = TimeSpan.FromMilliseconds(80),
            P99Latency = TimeSpan.FromMilliseconds(120),
            MessagesPerSecond = 15.5m,
            BytesPerSecond = 2048.5m,
            QueueSize = 5,
            ErrorCount = 2,
            ErrorRate = 0.08m,
            DeviceMetrics = new Dictionary<string, DevicePerformanceMetrics>
            {
                ["mobile-001"] = new DevicePerformanceMetrics
                {
                    DeviceId = "mobile-001",
                    DeviceType = "Mobile",
                    Latency = TimeSpan.FromMilliseconds(25),
                    MessagesReceived = 150,
                    MessagesSent = 145,
                    BytesReceived = 10240,
                    BytesSent = 8192,
                    IsHealthy = true
                },
                ["desktop-002"] = new DevicePerformanceMetrics
                {
                    DeviceId = "desktop-002",
                    DeviceType = "Desktop",
                    Latency = TimeSpan.FromMilliseconds(30),
                    MessagesReceived = 200,
                    MessagesSent = 195,
                    BytesReceived = 15360,
                    BytesSent = 12288,
                    IsHealthy = true
                }
            }
        };

        _mockRealTimeSyncService.Setup(x => x.GetPerformanceMetricsAsync(shopId))
                  .ReturnsAsync(expectedMetrics);

        // Act
        var result = await _mockRealTimeSyncService.Object.GetPerformanceMetricsAsync(shopId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(shopId, result.ShopId);
        Assert.Equal(12, result.ActiveConnections);
        Assert.Equal(25, result.TotalSubscriptions);
        Assert.Equal(28, result.CurrentLatency.TotalMilliseconds);
        Assert.Equal(35, result.AverageLatency.TotalMilliseconds);
        Assert.Equal(80, result.P95Latency.TotalMilliseconds);
        Assert.Equal(120, result.P99Latency.TotalMilliseconds);
        Assert.Equal(15.5m, result.MessagesPerSecond);
        Assert.Equal(2048.5m, result.BytesPerSecond);
        Assert.Equal(5, result.QueueSize);
        Assert.Equal(2, result.ErrorCount);
        Assert.Equal(0.08m, result.ErrorRate);
        Assert.Equal(2, result.DeviceMetrics.Count);
        Assert.All(result.DeviceMetrics.Values, metrics => Assert.True(metrics.IsHealthy));
        _mockRealTimeSyncService.Verify(x => x.GetPerformanceMetricsAsync(shopId), Times.Once);
    }
}
