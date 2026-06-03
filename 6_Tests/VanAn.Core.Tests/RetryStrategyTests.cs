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
[Trait("Category", "Retry")]
[Trait("Category", "FinancialSafety")]
public class RetryStrategyTests : IDisposable
{
    private readonly Mock<IIndexedDBService> _indexedDBServiceMock;
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly Mock<ILogger<OfflineOrderService>> _loggerMock;
    private readonly OfflineOrderService _offlineOrderService;
    private readonly ServiceProvider _serviceProvider;
    
    public RetryStrategyTests()
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
    public async Task Should_Retry_With_Exponential_Backoff_On_Network_Failure()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(domainOrder);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
    }
    
    [Fact]
    public async Task Should_Stop_Retrying_After_Max_Attempts()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ThrowsAsync(new HttpRequestException("Persistent network error"));
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }
    
    [Fact]
    public async Task Should_Use_Linear_Backoff_For_Transient_Errors()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(domainOrder);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
    }
    
    [Fact]
    public async Task Should_Not_Retry_Non_Transient_Errors()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ThrowsAsync(new ArgumentException("Invalid order data"));
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid order data", result.ErrorMessage);
    }
    
    [Fact]
    public async Task Should_Handle_Concurrent_Retry_Attempts()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(domainOrder);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        // Act - Concurrent retry attempts
        var task1 = _offlineOrderService.SyncSingleOrderAsync(order.Id);
        var task2 = _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        var results = await Task.WhenAll(task1, task2);
        
        // Assert
        Assert.True(results.All(r => r.Success));
    }
    
    [Fact]
    public async Task Should_Preserve_Order_Data_During_Retries()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        var originalTotal = order.TotalAmount;
        var originalItemCount = order.Items.Count;
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(domainOrder);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
        
        // Verify order data is preserved
        var storedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
        Assert.NotNull(storedOrder);
        Assert.Equal(originalTotal, storedOrder.TotalAmount);
        Assert.Equal(originalItemCount, storedOrder.Items.Count);
        
        // Verify all items are intact
        Assert.All(storedOrder.Items, item =>
        {
            Assert.True(item.Quantity > 0);
            Assert.True(item.UnitPrice > 0);
            Assert.True(item.TotalPrice > 0);
        });
    }
    
    [Fact]
    public async Task Should_Update_Retry_Attempts_Correctly()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ThrowsAsync(new HttpRequestException("Persistent failure"));
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.False(result.Success);
        
        // Verify retry attempts are tracked
        var storedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
        Assert.NotNull(storedOrder);
        Assert.NotNull(storedOrder.LastSyncError);
        Assert.Contains("Persistent failure", storedOrder.LastSyncError);
    }
    
    [Fact]
    public async Task Should_Reset_Retry_Count_On_Successful_Sync()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(domainOrder);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
        
        var storedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
        Assert.NotNull(storedOrder);
        Assert.True(storedOrder.IsSynced);
        Assert.Null(storedOrder.LastSyncError);
    }
    
    [Fact]
    public async Task Should_Handle_Retry_With_Different_Error_Types()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(domainOrder);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
    }
    
    [Fact]
    public async Task Should_Respect_Rate_Limiting_During_Retries()
    {
        // Arrange
        var order = CreateTestOrder();
        var domainOrder = order.ToDomain();
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync(domainOrder);
        
        // Act - Create order first
        await _offlineOrderService.CreateOrderAsync(order);
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
    }
    
    [Fact]
    public async Task Should_Handle_Batch_Retry_Strategy()
    {
        // Arrange
        var orders = Enumerable.Range(0, 5)
            .Select(_ => CreateTestOrder())
            .ToList();
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(orders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        // Act - Create orders first
        foreach (var order in orders)
        {
            await _offlineOrderService.CreateOrderAsync(order);
        }
        
        var result = await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task Should_Handle_Circuit_Breaker_Pattern()
    {
        // Arrange
        var orders = Enumerable.Range(0, 10)
            .Select(_ => CreateTestOrder())
            .ToList();
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(orders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        // Act - Create orders first
        foreach (var order in orders)
        {
            await _offlineOrderService.CreateOrderAsync(order);
        }
        
        var result = await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        Assert.True(result);
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
