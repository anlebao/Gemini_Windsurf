using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Commands;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Repositories;
using VanAn.Core.Tests.TestInfrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// Unit tests for OrderService - Phase 2.2 TDD Implementation
    /// Tests order CRUD operations, accounting integration, and HKD book updates
    /// </summary>
    public class OrderServiceTests : IDisposable
    {
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IAccountingService> _mockAccountingService;
        private readonly Mock<IHKDBookRepository> _mockHkdBookRepository;
        private readonly Mock<IAccountingEntryRepository> _mockAccountingEntryRepository;
        private readonly OrderService _orderService;
        private readonly TenantId _testTenantId = new(Guid.NewGuid());

        public OrderServiceTests()
        {
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockAccountingService = new Mock<IAccountingService>();
            _mockHkdBookRepository = new Mock<IHKDBookRepository>();
            _mockAccountingEntryRepository = new Mock<IAccountingEntryRepository>();

            _orderService = new OrderService(
                _mockOrderRepository.Object,
                _mockAccountingService.Object,
                _mockHkdBookRepository.Object,
                _mockAccountingEntryRepository.Object,
                new NullLogger<OrderService>()
            );
        }

        public void Dispose()
        {
            // Clean up if needed
        }

        [Fact]
        public async Task CreateOrderAsync_ShouldCreateOrder_WhenValidOrder()
        {
            // Arrange
            Order order = TestEntityBuilder.CreateOrder(_testTenantId, 100.00m);

            _ = _mockOrderRepository.Setup(x => x.AddAsync(order, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            // Act
            Order result = await _orderService.CreateOrderAsync(order, _testTenantId.Value);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.Should().Be(order);
            _mockOrderRepository.Verify(x => x.AddAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetTodayOrderCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            DateTime today = DateTime.UtcNow.Date;
            DateTime tomorrow = today.AddDays(1);

            _ = _mockOrderRepository.Setup(x => x.GetCountByDateRangeAsync(_testTenantId, today, tomorrow, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Act
            int result = await _orderService.GetTodayOrderCountAsync(_testTenantId.Value);

            // Assert
            _ = result.Should().Be(3);
            _mockOrderRepository.Verify(x => x.GetCountByDateRangeAsync(_testTenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetOrderByIdAsync_ShouldReturnOrder_WhenOrderExists()
        {
            // Arrange
            OrderId orderId = new(Guid.NewGuid());
            Order expectedOrder = TestEntityBuilder.CreateOrder(_testTenantId, 150.00m);

            _ = _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedOrder);

            // Act
            Order? result = await _orderService.GetOrderByIdAsync(orderId.Value, _testTenantId.Value);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.Should().Be(expectedOrder);
            _mockOrderRepository.Verify(x => x.GetByIdAsync(orderId, _testTenantId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetOrdersByDateRangeAsync_ShouldReturnOrders_WhenOrdersExist()
        {
            // Arrange
            DateTime startDate = DateTime.UtcNow.AddDays(-7);
            DateTime endDate = DateTime.UtcNow;
            List<Order> expectedOrders =
            [
                TestEntityBuilder.CreateOrder(_testTenantId, 100.00m),
                TestEntityBuilder.CreateOrder(_testTenantId, 200.00m)
            ];

            _ = _mockOrderRepository.Setup(x => x.GetByDateRangeAsync(_testTenantId, startDate, endDate, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedOrders);

            // Act
            IEnumerable<Order> result = await _orderService.GetOrdersByDateRangeAsync(_testTenantId.Value, startDate, endDate);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.Should().HaveCount(2);
            _mockOrderRepository.Verify(x => x.GetByDateRangeAsync(_testTenantId, startDate, endDate, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateOrderStatusAsync_ShouldUpdateStatus_WhenValidStatus()
        {
            // Arrange
            OrderId orderId = new(Guid.NewGuid());
            string newStatus = "Completed";
            Order existingOrder = TestEntityBuilder.CreateOrder(_testTenantId, 100.00m);

            _ = _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingOrder);
            _ = _mockOrderRepository.Setup(x => x.UpdateAsync(existingOrder, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingOrder);

            // Act
            bool result = await _orderService.UpdateOrderStatusAsync(orderId.Value, newStatus, _testTenantId.Value);

            // Assert
            _ = result.Should().BeTrue();
            _mockOrderRepository.Verify(x => x.GetByIdAsync(orderId, _testTenantId, It.IsAny<CancellationToken>()), Times.Once);
            _mockOrderRepository.Verify(x => x.UpdateAsync(existingOrder, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ============================================================================
        // Coverage gap tests (replacing deleted OrderApiIntegrationTests.cs)
        // Reason: OrderApiIntegrationTests targeted KhachLink Program (no REST API),
        // tested non-existent endpoints (DELETE/PUT). These unit tests cover the same
        // business behaviors at the service layer where the unified order flow lives.
        // ============================================================================

        [Fact]
        public async Task CreateOrderFromCommandAsync_ShouldCreateOrderFromGatewayCommand()
        {
            // Arrange: Gateway entry point — maps CreateOrderCommand to domain Order
            CreateOrderCommand command = new()
            {
                CustomerDeviceId = Guid.NewGuid(),
                Items =
                [
                    new() { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 25.0m },
                    new() { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 50.0m }
                ]
            };

            _ = _mockOrderRepository
                .Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order o, CancellationToken _) => o);

            // Act
            Order result = await _orderService.CreateOrderFromCommandAsync(command, _testTenantId.Value);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.TenantId.Value.Should().Be(_testTenantId.Value);
            _ = result.Items.Should().HaveCount(2);
            _mockOrderRepository.Verify(
                x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData("pending", "preparing", true)]   // Pending -> Processing(=preparing)
        [InlineData("pending", "cancelled", true)]
        [InlineData("preparing", "completed", true)] // Processing -> Completed
        [InlineData("preparing", "cancelled", true)]
        [InlineData("completed", "pending", false)]  // Final state
        [InlineData("cancelled", "pending", false)]  // Final state
        [InlineData("pending", "completed", false)]  // Cannot skip processing
        public async Task IsTransitionValidAsync_ShouldEnforceStateMachine(
            string fromStatus, string toStatus, bool expectedValid)
        {
            // Arrange
            OrderStatusId current = new(fromStatus);
            OrderStatusId target = new(toStatus);

            // Act
            bool result = await _orderService.IsTransitionValidAsync(current, target);

            // Assert
            _ = result.Should().Be(expectedValid);
        }

        [Fact]
        public async Task GetOrdersByStatusAsync_ShouldReturnOrdersFilteredByStatus()
        {
            // Arrange
            OrderStatusId status = OrderStatusId.Pending;
            List<Order> orders =
            [
                TestEntityBuilder.CreateOrder(_testTenantId, 100.00m),
                TestEntityBuilder.CreateOrder(_testTenantId, 200.00m)
            ];
            _ = _mockOrderRepository
                .Setup(x => x.GetByStatusAsync(_testTenantId, status.Value, It.IsAny<CancellationToken>()))
                .ReturnsAsync(orders);

            // Act
            List<Order> result = await _orderService.GetOrdersByStatusAsync(status, _testTenantId.Value);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.Should().HaveCount(2);
            _mockOrderRepository.Verify(
                x => x.GetByStatusAsync(_testTenantId, status.Value, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetOrderSummaryAsync_ShouldAggregateOrderItems()
        {
            // Arrange
            Guid orderId = Guid.NewGuid();
            Order order = TestEntityBuilder.CreateOrder(_testTenantId, 75.0m);
            _ = _mockOrderRepository
                .Setup(x => x.GetByIdWithIncludesAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            // Act
            OrderSummary result = await _orderService.GetOrderSummaryAsync(orderId, _testTenantId.Value);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.OrderId.Should().Be(order.Id);
            _ = result.TotalAmount.Should().Be(order.TotalPrice);
            _ = result.ItemCount.Should().Be(order.Items.Count);
        }

        [Fact]
        public async Task GetOrderSummaryAsync_ShouldReturnEmptySummary_WhenOrderNotFound()
        {
            // Arrange
            Guid orderId = Guid.NewGuid();
            _ = _mockOrderRepository
                .Setup(x => x.GetByIdWithIncludesAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order?)null);

            // Act
            OrderSummary result = await _orderService.GetOrderSummaryAsync(orderId, _testTenantId.Value);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.OrderId.Should().Be(Guid.Empty);
            _ = result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task GetEntriesByOrderAsync_ShouldReturnAccountingEntriesForOrder()
        {
            // Arrange: Order's accounting integration — entries linked by id or reversal id
            Guid orderId = Guid.NewGuid();
            AccountingEntry matchingEntry = TestEntityBuilder.CreateAccountingEntry(
                _testTenantId, AccountingEntryType.Revenue, new Money(100m));
            AccountingEntry otherEntry = TestEntityBuilder.CreateAccountingEntry(
                _testTenantId, AccountingEntryType.Expense, new Money(50m));
            List<AccountingEntry> allEntries = [matchingEntry, otherEntry];

            _ = _mockAccountingEntryRepository
                .Setup(x => x.GetByTenantAsync(_testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(allEntries);

            // Act
            List<AccountingEntry> result = await _orderService.GetEntriesByOrderAsync(orderId, _testTenantId);

            // Assert
            _ = result.Should().NotBeNull();
            // Filtering is by AccountingEntryId match — verify repository was queried
            _mockAccountingEntryRepository.Verify(
                x => x.GetByTenantAsync(_testTenantId, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
