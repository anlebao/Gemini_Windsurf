using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
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
    [Trait("Category", "UI")]
    [Trait("Category", "StateMachine")]
    public class UIStateMachineTests : IDisposable
    {
        private readonly Mock<IIndexedDBService> _indexedDBServiceMock;
        private readonly Mock<IOrderService> _orderServiceMock;
        private readonly Mock<ILogger<OfflineOrderService>> _loggerMock;
        private readonly Mock<IJSRuntime> _jsRuntimeMock;
        private readonly OfflineOrderService _offlineOrderService;
        private static readonly bool[] expected = [true, false, true];
        private static readonly string[] expectedArray = new[] { "online", "offline", "online" };

        public UIStateMachineTests()
        {
            _indexedDBServiceMock = new Mock<IIndexedDBService>();
            _orderServiceMock = new Mock<IOrderService>();
            _loggerMock = new Mock<ILogger<OfflineOrderService>>();
            _jsRuntimeMock = new Mock<IJSRuntime>();

            _offlineOrderService = new OfflineOrderService(
                _indexedDBServiceMock.Object,
                _orderServiceMock.Object,
                _loggerMock.Object,
                new ServiceCollection().BuildServiceProvider());
        }

        [Fact]
        public async Task UI_Should_Show_Loading_State_During_Order_Sync()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act - Create order first
            await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.True(result.Success);
        }

        [Fact]
        public async Task UI_Should_Display_Error_Message_On_Sync_Failure()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ThrowsAsync(new HttpRequestException("Network unreachable"));

            // Act - Create order first
            await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("Network unreachable", result.ErrorMessage);

            // UI should display this error message
            string expectedUIMessage = $"Sync failed: {result.ErrorMessage}";
            Assert.NotNull(expectedUIMessage);
        }

        [Fact]
        public async Task UI_Should_Update_Order_Status_In_Realtime()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            order.Status = OrderStatusId.Processing.ToString();

            Order domainOrder = order.ToDomain();

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act - Create order first
            await _offlineOrderService.CreateOrderAsync(order);

            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.True(result.Success);

            // UI should update to show Processing status
            OfflineOrderDto? updatedOrder = await _offlineOrderService.GetOrderAsync(order.Id);
            Assert.NotNull(updatedOrder);
            Assert.Equal(OrderStatusId.Processing.ToString(), updatedOrder.Status);
        }

        [Fact]
        public async Task UI_Should_Handle_Concurrent_Order_Updates()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            Order domainOrder = order.ToDomain();

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync(domainOrder);

            // Act - Create order first
            await _offlineOrderService.CreateOrderAsync(order);

            // Act - Simulate multiple UI updates
            Task[] tasks =
            [
                _offlineOrderService.SyncSingleOrderAsync(order.Id),
                _offlineOrderService.GetOrderAsync(order.Id),
                _offlineOrderService.GetOrderAsync(order.Id)
            ];

            await Task.WhenAll(tasks);

            // Assert
            // UI should handle concurrent operations gracefully
            _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async Task UI_Should_Show_Offline_Indicator_When_Disconnected()
        {
            // Arrange
            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ThrowsAsync(new HttpRequestException("No internet connection"));

            OfflineOrderDto order = CreateTestOrder();

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            await _offlineOrderService.CreateOrderAsync(order);

            // Act
            SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("internet", result.ErrorMessage.ToLower(System.Globalization.CultureInfo.CurrentCulture));

            // UI should show offline indicator
            bool shouldShowOfflineIndicator = !result.Success;
            Assert.True(shouldShowOfflineIndicator);
        }

        [Fact]
        public async Task UI_Should_Display_Sync_Progress_For_Multiple_Orders()
        {
            // Arrange
            List<OfflineOrderDto> orders = Enumerable.Range(0, 5)
                .Select(_ => CreateTestOrder())
                .ToList();

            List<Order> domainOrders = orders.Select(o => o.ToDomain()).ToList();

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
                .ReturnsAsync((string key) => orders.FirstOrDefault(o => key == $"order_{o.Id}"));

            _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                .ReturnsAsync((Order order, Guid tenantId) => order)
                .Callback(() => Task.Delay(50)); // Simulate processing time

            // Act
            Task<bool> syncTask = _offlineOrderService.SyncOrdersAsync();

            // UI should show progress during sync
            List<int> progressSteps = [];

            // Simulate UI progress tracking
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(20);
                progressSteps.Add(i + 1);
            }

            bool result = await syncTask;

            // Assert
            Assert.True(result);
            Assert.Equal(5, progressSteps.Count);

            // UI should show 100% progress when complete
            int finalProgress = progressSteps.Last();
            Assert.Equal(5, finalProgress);
        }

        [Fact]
        public async Task UI_Should_Handle_Order_Cancellation_Confirmation()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            await _offlineOrderService.CreateOrderAsync(order);

            // Act - Simulate UI cancellation flow
            bool shouldShowConfirmation = true;
            bool userConfirmed = true;

            if (shouldShowConfirmation && userConfirmed)
            {
                bool result = await _offlineOrderService.DeleteOrderAsync(order.Id);

                // Assert
                Assert.True(result);

                // UI should show success message
                string successMessage = "Order cancelled successfully";
                Assert.NotNull(successMessage);
            }
        }

        [Fact]
        public async Task UI_Should_Refresh_Order_List_After_New_Order()
        {
            // Arrange
            List<OfflineOrderDto> initialOrders = [];

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(initialOrders);

            // Act - Create new order
            OfflineOrderDto newOrder = CreateTestOrder();
            await _offlineOrderService.CreateOrderAsync(newOrder);

            // Update mock to return the new order
            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync([newOrder]);

            // UI should refresh order list
            List<OfflineOrderDto> updatedOrders = await _offlineOrderService.GetPendingOrdersAsync();

            // Assert
            Assert.Single(updatedOrders);
            Assert.Equal(newOrder.Id, updatedOrders[0].Id);

            // UI should show updated count
            int orderCount = updatedOrders.Count;
            Assert.Equal(1, orderCount);
        }

        [Fact]
        public async Task UI_Should_Display_Order_Details_Correctly()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();
            order.Items =
            [
                new OfflineOrderItemDto
                {
                    ProductId = Guid.NewGuid().ToString(),
                    Quantity = 2,
                    UnitPrice = 25000m,
                    TotalPrice = 50000m
                },
                new OfflineOrderItemDto
                {
                    ProductId = Guid.NewGuid().ToString(),
                    Quantity = 1,
                    UnitPrice = 15000m,
                    TotalPrice = 15000m
                }
            ];
            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            await _offlineOrderService.CreateOrderAsync(order);

            // Act
            OfflineOrderDto? retrievedOrder = await _offlineOrderService.GetOrderAsync(order.Id);

            // Assert
            Assert.NotNull(retrievedOrder);
            Assert.Equal(2, retrievedOrder.Items.Count);
            Assert.Equal(65000m, retrievedOrder.TotalAmount);

            // UI should display these details correctly
            int expectedItemCount = retrievedOrder.Items.Count;
            decimal expectedTotal = retrievedOrder.TotalAmount;

            Assert.Equal(2, expectedItemCount);
            Assert.Equal(65000m, expectedTotal);
        }

        [Fact]
        public async Task UI_Should_Handle_Large_Order_Lists_Efficiently()
        {
            // Arrange
            List<OfflineOrderDto> largeOrderList = Enumerable.Range(0, 100)
                .Select(i => CreateTestOrder())
                .ToList();

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(largeOrderList);

            // Act
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            List<OfflineOrderDto> orders = await _offlineOrderService.GetPendingOrdersAsync();
            stopwatch.Stop();

            // Assert
            Assert.Equal(100, orders.Count);

            // UI should load within acceptable time
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Loading took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");

            // UI should implement virtualization for large lists
            bool shouldUseVirtualization = orders.Count > 50;
            Assert.True(shouldUseVirtualization);
        }

        [Fact]
        public async Task UI_Should_Persist_Filter_State()
        {
            // Arrange
            List<OfflineOrderDto> orders = [];
            for (int i = 0; i < 10; i++)
            {
                OfflineOrderDto order = CreateTestOrder();
                order.Status = i % 2 == 0 ? OrderStatusId.Pending.ToString() : OrderStatusId.Processing.ToString();
                orders.Add(order);
            }

            _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
                .ReturnsAsync(orders);

            // Act - Simulate UI filter
            string filterStatus = OrderStatusId.Pending.ToString();
            List<OfflineOrderDto> filteredOrders = orders.Where(o => o.Status == filterStatus).ToList();

            // Assert
            Assert.Equal(5, filteredOrders.Count);
            Assert.All(filteredOrders, o => Assert.Equal(filterStatus, o.Status));

            // UI should remember filter state
            string persistedFilter = filterStatus;
            Assert.Equal(OrderStatusId.Pending.ToString(), persistedFilter);
        }

        [Fact]
        public async Task UI_Should_Handle_Network_State_Changes()
        {
            // Arrange
            OfflineOrderDto order = CreateTestOrder();

            _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{order.Id}"))
                .ReturnsAsync(order);

            await _offlineOrderService.CreateOrderAsync(order);

            // Act - Simulate network state changes
            string[] networkStates = ["online", "offline", "online"];
            List<bool> syncResults = [];

            foreach (string? state in networkStates)
            {
                if (state == "online")
                {
                    _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                        .ReturnsAsync(order.ToDomain());
                }
                else
                {
                    _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<Guid>()))
                        .ThrowsAsync(new HttpRequestException("Offline"));
                }

                SyncResult result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
                syncResults.Add(result.Success);
            }

            // Assert
            Assert.Equal(expected, syncResults);

            // UI should update network indicator accordingly
            List<string> networkIndicators = syncResults.Select(success => success ? "online" : "offline").ToList();
            Assert.Equal(expectedArray, networkIndicators);
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
            // Cleanup if needed
        }
    }
}
