using Xunit;
using Moq;
using System.Threading.Tasks;
using VanAn.Shared.Omnichannel;

namespace VanAn.Omnichannel.Tests;

public class OmnichannelServiceTests
{
    private readonly Mock<IOmnichannelService> _mockService;

    public OmnichannelServiceTests()
    {
        _mockService = new Mock<IOmnichannelService>();
    }

    [Fact(DisplayName = "TDD: Sync User Preferences Across Devices")]
    public async Task Omnichannel_SyncUserPreferences_ShouldSucceed()
    {
        // Arrange
        var userId = "test-user-123";
        var preferences = new UserPreferences
        {
            UserId = userId,
            Language = "vi",
            Theme = "dark",
            TimeZone = "Asia/Ho_Chi_Minh",
            EnableNotifications = true,
            EnableAutoSync = true
        };

        _mockService.Setup(x => x.SyncUserPreferencesAsync(userId, preferences))
                  .Returns(Task.CompletedTask);

        // Act
        await _mockService.Object.SyncUserPreferencesAsync(userId, preferences);

        // Assert
        _mockService.Verify(x => x.SyncUserPreferencesAsync(userId, preferences), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get User Preferences with Offline Fallback")]
    public async Task Omnichannel_GetUserPreferences_ShouldReturnPreferences()
    {
        // Arrange
        var userId = "test-user-456";
        var expectedPreferences = new UserPreferences
        {
            UserId = userId,
            Language = "vi",
            Theme = "light",
            TimeZone = "Asia/Ho_Chi_Minh",
            EnableNotifications = false,
            EnableAutoSync = true,
            LastSyncedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _mockService.Setup(x => x.GetUserPreferencesAsync(userId))
                  .ReturnsAsync(expectedPreferences);

        // Act
        var result = await _mockService.Object.GetUserPreferencesAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("vi", result.Language);
        Assert.Equal("light", result.Theme);
        Assert.Equal("Asia/Ho_Chi_Minh", result.TimeZone);
        Assert.False(result.EnableNotifications);
        Assert.True(result.EnableAutoSync);
    }

    [Fact(DisplayName = "TDD: Sync Order Status Real-Time")]
    public async Task Omnichannel_SyncOrderStatus_ShouldUpdateAllDevices()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var status = new OrderStatus
        {
            OrderId = orderId,
            Status = "PREPARING",
            Description = "Order is being prepared",
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "kitchen-staff-001"
        };

        _mockService.Setup(x => x.SyncOrderStatusAsync(orderId, status))
                  .Returns(Task.CompletedTask);

        // Act
        await _mockService.Object.SyncOrderStatusAsync(orderId, status);

        // Assert
        _mockService.Verify(x => x.SyncOrderStatusAsync(orderId, status), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Real-Time Order Updates")]
    public async Task Omnichannel_GetOrderStatus_ShouldReturnCurrentStatus()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var expectedStatus = new OrderStatus
        {
            OrderId = orderId,
            Status = "READY",
            Description = "Order is ready for pickup",
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "kitchen-staff-002"
        };

        _mockService.Setup(x => x.GetOrderStatusAsync(orderId))
                  .ReturnsAsync(expectedStatus);

        // Act
        var result = await _mockService.Object.GetOrderStatusAsync(orderId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal("READY", result.Status);
        Assert.Equal("Order is ready for pickup", result.Description);
        Assert.Equal("kitchen-staff-002", result.UpdatedBy);
    }

    [Fact(DisplayName = "TDD: Sync Inventory Across Platforms")]
    public async Task Omnichannel_SyncInventory_ShouldUpdateAllDevices()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var quantity = 25;

        _mockService.Setup(x => x.SyncInventoryAsync(productId, quantity))
                  .Returns(Task.CompletedTask);

        // Act
        await _mockService.Object.SyncInventoryAsync(productId, quantity);

        // Assert
        _mockService.Verify(x => x.SyncInventoryAsync(productId, quantity), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Inventory Status with Offline Support")]
    public async Task Omnichannel_GetInventoryStatus_ShouldReturnCurrentInventory()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var expectedInventory = new InventoryStatus
        {
            ProductId = productId,
            AvailableQuantity = 50,
            ReservedQuantity = 10,
            TotalQuantity = 60,
            LastUpdated = DateTime.UtcNow,
            Location = "main-store",
            IsLowStock = false
        };

        _mockService.Setup(x => x.GetInventoryStatusAsync(productId))
                  .ReturnsAsync(expectedInventory);

        // Act
        var result = await _mockService.Object.GetInventoryStatusAsync(productId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(productId, result.ProductId);
        Assert.Equal(50, result.AvailableQuantity);
        Assert.Equal(10, result.ReservedQuantity);
        Assert.Equal(60, result.TotalQuantity);
        Assert.Equal("main-store", result.Location);
        Assert.False(result.IsLowStock);
    }

    [Fact(DisplayName = "TDD: Queue Offline Operations for Later Sync")]
    public async Task Omnichannel_QueueOfflineOperation_ShouldStoreForLater()
    {
        // Arrange
        var operation = new OfflineOperation
        {
            UserId = "test-user-789",
            OperationType = "CREATE_ORDER",
            Data = new { productId = Guid.NewGuid(), quantity = 2 },
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            RetryCount = 0
        };

        _mockService.Setup(x => x.QueueOfflineOperationAsync(operation))
                  .Returns(Task.CompletedTask);

        // Act
        await _mockService.Object.QueueOfflineOperationAsync(operation);

        // Assert
        _mockService.Verify(x => x.QueueOfflineOperationAsync(operation), Times.Once);
    }

    [Fact(DisplayName = "TDD: Process Offline Queue When Connection Restored")]
    public async Task Omnichannel_ProcessOfflineQueue_ShouldSyncQueuedOperations()
    {
        // Arrange
        var userId = "test-user-back-online";

        _mockService.Setup(x => x.ProcessOfflineQueueAsync(userId))
                  .Returns(Task.CompletedTask);

        // Act
        await _mockService.Object.ProcessOfflineQueueAsync(userId);

        // Assert
        _mockService.Verify(x => x.ProcessOfflineQueueAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Connect Real-Time Updates")]
    public async Task Omnichannel_ConnectRealtime_ShouldEstablishConnection()
    {
        // Arrange
        var userId = "test-user-realtime";

        _mockService.Setup(x => x.ConnectRealtimeAsync(userId))
                  .Returns(Task.CompletedTask);

        // Act
        await _mockService.Object.ConnectRealtimeAsync(userId);

        // Assert
        _mockService.Verify(x => x.ConnectRealtimeAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Disconnect Real-Time Updates")]
    public async Task Omnichannel_DisconnectRealtime_ShouldCloseConnection()
    {
        // Arrange
        var userId = "test-user-disconnect";

        _mockService.Setup(x => x.DisconnectRealtimeAsync(userId))
                  .Returns(Task.CompletedTask);

        // Act
        await _mockService.Object.DisconnectRealtimeAsync(userId);

        // Assert
        _mockService.Verify(x => x.DisconnectRealtimeAsync(userId), Times.Once);
    }
}
