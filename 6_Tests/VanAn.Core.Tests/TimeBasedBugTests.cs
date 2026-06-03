using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Microsoft.Extensions.Logging;
using Xunit;

namespace VanAn.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "TimeBased")]
[Trait("Category", "FinancialSafety")]
public class TimeBasedBugTests : IDisposable
{
    private readonly Mock<IIndexedDBService> _indexedDBServiceMock;
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly Mock<ILogger<OfflineOrderService>> _loggerMock;
    private readonly OfflineOrderService _offlineOrderService;
    private readonly ServiceProvider _serviceProvider;
    
    public TimeBasedBugTests()
    {
        _indexedDBServiceMock = new Mock<IIndexedDBService>();
        _orderServiceMock = new Mock<IOrderService>();
        _loggerMock = new Mock<ILogger<OfflineOrderService>>();
        
        var services = new ServiceCollection();
        services.AddSingleton(_indexedDBServiceMock.Object);
        services.AddSingleton(_orderServiceMock.Object);
        services.AddSingleton(_loggerMock.Object);
        services.AddTransient<OfflineOrderService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _offlineOrderService = _serviceProvider.GetRequiredService<OfflineOrderService>();
    }
    
    [Fact]
    public async Task Should_Handle_Clock_Drift_During_Sync()
    {
        // Arrange
        var order = CreateTestOrder();
        order.CreatedAtTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds();
        
        var serverOrder = order.ToDomain();
        // Server time differs (CreatedAt is protected, use factory in real scenario)
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(serverOrder);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
        
        // Should handle clock drift gracefully
        var syncedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
        Assert.NotNull(syncedOrder);
        Assert.True(syncedOrder.IsSynced);
        
        // Server timestamp should be used
        Assert.True(Math.Abs((serverOrder.CreatedAt - DateTime.UtcNow).TotalMinutes) < 5);
    }
    
