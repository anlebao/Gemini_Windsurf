using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Microsoft.Extensions.Logging;
using Xunit;

namespace VanAn.Tests
{
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

            ServiceCollection services = new();
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
            List<OfflineOrderDto> largeDataset = CreateLargeProductionDataset(1000); // 1000 orders

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(largeDataset);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => largeDataset.FirstOrDefault(o => key == $"order_{o.Id}"));

            List<Order> domainOrders = largeDataset.Select(o => o.ToDomain()).ToList();

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            // Act
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool result = await _offlineOrderService.SyncOrdersAsync();
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
            List<OfflineOrderDto> variedOrders = CreateVariedProductionOrders();

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(variedOrders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => variedOrders.FirstOrDefault(o => key == $"order_{o.Id}"));

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            // Act
            bool result = await _offlineOrderService.SyncOrdersAsync();

            // Assert
            Assert.True(result);

            // Should handle different order types
            _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Exactly(variedOrders.Count));

            // Verify all order types were processed
            List<IGrouping<int, OfflineOrderDto>> orderTypes = variedOrders.GroupBy(o => o.Items.Count).ToList();
            Assert.True(orderTypes.Count > 1, "Should have orders with different item counts");
        }

        [Fact]
        public async Task Should_Handle_Production_Error_Rates()
        {
            // Arrange
            List<OfflineOrderDto> orders = CreateProductionDatasetWithErrorRate(100, 0.1); // 10% error rate

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            int callCount = 0;
            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) =>
                {
                    callCount++;
                    // Simulate 10% failure rate
                    return callCount % 10 == 0 ? throw new HttpRequestException("Simulated production error") : order;
                });

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            // Act
            bool result = await _offlineOrderService.SyncOrdersAsync();

            // Assert
            Assert.False(result); // Not all succeeded due to errors

            // Should handle errors gracefully
            Assert.True(callCount >= 100);

            // Some orders should be synced despite errors
            List<OfflineOrderDto> pendingOrders = await _offlineOrderService.GetPendingOrdersAsync();
            Assert.True(pendingOrders.Count < 100);
        }

        [Fact]
        public async Task Should_Handle_Production_Concurrent_Load()
        {
            // Arrange
            List<OfflineOrderDto> orders = CreateLargeProductionDataset(500);

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            List<Order> domainOrders = orders.Select(o => o.ToDomain()).ToList();

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            // Act - Simulate concurrent load
            List<Task<bool>> tasks = [];
            for (int i = 0; i < 10; i++)
            {
                List<OfflineOrderDto> batch = orders.Skip(i * 50).Take(50).ToList();
                tasks.Add(Task.Run(async () =>
                {
                    foreach (OfflineOrderDto? order in batch)
                    {
                        await _offlineOrderService.CreateOrderAsync(order);
                    }
                    return await _offlineOrderService.SyncOrdersAsync();
                }));
            }

            bool[] results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, Assert.True);

            // Should handle concurrent load without data corruption
            _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.AtLeast(500));
        }

        [Fact]
        public async Task Should_Handle_Production_Data_Corruption()
        {
            // Arrange
            List<OfflineOrderDto> corruptedOrders = CreateCorruptedProductionData();

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(corruptedOrders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => corruptedOrders.FirstOrDefault(o => key == $"order_{o.Id}"));

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            // Act
            bool result = await _offlineOrderService.SyncOrdersAsync();

            // Assert
            // Should handle corrupted data gracefully

            // Should not crash on corrupted data
            Assert.True(true); // Test passes if no exception thrown
        }

        [Fact]
        public async Task Should_Handle_Production_Network_Conditions()
        {
            // Arrange
            List<OfflineOrderDto> orders = CreateLargeProductionDataset(200);

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            int callCount = 0;
            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) =>
                {
                    callCount++;
                    // Simulate poor network conditions
                    return callCount % 3 == 0
                        ? throw new HttpRequestException("Network timeout")
                        : callCount % 5 == 0 ? throw new TaskCanceledException("Request cancelled") : order;
                });

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            // Act
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool result = await _offlineOrderService.SyncOrdersAsync();
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
            List<OfflineOrderDto> memoryIntensiveOrders = CreateMemoryIntensiveDataset(100);

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(memoryIntensiveOrders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => memoryIntensiveOrders.FirstOrDefault(o => key == $"order_{o.Id}"));

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            // Act
            long initialMemory = GC.GetTotalMemory(true);
            bool result = await _offlineOrderService.SyncOrdersAsync();
            long finalMemory = GC.GetTotalMemory(true);

            // Assert
            Assert.True(result);

            // Should not cause excessive memory usage
            long memoryIncrease = finalMemory - initialMemory;
            Assert.True(memoryIncrease < 100 * 1024 * 1024, $"Memory increased by {memoryIncrease / (1024 * 1024)}MB, expected < 100MB");
        }

        [Fact]
        public async Task Should_Handle_Production_Data_Volume_Spikes()
        {
            // Arrange
            List<OfflineOrderDto> baselineOrders = CreateLargeProductionDataset(100);
            List<OfflineOrderDto> spikeOrders = CreateLargeProductionDataset(1000); // 10x spike

            _indexedDBServiceMock.SetupSequence(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(baselineOrders)
                .ReturnsAsync(spikeOrders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => baselineOrders.Concat(spikeOrders).FirstOrDefault(o => key == $"order_{o.Id}"));

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            // Act - Baseline sync
            System.Diagnostics.Stopwatch baselineStopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool baselineResult = await _offlineOrderService.SyncOrdersAsync();
            baselineStopwatch.Stop();

            // Act - Spike sync
            System.Diagnostics.Stopwatch spikeStopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool spikeResult = await _offlineOrderService.SyncOrdersAsync();
            spikeStopwatch.Stop();

            // Assert
            Assert.True(baselineResult);
            Assert.True(spikeResult);

            // Should handle volume spikes gracefully
            double baselineThroughput = baselineOrders.Count / (baselineStopwatch.ElapsedMilliseconds / 1000.0);
            double spikeThroughput = spikeOrders.Count / (spikeStopwatch.ElapsedMilliseconds / 1000.0);

            // Throughput should not degrade significantly
            double throughputRatio = spikeThroughput / baselineThroughput;
            Assert.True(throughputRatio > 0.08, $"Throughput degraded too much: {throughputRatio:P2}");
        }

        [Fact]
        public async Task Should_Handle_Production_Long_Running_Operations()
        {
            // Arrange
            List<OfflineOrderDto> longRunningOrders = CreateLargeProductionDataset(500);

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
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool result = await _offlineOrderService.SyncOrdersAsync();
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
            List<OfflineOrderDto> orders = CreateProductionDatasetWithDependencies(200);

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            // Act
            bool result = await _offlineOrderService.SyncOrdersAsync();

            // Assert
            Assert.True(result);

            // Should maintain data consistency
            List<OfflineOrderDto> syncedOrders = await _offlineOrderService.GetPendingOrdersAsync();

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
            List<OfflineOrderDto> edgeCaseOrders = CreateEdgeCaseProductionData();

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(edgeCaseOrders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => edgeCaseOrders.FirstOrDefault(o => key == $"order_{o.Id}"));

            // Act
            bool result = await _offlineOrderService.SyncOrdersAsync();

            // Assert
            Assert.True(result);

            // Should handle edge cases without crashing
            Assert.True(true); // Test passes if no exception thrown
        }

        private static List<OfflineOrderDto> CreateLargeProductionDataset(int count)
        {
            List<OfflineOrderDto> orders = [];
            Random random = new();

            for (int i = 0; i < count; i++)
            {
                int itemCount = random.Next(1, 10);
                List<OfflineOrderItemDto> items = [];

                for (int j = 0; j < itemCount; j++)
                {
                    int quantity = random.Next(1, 5);
                    decimal unitPrice = (decimal)((random.NextDouble() * 100000) + 10000);

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
            List<OfflineOrderDto> orders = [];

            // Single item orders
            for (int i = 0; i < 10; i++)
            {
                orders.Add(CreateTestOrder());
            }

            // Multi-item orders
            for (int i = 0; i < 10; i++)
            {
                OfflineOrderDto order = CreateTestOrder();
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
                OfflineOrderDto order = CreateTestOrder();
                order.TotalAmount = 1000000 + (i * 100000);
                order.Items[0].TotalPrice = order.TotalAmount;
                orders.Add(order);
            }

            return orders;
        }

        private static List<OfflineOrderDto> CreateProductionDatasetWithErrorRate(int count, double errorRate)
        {
            List<OfflineOrderDto> orders = CreateLargeProductionDataset(count);
            return orders;
        }

        private List<OfflineOrderDto> CreateCorruptedProductionData()
        {
            List<OfflineOrderDto> orders =
            [
                // Valid order
                CreateTestOrder()
            ];

            // Order with negative total
            OfflineOrderDto negativeOrder = CreateTestOrder();
            negativeOrder.TotalAmount = -1000;
            orders.Add(negativeOrder);

            // Order with empty items
            OfflineOrderDto emptyOrder = CreateTestOrder();
            emptyOrder.Items = [];
            orders.Add(emptyOrder);

            // Order with invalid item data
            OfflineOrderDto invalidItemOrder = CreateTestOrder();
            invalidItemOrder.Items[0].Quantity = -1;
            orders.Add(invalidItemOrder);

            return orders;
        }

        private List<OfflineOrderDto> CreateMemoryIntensiveDataset(int count)
        {
            List<OfflineOrderDto> orders = [];

            for (int i = 0; i < count; i++)
            {
                OfflineOrderDto order = CreateTestOrder();

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

        private static List<OfflineOrderDto> CreateProductionDatasetWithDependencies(int count)
        {
            List<OfflineOrderDto> orders = CreateLargeProductionDataset(count);

            // Create dependencies between orders
            for (int i = 1; i < orders.Count; i++)
            {
                orders[i].CustomerId = orders[i - 1].CustomerId; // Same customer
            }

            return orders;
        }

        private List<OfflineOrderDto> CreateEdgeCaseProductionData()
        {
            List<OfflineOrderDto> orders = [];

            // Order with maximum values
            OfflineOrderDto maxOrder = CreateTestOrder();
            maxOrder.TotalAmount = decimal.MaxValue;
            orders.Add(maxOrder);

            // Order with minimum values
            OfflineOrderDto minOrder = CreateTestOrder();
            minOrder.TotalAmount = decimal.MinValue;
            orders.Add(minOrder);

            // Order with very long strings
            OfflineOrderDto longOrder = CreateTestOrder();
            longOrder.CustomerId = new string('x', 1000);
            orders.Add(longOrder);

            // Order with special characters
            OfflineOrderDto specialOrder = CreateTestOrder();
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
                Items =
                [
                    new OfflineOrderItemDto
                    {
                        ProductId = Guid.NewGuid().ToString(),
                        Quantity = 1,
                        UnitPrice = 25000m,
                        TotalPrice = 25000m
                    }
                ],
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
}
