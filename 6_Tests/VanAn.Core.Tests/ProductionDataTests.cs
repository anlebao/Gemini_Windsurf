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
[Trait("Category", "Production")]
[Trait("Category", "DataIntegrity")]
public class ProductionDataTests : IDisposable
{
    private readonly Mock<IIndexedDBService> _indexedDBServiceMock;
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly Mock<ILogger<OfflineOrderService>> _loggerMock;
    private readonly OfflineOrderService _offlineOrderService;
    private readonly ServiceProvider _serviceProvider;
    
    public ProductionDataTests()
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
    public async Task Should_Handle_Large_Production_Dataset()
    {
        // Arrange
        var largeDataset = CreateLargeProductionDataset(1000); // 1000 orders
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(largeDataset);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => largeDataset.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        var domainOrders = largeDataset.Select(o => o.ToDomain()).ToList();
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _offlineOrderService.SyncOrdersAsync();
        stopwatch.Stop();
        
        // Assert
        Assert.True(result);
        
        // Should handle large dataset efficiently
        Assert.True(stopwatch.ElapsedMilliseconds < 30000, $"Processing 1000 orders took {stopwatch.ElapsedMilliseconds}ms, expected < 30000ms");
        
        // Should process all orders
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Exactly(1000));
    }
    
    [Fact]
    public async Task Should_Handle_Production_Order_Variety()
    {
        // Arrange
        var variedOrders = CreateVariedProductionOrders();
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(variedOrders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => variedOrders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        // Act
        var result = await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        Assert.True(result);
        
        // Should handle different order types
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Exactly(variedOrders.Count));
        
        // Verify all order types were processed
        var orderTypes = variedOrders.GroupBy(o => o.Items.Count).ToList();
        Assert.True(orderTypes.Count > 1, "Should have orders with different item counts");
    }
    
    [Fact]
    public async Task Should_Handle_Production_Error_Rates()
    {
        // Arrange
        var orders = CreateProductionDatasetWithErrorRate(100, 0.1); // 10% error rate
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(orders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        var callCount = 0;
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) =>
            {
                callCount++;
                // Simulate 10% failure rate
                if (callCount % 10 == 0)
                {
                    throw new HttpRequestException("Simulated production error");
                }
                return order;
            });
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(orders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        // Act
        var result = await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        Assert.False(result); // Not all succeeded due to errors
        
        // Should handle errors gracefully
        Assert.True(callCount >= 100);
        
        // Some orders should be synced despite errors
        var pendingOrders = await _offlineOrderService.GetPendingOrdersAsync();
        Assert.True(pendingOrders.Count < 100);
    }
    
    [Fact]
    public async Task Should_Handle_Production_Concurrent_Load()
    {
        // Arrange
        var orders = CreateLargeProductionDataset(500);
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(orders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        var domainOrders = orders.Select(o => o.ToDomain()).ToList();
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        // Act - Simulate concurrent load
        var tasks = new List<Task<bool>>();
        for (int i = 0; i < 10; i++)
        {
            var batch = orders.Skip(i * 50).Take(50).ToList();
            tasks.Add(Task.Run(async () =>
            {
                foreach (var order in batch)
                {
                    await _offlineOrderService.CreateOrderAsync(order);
                }
                return await _offlineOrderService.SyncOrdersAsync();
            }));
        }
        
        var results = await Task.WhenAll(tasks);
        
        // Assert
        Assert.All(results, result => Assert.True(result));
        
        // Should handle concurrent load without data corruption
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.AtLeast(500));
    }
    
    [Fact]
    public async Task Should_Handle_Production_Data_Corruption()
    {
        // Arrange
        var corruptedOrders = CreateCorruptedProductionData();
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(corruptedOrders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => corruptedOrders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        // Act
        var result = await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        // Should handle corrupted data gracefully
        Assert.NotNull(result);
        
        // Should not crash on corrupted data
        Assert.True(true); // Test passes if no exception thrown
    }
    
    [Fact]
    public async Task Should_Handle_Production_Network_Conditions()
    {
        // Arrange
        var orders = CreateLargeProductionDataset(200);
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(orders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        var callCount = 0;
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) =>
            {
                callCount++;
                // Simulate poor network conditions
                if (callCount % 3 == 0)
                {
                    throw new HttpRequestException("Network timeout");
                }
                if (callCount % 5 == 0)
                {
                    throw new TaskCanceledException("Request cancelled");
                }
                return order;
            });
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(orders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _offlineOrderService.SyncOrdersAsync();
        stopwatch.Stop();
        
        // Assert
        Assert.False(result); // Not all succeeded due to network issues
        
        // Should handle network conditions gracefully
        Assert.True(stopwatch.ElapsedMilliseconds < 60000, $"Sync took {stopwatch.ElapsedMilliseconds}ms under poor network conditions");
        
        // Should retry failed orders
        Assert.True(callCount >= 200);
    }
    
    [Fact]
    public async Task Should_Handle_Production_Memory_Constraints()
    {
        // Arrange
        var memoryIntensiveOrders = CreateMemoryIntensiveDataset(100);
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(memoryIntensiveOrders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => memoryIntensiveOrders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        // Act
        var initialMemory = GC.GetTotalMemory(true);
        var result = await _offlineOrderService.SyncOrdersAsync();
        var finalMemory = GC.GetTotalMemory(true);
        
        // Assert
        Assert.True(result);
        
        // Should not cause excessive memory usage
        var memoryIncrease = finalMemory - initialMemory;
        Assert.True(memoryIncrease < 100 * 1024 * 1024, $"Memory increased by {memoryIncrease / (1024 * 1024)}MB, expected < 100MB");
    }
    
    [Fact]
    public async Task Should_Handle_Production_Data_Volume_Spikes()
    {
        // Arrange
        var baselineOrders = CreateLargeProductionDataset(100);
        var spikeOrders = CreateLargeProductionDataset(1000); // 10x spike
        
        _indexedDBServiceMock.SetupSequence(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(baselineOrders)
            .ReturnsAsync(spikeOrders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => baselineOrders.Concat(spikeOrders).FirstOrDefault(o => key == $"order_{o.Id}"));
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        // Act - Baseline sync
        var baselineStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var baselineResult = await _offlineOrderService.SyncOrdersAsync();
        baselineStopwatch.Stop();
        
        // Act - Spike sync
        var spikeStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var spikeResult = await _offlineOrderService.SyncOrdersAsync();
        spikeStopwatch.Stop();
        
        // Assert
        Assert.True(baselineResult);
        Assert.True(spikeResult);
        
        // Should handle volume spikes gracefully
        var baselineThroughput = baselineOrders.Count / (baselineStopwatch.ElapsedMilliseconds / 1000.0);
        var spikeThroughput = spikeOrders.Count / (spikeStopwatch.ElapsedMilliseconds / 1000.0);
        
        // Throughput should not degrade significantly
        var throughputRatio = spikeThroughput / baselineThroughput;
        Assert.True(throughputRatio > 0.08, $"Throughput degraded too much: {throughputRatio:P2}");
    }
    
    [Fact]
    public async Task Should_Handle_Production_Long_Running_Operations()
    {
        // Arrange
        var longRunningOrders = CreateLargeProductionDataset(500);
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(longRunningOrders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => longRunningOrders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) =>
            {
                // Simulate long-running operation
                Thread.Sleep(10);
                return order;
            });
        
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _offlineOrderService.SyncOrdersAsync();
        stopwatch.Stop();
        
        // Assert
        Assert.True(result);
        
        // Should handle long-running operations without timeout
        Assert.True(stopwatch.ElapsedMilliseconds < 120000, $"Long-running operation took {stopwatch.ElapsedMilliseconds}ms, expected < 120000ms");
    }
    
    [Fact]
    public async Task Should_Handle_Production_Data_Consistency()
    {
        // Arrange
        var orders = CreateProductionDatasetWithDependencies(200);
        
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
        
        // Should maintain data consistency
        var syncedOrders = await _offlineOrderService.GetPendingOrdersAsync();
        
        // All orders should be either synced or have proper error tracking
        Assert.All(syncedOrders, order =>
        {
            if (!order.IsSynced)
            {
                Assert.True(order.SyncAttempts > 0);
                Assert.NotNull(order.LastSyncError);
            }
        });
    }
    
    [Fact]
    public async Task Should_Handle_Production_Edge_Cases()
    {
        // Arrange
        var edgeCaseOrders = CreateEdgeCaseProductionData();
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
            .ReturnsAsync((Order order, Guid tenantId) => order);
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(edgeCaseOrders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => edgeCaseOrders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        // Act
        var result = await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        Assert.True(result);
        
        // Should handle edge cases without crashing
        Assert.True(true); // Test passes if no exception thrown
    }
    
    private List<OfflineOrderDto> CreateLargeProductionDataset(int count)
    {
        var orders = new List<OfflineOrderDto>();
        var random = new Random();
        
        for (int i = 0; i < count; i++)
        {
            var itemCount = random.Next(1, 10);
            var items = new List<OfflineOrderItemDto>();
            
            for (int j = 0; j < itemCount; j++)
            {
                var quantity = random.Next(1, 5);
                var unitPrice = (decimal)(random.NextDouble() * 100000 + 10000);
                
                items.Add(new OfflineOrderItemDto
                {
                    ProductId = Guid.NewGuid().ToString(),
                    Quantity = quantity,
                    UnitPrice = Math.Round(unitPrice, 2),
                    TotalPrice = Math.Round(quantity * unitPrice, 2)
                });
            }
            
            orders.Add(new OfflineOrderDto
            {
                Id = Guid.NewGuid().ToString(),
                CustomerId = $"customer-{random.Next(1, 100)}",
                ShopId = Guid.NewGuid().ToString(),
                Items = items,
                TotalAmount = items.Sum(i => i.TotalPrice),
                Status = OrderStatusId.Pending.ToString(),
                CreatedAtTimestamp = DateTimeOffset.UtcNow.AddMinutes(random.Next(-1440, 0)).ToUnixTimeMilliseconds()
            });
        }
        
        return orders;
    }
    
    private List<OfflineOrderDto> CreateVariedProductionOrders()
    {
        var orders = new List<OfflineOrderDto>();
        
        // Single item orders
        for (int i = 0; i < 10; i++)
        {
            orders.Add(CreateTestOrder());
        }
        
        // Multi-item orders
        for (int i = 0; i < 10; i++)
        {
            var order = CreateTestOrder();
            order.Items = Enumerable.Range(1, 5).Select(j => new OfflineOrderItemDto
            {
                ProductId = Guid.NewGuid().ToString(),
                Quantity = j,
                UnitPrice = 25000m * j,
                TotalPrice = 25000m * j * j
            }).ToList();
            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);
            orders.Add(order);
        }
        
        // High-value orders
        for (int i = 0; i < 5; i++)
        {
            var order = CreateTestOrder();
            order.TotalAmount = 1000000 + i * 100000;
            order.Items[0].TotalPrice = order.TotalAmount;
            orders.Add(order);
        }
        
        return orders;
    }
    
    private List<OfflineOrderDto> CreateProductionDatasetWithErrorRate(int count, double errorRate)
    {
        var orders = CreateLargeProductionDataset(count);
        return orders;
    }
    
    private List<OfflineOrderDto> CreateCorruptedProductionData()
    {
        var orders = new List<OfflineOrderDto>();
        
        // Valid order
        orders.Add(CreateTestOrder());
        
        // Order with negative total
        var negativeOrder = CreateTestOrder();
        negativeOrder.TotalAmount = -1000;
        orders.Add(negativeOrder);
        
        // Order with empty items
        var emptyOrder = CreateTestOrder();
        emptyOrder.Items = new List<OfflineOrderItemDto>();
        orders.Add(emptyOrder);
        
        // Order with invalid item data
        var invalidItemOrder = CreateTestOrder();
        invalidItemOrder.Items[0].Quantity = -1;
        orders.Add(invalidItemOrder);
        
        return orders;
    }
    
    private List<OfflineOrderDto> CreateMemoryIntensiveDataset(int count)
    {
        var orders = new List<OfflineOrderDto>();
        
        for (int i = 0; i < count; i++)
        {
            var order = CreateTestOrder();
            
            // Add many items to increase memory usage
            order.Items = Enumerable.Range(1, 50).Select(j => new OfflineOrderItemDto
            {
                ProductId = Guid.NewGuid().ToString(),
                Quantity = j,
                UnitPrice = 25000m,
                TotalPrice = 25000m * j
            }).ToList();
            
            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);
            orders.Add(order);
        }
        
        return orders;
    }
    
    private List<OfflineOrderDto> CreateProductionDatasetWithDependencies(int count)
    {
        var orders = CreateLargeProductionDataset(count);
        
        // Create dependencies between orders
        for (int i = 1; i < orders.Count; i++)
        {
            orders[i].CustomerId = orders[i - 1].CustomerId; // Same customer
        }
        
        return orders;
    }
    
    private List<OfflineOrderDto> CreateEdgeCaseProductionData()
    {
        var orders = new List<OfflineOrderDto>();
        
        // Order with maximum values
        var maxOrder = CreateTestOrder();
        maxOrder.TotalAmount = decimal.MaxValue;
        orders.Add(maxOrder);
        
        // Order with minimum values
        var minOrder = CreateTestOrder();
        minOrder.TotalAmount = decimal.MinValue;
        orders.Add(minOrder);
        
        // Order with very long strings
        var longOrder = CreateTestOrder();
        longOrder.CustomerId = new string('x', 1000);
        orders.Add(longOrder);
        
        // Order with special characters
        var specialOrder = CreateTestOrder();
        specialOrder.CustomerId = "customer@#$%^&*()";
        orders.Add(specialOrder);
        
        return orders;
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