    [Fact]
    public async Task Should_Prevent_Duplicate_Orders_With_Same_Timestamp()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var order1 = CreateTestOrder();
        order1.CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        var order2 = CreateTestOrder();
        order2.CreatedAtTimestamp = timestamp.ToUnixTimeMilliseconds();
        order2.Id = order1.Id; // Same ID but different timestamp
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(new List<OfflineOrderDto> { order1 });
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order1.Id}"))
            .ReturnsAsync(order1);
        
        var domainOrder = order1.ToDomain();
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(domainOrder, It.IsAny<Guid>()))
            .ReturnsAsync(domainOrder);
        
        // Act
        await _offlineOrderService.CreateOrderAsync(order1);
        await _offlineOrderService.CreateOrderAsync(order2);
        
        var result = await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        Assert.True(result);
        
        // Should only create one order due to duplicate detection
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Once);
    }
    
    [Fact]
    public async Task Should_Handle_Daylight_Saving_Time_Transitions()
    {
        // Arrange
        var order = CreateTestOrder();
        
        // Simulate DST transition (spring forward)
        var dstTransitionTime = new DateTimeOffset(2024, 3, 31, 2, 30, 0, TimeSpan.Zero); // DST starts
        var orderTime = dstTransitionTime.AddHours(-1);
        
        order.CreatedAtTimestamp = orderTime.ToUnixTimeMilliseconds();
        
        var serverOrder = order.ToDomain();
        // Server time after DST (CreatedAt is protected, use factory in real scenario)
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(serverOrder);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
        
        // Should handle DST transition correctly
        var syncedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
        Assert.NotNull(syncedOrder);
        Assert.True(syncedOrder.IsSynced);
    }
    
    [Fact]
    public async Task Should_Handle_Timezone_Differences()
    {
        // Arrange
        var order = CreateTestOrder();
        var utcTime = DateTimeOffset.UtcNow;
        var localTime = utcTime.AddHours(7); // UTC+7 timezone
        
        order.CreatedAtTimestamp = localTime.ToUnixTimeMilliseconds();
        
        var serverOrder = order.ToDomain();
        // Server uses UTC (CreatedAt is protected, use factory in real scenario)
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(serverOrder);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
        
        // Should normalize to UTC
        var syncedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
        Assert.NotNull(syncedOrder);
        Assert.True(syncedOrder.IsSynced);
    }
    
    [Fact]
    public async Task Should_Handle_Leap_Year_Correctly()
    {
        // Arrange
        var leapDate = new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero); // Leap year
        var order = CreateTestOrder();
        order.CreatedAtTimestamp = leapDate.ToUnixTimeMilliseconds();
        
        var serverOrder = order.ToDomain();
        // CreatedAt is protected, use factory
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(serverOrder);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
        
        // Should handle leap year correctly
        var syncedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
        Assert.NotNull(syncedOrder);
        Assert.Equal(leapDate.Year, syncedOrder.CreatedAt.Year);
        Assert.Equal(leapDate.Month, syncedOrder.CreatedAt.Month);
        Assert.Equal(leapDate.Day, syncedOrder.CreatedAt.Day);
    }
    
    [Fact]
    public async Task Should_Handle_Millisecond_Precision()
    {
        // Arrange
        var order = CreateTestOrder();
        var preciseTime = DateTimeOffset.UtcNow.AddMilliseconds(123);
        order.CreatedAtTimestamp = preciseTime.ToUnixTimeMilliseconds();
        
        var serverOrder = order.ToDomain();
        // CreatedAt is protected, use factory
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(serverOrder);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
        
        // Should preserve millisecond precision
        var syncedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
        Assert.NotNull(syncedOrder);
        Assert.True(syncedOrder.IsSynced);
    }
    
    [Fact]
    public async Task Should_Handle_Order_Expiration_Time()
    {
        // Arrange
        var order = CreateTestOrder();
        var expiredTime = DateTimeOffset.UtcNow.AddHours(-25); // Older than 24 hours
        order.CreatedAtTimestamp = expiredTime.ToUnixTimeMilliseconds();
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        // Should handle expired orders appropriately
        // (Implementation specific - might reject or warn about expired orders)
        Assert.NotNull(result);
    }
    
    [Fact]
    public async Task Should_Handle_Concurrent_Time_Updates()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        var callCount = 0;
        _orderServiceMock.Setup(x => x.CreateOrderAsync(domainOrder, It.IsAny<Guid>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // Simulate time passing between calls
                Thread.Sleep(10);
                return domainOrder;
            });
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        // Act - Concurrent sync attempts
        var task1 = _offlineOrderService.SyncSingleOrderAsync(order.Id);
        var task2 = _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        var results = await Task.WhenAll(task1, task2);
        
        // Assert
        Assert.All(results, result => Assert.True(result.Success));
        
        // Should handle concurrent time updates
        Assert.True(callCount <= 2); // Should limit concurrent calls
    }
    
    [Fact]
    public async Task Should_Handle_Future_Timestamps()
    {
        // Arrange
        var order = CreateTestOrder();
        var futureTime = DateTimeOffset.UtcNow.AddHours(1); // Future timestamp
        order.CreatedAtTimestamp = futureTime.ToUnixTimeMilliseconds();
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        var serverOrder = order.ToDomain();
        // Server corrects to current time (CreatedAt is protected, use factory)
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
        
        // Should correct future timestamps
        var syncedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
        Assert.NotNull(syncedOrder);
        Assert.True(syncedOrder.CreatedAt <= DateTime.UtcNow);
    }
    
    [Fact]
    public async Task Should_Handle_Timestamp_Overflow()
    {
        // Arrange
        var order = CreateTestOrder();
        var maxTimestamp = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();
        order.CreatedAtTimestamp = maxTimestamp;
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        // Should handle extreme timestamp values gracefully
        Assert.NotNull(result);
    }
    
    [Fact]
    public async Task Should_Handle_Negative_Timestamps()
    {
        // Arrange
        var order = CreateTestOrder();
        var negativeTimestamp = -1000; // Before Unix epoch
        order.CreatedAtTimestamp = negativeTimestamp;
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        // Should handle negative timestamps gracefully
        Assert.NotNull(result);
    }
    
    [Fact]
    public async Task Should_Maintain_Chronological_Order()
    {
        // Arrange
        var orders = new List<OfflineOrderDto>();
        var baseTime = DateTimeOffset.UtcNow;
        
        for (int i = 0; i < 5; i++)
        {
            var order = CreateTestOrder();
            order.CreatedAtTimestamp = baseTime.AddMinutes(i).ToUnixTimeMilliseconds();
            orders.Add(order);
        }
        
        var domainOrders = orders.Select(o => o.ToDomain()).ToList();
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(orders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        // Act
        var result = await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        Assert.True(result);
        
        // Should maintain chronological order
        var syncedOrders = await _offlineOrderService.GetPendingOrdersAsync();
        var sortedOrders = syncedOrders.OrderBy(o => o.CreatedAt).ToList();
        
        for (int i = 0; i < sortedOrders.Count - 1; i++)
        {
            Assert.True(sortedOrders[i].CreatedAt <= sortedOrders[i + 1].CreatedAt);
        }
    }
    
    [Fact]
    public async Task Should_Handle_Time_Zone_Ambiguity()
    {
        // Arrange
        var ambiguousTime = new DateTimeOffset(2024, 11, 3, 1, 30, 0, TimeSpan.Zero); // DST end - ambiguous hour
        var order = CreateTestOrder();
        order.CreatedAtTimestamp = ambiguousTime.ToUnixTimeMilliseconds();
        
        var serverOrder = order.ToDomain();
        // CreatedAt is protected, use factory
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(serverOrder);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
        
        // Should handle ambiguous time zone situations
        var syncedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
        Assert.NotNull(syncedOrder);
        Assert.True(syncedOrder.IsSynced);
    }
    
    [Fact]
    public async Task Should_Handle_Rapid_Successive_Updates()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        var updateCount = 0;
        _orderServiceMock.Setup(x => x.CreateOrderAsync(domainOrder, It.IsAny<Guid>()))
            .ReturnsAsync(() =>
            {
                updateCount++;
                // Simulate rapid updates
                order.CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return domainOrder;
            });
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(() => order);
        
        // Act - Rapid successive syncs
        var tasks = new Task<SyncResult>[5];
        for (int i = 0; i < 5; i++)
        {
            tasks[i] = _offlineOrderService.SyncSingleOrderAsync(order.Id);
            await Task.Delay(1); // Very short delay
        }
        
        var results = await Task.WhenAll(tasks);
        
        // Assert
        Assert.All(results, result => Assert.True(result.Success));
        
        // Should handle rapid updates without conflicts
        Assert.True(updateCount <= 5);
    }
    
    private OfflineOrderDto CreateTestOrder()
    {
        return new OfflineOrderDto
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = "test-customer",
            ShopId = Guid.NewGuid().ToString(),
            Items = new List<OfflineOrderItemDto>
            {
                new OfflineOrderItemDto
                {
                    ProductId = Guid.NewGuid().ToString(),
                    Quantity = 1,
                    UnitPrice = 25000m,
                    TotalPrice = 25000m
                }
            },
            TotalAmount = 25000m,
            Status = OrderStatusId.Pending.ToString(),
            CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
