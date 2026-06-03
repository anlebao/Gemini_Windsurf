using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Commands;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Repositories;
using VanAn.Core.Tests.TestInfrastructure;
using Xunit;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VanAn.Core.Tests.Services;

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
        var order = TestEntityBuilder.CreateOrder(_testTenantId, 100.00m);
        
        _mockOrderRepository.Setup(x => x.AddAsync(order, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _orderService.CreateOrderAsync(order, _testTenantId.Value);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(order);
        _mockOrderRepository.Verify(x => x.AddAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTodayOrderCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        _mockOrderRepository.Setup(x => x.GetCountByDateRangeAsync(_testTenantId, today, tomorrow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        var result = await _orderService.GetTodayOrderCountAsync(_testTenantId.Value);

        // Assert
        result.Should().Be(3);
        _mockOrderRepository.Verify(x => x.GetCountByDateRangeAsync(_testTenantId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ShouldReturnOrder_WhenOrderExists()
    {
        // Arrange
        var orderId = new OrderId(Guid.NewGuid());
        var expectedOrder = TestEntityBuilder.CreateOrder(_testTenantId, 150.00m);
        
        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, _testTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrder);

        // Act
        var result = await _orderService.GetOrderByIdAsync(orderId.Value, _testTenantId.Value);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedOrder);
        _mockOrderRepository.Verify(x => x.GetByIdAsync(orderId, _testTenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrdersByDateRangeAsync_ShouldReturnOrders_WhenOrdersExist()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var expectedOrders = new List<Order>
        {
            TestEntityBuilder.CreateOrder(_testTenantId, 100.00m),
            TestEntityBuilder.CreateOrder(_testTenantId, 200.00m)
        };
        
        _mockOrderRepository.Setup(x => x.GetByDateRangeAsync(_testTenantId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrders);

        // Act
        var result = await _orderService.GetOrdersByDateRangeAsync(_testTenantId.Value, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        _mockOrderRepository.Verify(x => x.GetByDateRangeAsync(_testTenantId, startDate, endDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldUpdateStatus_WhenValidStatus()
    {
        // Arrange
        var orderId = new OrderId(Guid.NewGuid());
        var newStatus = "Completed";
        var existingOrder = TestEntityBuilder.CreateOrder(_testTenantId, 100.00m);
        
        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId, _testTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);
        _mockOrderRepository.Setup(x => x.UpdateAsync(existingOrder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        // Act
        var result = await _orderService.UpdateOrderStatusAsync(orderId.Value, newStatus, _testTenantId.Value);

        // Assert
        result.Should().BeTrue();
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
        var command = new CreateOrderCommand
        {
            CustomerDeviceId = Guid.NewGuid(),
            Items = new List<OrderItemRequest>
            {
                new() { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 25.0m },
                new() { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 50.0m }
            }
        };

        _mockOrderRepository
            .Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order o, CancellationToken _) => o);

        // Act
        var result = await _orderService.CreateOrderFromCommandAsync(command, _testTenantId.Value);

        // Assert
        result.Should().NotBeNull();
        result.TenantId.Value.Should().Be(_testTenantId.Value);
        result.Items.Should().HaveCount(2);
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
        var current = new OrderStatusId(fromStatus);
        var target = new OrderStatusId(toStatus);

        // Act
        var result = await _orderService.IsTransitionValidAsync(current, target);

        // Assert
        result.Should().Be(expectedValid);
    }

    [Fact]
    public async Task GetOrdersByStatusAsync_ShouldReturnOrdersFilteredByStatus()
    {
        // Arrange
        var status = OrderStatusId.Pending;
        var orders = new List<Order>
        {
            TestEntityBuilder.CreateOrder(_testTenantId, 100.00m),
            TestEntityBuilder.CreateOrder(_testTenantId, 200.00m)
        };
        _mockOrderRepository
            .Setup(x => x.GetByStatusAsync(_testTenantId, status.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        // Act
        var result = await _orderService.GetOrdersByStatusAsync(status, _testTenantId.Value);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        _mockOrderRepository.Verify(
            x => x.GetByStatusAsync(_testTenantId, status.Value, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrderSummaryAsync_ShouldAggregateOrderItems()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = TestEntityBuilder.CreateOrder(_testTenantId, 75.0m);
        _mockOrderRepository
            .Setup(x => x.GetByIdWithIncludesAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _orderService.GetOrderSummaryAsync(orderId, _testTenantId.Value);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.TotalAmount.Should().Be(order.TotalPrice);
        result.ItemCount.Should().Be(order.Items.Count);
    }

    [Fact]
    public async Task GetOrderSummaryAsync_ShouldReturnEmptySummary_WhenOrderNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _mockOrderRepository
            .Setup(x => x.GetByIdWithIncludesAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        // Act
        var result = await _orderService.GetOrderSummaryAsync(orderId, _testTenantId.Value);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(Guid.Empty);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntriesByOrderAsync_ShouldReturnAccountingEntriesForOrder()
    {
        // Arrange: Order's accounting integration — entries linked by id or reversal id
        var orderId = Guid.NewGuid();
        var matchingEntry = TestEntityBuilder.CreateAccountingEntry(
            _testTenantId, AccountingEntryType.Revenue, new Money(100m));
        var otherEntry = TestEntityBuilder.CreateAccountingEntry(
            _testTenantId, AccountingEntryType.Expense, new Money(50m));
        var allEntries = new List<AccountingEntry> { matchingEntry, otherEntry };

        _mockAccountingEntryRepository
            .Setup(x => x.GetByTenantAsync(_testTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allEntries);

        // Act
        var result = await _orderService.GetEntriesByOrderAsync(orderId, _testTenantId);

        // Assert
        result.Should().NotBeNull();
        // Filtering is by AccountingEntryId match — verify repository was queried
        _mockAccountingEntryRepository.Verify(
            x => x.GetByTenantAsync(_testTenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
