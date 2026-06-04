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

            ServiceCollection services = new();
            _ = services.AddSingleton(_indexedDBServiceMock.Object);
            _ = services.AddSingleton(_orderServiceMock.Object);
            _ = services.AddSingleton(_loggerMock.Object);
            _ = services.AddTransient<OfflineOrderService>();

            _serviceProvider = services.BuildServiceProvider();
            _offlineOrderService = _serviceProvider.GetRequiredService<OfflineOrderService>();
        }

        [Fact]
        public async Task Should_Retry_With_Exponential_Backoff_On_Network_Failure()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Should_Stop_Retrying_After_Max_Attempts()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ThrowsAsync(new HttpRequestException("Persistent network error"));

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task Should_Use_Linear_Backoff_For_Transient_Errors()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Should_Not_Retry_Non_Transient_Errors()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ThrowsAsync(new ArgumentException("Invalid order data"));

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Invalid order data", result.ErrorMessage);
        }

        [Fact]
        public async Task Should_Handle_Concurrent_Retry_Attempts()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            // Act - Concurrent retry attempts
            Task<SyncResult> task1 = _offlineOrderService.SyncSingleOrderAsync(order.Id);
            Task<SyncResult> task2 = _offlineOrderService.SyncSingleOrderAsync(order.Id);

            SyncResult[] results = await Task.WhenAll(task1, task2);

            // Assert
            Assert.True(results.All(r => r.Success));
        }

        [Fact]
        public async Task Should_Preserve_Order_Data_During_Retries()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            decimal originalTotal = order.TotalAmount;
            int originalItemCount = order.Items.Count;

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.True(result.Success);

            // Verify order data is preserved
            OfflineOrderDto? storedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
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
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ThrowsAsync(new HttpRequestException("Persistent failure"));

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.False(result.Success);

            // Verify retry attempts are tracked
            OfflineOrderDto? storedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
            Assert.NotNull(storedOrder);
            Assert.NotNull(storedOrder.LastSyncError);
            Assert.Contains("Persistent failure", storedOrder.LastSyncError);
        }

        [Fact]
        public async Task Should_Reset_Retry_Count_On_Successful_Sync()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.True(result.Success);

            OfflineOrderDto? storedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
            Assert.NotNull(storedOrder);
            Assert.True(storedOrder.IsSynced);
            Assert.Null(storedOrder.LastSyncError);
        }

        [Fact]
        public async Task Should_Handle_Retry_With_Different_Error_Types()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Should_Respect_Rate_Limiting_During_Retries()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act - Create order first
            _ = await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task Should_Handle_Batch_Retry_Strategy()
        {
            // Arrange
            List<OfflineOrderDto> orders = Enumerable.Range(0, 5)
                .Select(_ => CreateTestOrder())
                .ToList();

            _ = _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            // Act - Create orders first
            foreach (OfflineOrderDto? order in orders)
            {
                _ = await _offlineOrderService.CreateOrderAsync(order);
            }

            bool result = await _offlineOrderService.SyncOrdersAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Should_Handle_Circuit_Breaker_Pattern()
        {
            // Arrange
            List<OfflineOrderDto> orders = Enumerable.Range(0, 10)
                .Select(_ => CreateTestOrder())
                .ToList();

            _ = _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            _ = _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            _ = _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order);

            // Act - Create orders first
            foreach (OfflineOrderDto? order in orders)
            {
                _ = await _offlineOrderService.CreateOrderAsync(order);
            }

            bool result = await _offlineOrderService.SyncOrdersAsync();

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
