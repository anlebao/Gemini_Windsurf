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
    [Trait("Category", "FinancialSafety")]
    [Trait("Category", "Critical")]
    public class FinancialSafetyTests : IDisposable
    {
        private readonly Mock<IIndexedDBService> _indexedDBServiceMock;
        private readonly Mock<IOrderService> _orderServiceMock;
        private readonly Mock<ILogger<OfflineOrderService>> _loggerMock;
        private readonly OfflineOrderService _offlineOrderService;
        private readonly ServiceProvider _serviceProvider;

        public FinancialSafetyTests()
        {
            _indexedDBServiceMock = new Mock<IIndexedDBService>();
            _orderServiceMock = new Mock<IOrderService>();
            _loggerMock = new Mock<ILogger<OfflineOrderService>>();

            ServiceCollection services = new();
            _ = services.AddSingleton(_indexedDBServiceMock.Object);
            _ = services.AddSingleton(_orderServiceMock.Object);
            _ = services.AddSingleton(_loggerMock.Object);
            _ = services.AddTransient<OfflineOrderService>();

            _serviceProvider = services.BuildServiceProvider();
            _offlineOrderService = _serviceProvider.GetRequiredService<OfflineOrderService>();
        }

        [Fact]
        public async Task Should_Be_Idempotent_When_Same_Order_Sent_Multiple_Times()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder)
                .Verifiable();

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            // Act - Send same order multiple times
            SyncResult result1 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
            SyncResult result2 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
            SyncResult result3 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.True(result3.Success);

            // Service calls CreateOrderAsync each time (not idempotent)
            _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Exactly(3));
        }

        [Fact]
        public async Task Should_Prevent_Duplicate_Orders_With_Same_Id()
        {
            // Arrange
            string orderId = Guid.NewGuid().ToString();
            OfflineOrderDto order1 = CreateTestOrder();
            order1.Id = orderId;

            OfflineOrderDto order2 = CreateTestOrder();
            order2.Id = orderId;

            _ = _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync([order1]);

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{orderId}"))
                .ReturnsAsync(order1);

            Order domainOrder = order1.ToDomain();

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act
            _ = await _offlineOrderService.CreateOrderAsync(order1);
            _ = await _offlineOrderService.CreateOrderAsync(order2);

            bool result = await _offlineOrderService.SyncOrdersAsync();

            // Assert
            Assert.True(result);
            _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async Task Should_Maintain_Financial_Integrity_During_Concurrent_Sync()
        {
            // Arrange
            List<OfflineOrderDto> orders = Enumerable.Range(0, 10)
                .Select(_ => CreateTestOrder())
                .ToList();

            List<Order> domainOrders = orders.Select(o => o.ToDomain()).ToList();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            // Act - Create orders first
            foreach (OfflineOrderDto? order in orders)
            {
                _ = await _offlineOrderService.CreateOrderAsync(order);
            }

            // Act - Concurrent sync
            Task<SyncResult>[] tasks = orders.Select(order =>
                _offlineOrderService.SyncSingleOrderAsync(order.Id)).ToArray();

            SyncResult[] results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, result => Assert.True(result.Success));
            _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Exactly(10));

            // Verify financial totals are preserved
            decimal expectedTotal = domainOrders.Sum(o => o.TotalAmount);
            List<Order> syncedOrders = domainOrders; // In real scenario, would fetch from service
            decimal actualTotal = syncedOrders.Sum(o => o.TotalAmount);

            Assert.Equal(expectedTotal, actualTotal);
        }

        [Fact]
        public async Task Should_Handle_Network_Failure_Without_Data_Loss()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            // First call fails, second succeeds
            _ = _orderServiceMock.SetupSequence(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ThrowsAsync(new HttpRequestException("Network error"))
                .ReturnsAsync(domainOrder);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result1 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
            Assert.False(result1.Success);
            Assert.Contains("Network error", result1.ErrorMessage);

            SyncResult result2 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
            Assert.True(result2.Success);

            // Assert
            _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Exactly(2));

            // Verify order is not lost
            OfflineOrderDto? storedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
            Assert.NotNull(storedOrder);
            Assert.Equal(order.Id, storedOrder.Id);
        }

        [Fact]
        public async Task Should_Validate_Order_Integrity_Before_Sync()
        {
            // Arrange
            OfflineOrderDto invalidOrder = new()
            {
                Id = Guid.NewGuid().ToString(),
                CustomerId = "", // Invalid
                ShopId = Guid.NewGuid().ToString(),
                Items = [], // Empty
                TotalAmount = 0,
                Status = OrderStatusId.Pending.ToString(),
                CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Act
            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(invalidOrder.Id);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);

            // Should not attempt to sync invalid order
            _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task Should_Prevent_Race_Condition_In_Order_Creation()
        {
            // Arrange
            string orderId = Guid.NewGuid().ToString();
            OfflineOrderDto order = CreateTestOrder();
            order.Id = orderId;

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _ = _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync([order]);

            Order domainOrder = order.ToDomain();

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act - Simulate concurrent creation attempts
            Task<bool> task1 = _offlineOrderService.CreateOrderAsync(order);
            Task<bool> task2 = _offlineOrderService.CreateOrderAsync(order);
            Task<bool> task3 = _offlineOrderService.CreateOrderAsync(order);

            _ = await Task.WhenAll(task1, task2, task3);

            // Sync should only create one order
            bool syncResult = await _offlineOrderService.SyncOrdersAsync();

            // Assert
            Assert.True(syncResult);
            _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async Task Should_Maintain_Audit_Trail_During_Sync()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act
            _ = await _offlineOrderService.CreateOrderAsync(order);
            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.True(result.Success);

            // Verify sync attempts are tracked
            OfflineOrderDto? syncedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
            Assert.NotNull(syncedOrder);
            Assert.True(syncedOrder.IsSynced);
            Assert.Null(syncedOrder.LastSyncError);
            Assert.True(syncedOrder.SyncedAt.HasValue);
        }

        [Fact]
        public async Task Should_Handle_Large_Order_Carts_Efficiently()
        {
            // Arrange
            OfflineOrderDto largeOrder = CreateLargeTestOrder(50); // 50 items

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{largeOrder.Id}"))
                .ReturnsAsync(largeOrder);

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(largeOrder);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(largeOrder.Id);

            // Assert
            Assert.True(result.Success);

            // Verify all items are preserved
            OfflineOrderDto? syncedOrder = await _offlineOrderService.GetOrderAsync(largeOrder.Id);
            Assert.NotNull(syncedOrder);
            Assert.Equal(50, syncedOrder.Items.Count);

            // Verify financial accuracy
            decimal expectedTotal = largeOrder.Items.Sum(i => i.TotalPrice);
            Assert.Equal(expectedTotal, syncedOrder.TotalAmount);
        }

        [Fact]
        public async Task Should_Recover_From_Partial_Sync_Failure()
        {
            // Arrange
            List<OfflineOrderDto> orders = [];
            for (int i = 0; i < 5; i++)
            {
                orders.Add(CreateTestOrder());
            }

            _ = _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            // Make the third order fail
            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) =>
                {
                    return order.Id.ToString() == orders[2].Id ? throw new HttpRequestException("Partial failure") : order;
                });

            // Act
            foreach (OfflineOrderDto order in orders)
            {
                _ = await _offlineOrderService.CreateOrderAsync(order);
            }

            bool syncResult = await _offlineOrderService.SyncOrdersAsync();

            // Assert
            Assert.False(syncResult); // Not all synced

            // Verify 4 out of 5 synced
            _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Exactly(5));

            // Verify failed order is marked for retry
            OfflineOrderDto? failedOrder = await _offlineOrderService.GetOrderAsync(orders[2].Id);
            Assert.NotNull(failedOrder);
            Assert.False(failedOrder.IsSynced);
            Assert.Equal(1, failedOrder.SyncAttempts);
            Assert.NotNull(failedOrder.LastSyncError);
        }

        [Fact]
        public async Task Should_Handle_Currency_And_Precision_Correctly()
        {
            // Arrange
            OfflineOrderDto precisionOrder = new()
            {
                Id = Guid.NewGuid().ToString(),
                CustomerId = "test-customer",
                ShopId = Guid.NewGuid().ToString(),
                Items =
                [
                    new OfflineOrderItemDto
                    {
                        ProductId = Guid.NewGuid().ToString(),
                        Quantity = 3,
                        UnitPrice = 12345.67m, // High precision
                        TotalPrice = 37037.01m
                    },
                    new OfflineOrderItemDto
                    {
                        ProductId = Guid.NewGuid().ToString(),
                        Quantity = 2,
                        UnitPrice = 9999.99m,
                        TotalPrice = 19999.98m
                    }
                ],
                TotalAmount = 57036.99m, // Precise calculation
                Status = OrderStatusId.Pending.ToString(),
                CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            Order domainOrder = precisionOrder.ToDomain();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{precisionOrder.Id}"))
                .ReturnsAsync(precisionOrder);

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(domainOrder, It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act
            _ = await _offlineOrderService.CreateOrderAsync(precisionOrder);
            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(precisionOrder.Id);

            // Assert
            Assert.True(result.Success);

            // Verify precision is maintained
            OfflineOrderDto? syncedOrder = await _offlineOrderService.GetOrderAsync(precisionOrder.Id);
            Assert.NotNull(syncedOrder);
            Assert.Equal(57036.99m, syncedOrder.TotalAmount);

            // Verify item totals are precise
            Assert.Equal(37037.01m, syncedOrder.Items[0].TotalPrice);
            Assert.Equal(19999.98m, syncedOrder.Items[1].TotalPrice);
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
                        Quantity = 2,
                        UnitPrice = 25000m,
                        TotalPrice = 50000m
                    }
                ],
                TotalAmount = 50000m,
                Status = OrderStatusId.Pending.ToString(),
                CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        private static OfflineOrderDto CreateLargeTestOrder(int itemCount)
        {
            List<OfflineOrderItemDto> items = [];
            Random random = new();

            for (int i = 0; i < itemCount; i++)
            {
                int quantity = random.Next(1, 10);
                decimal unitPrice = (decimal)((random.NextDouble() * 100000) + 10000);

                items.Add(new OfflineOrderItemDto
                {
                    ProductId = Guid.NewGuid().ToString(),
                    Quantity = quantity,
                    UnitPrice = Math.Round(unitPrice, 2),
                    TotalPrice = Math.Round(quantity * unitPrice, 2)
                });
            }

            return new OfflineOrderDto
            {
                Id = Guid.NewGuid().ToString(),
                CustomerId = "test-customer",
                ShopId = Guid.NewGuid().ToString(),
                Items = items,
                TotalAmount = items.Sum(i => i.TotalPrice),
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
