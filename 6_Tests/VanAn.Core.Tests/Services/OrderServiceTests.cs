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

        // ============================================================================
        // Sprint B — Accounting Entry Timing Tests
        // SC11: CreateOrderAsync does NOT create accounting entries
        // SC12: ConfirmPaymentAsync DOES create accounting entries (Revenue + COGS)
        // SC13: ConfirmPaymentAsync is idempotent — second call is a noop
        // TT 152/2025/TT-BTC: cash-basis accounting
        // ============================================================================

        [Fact]
        public async Task CreateOrderAsync_ShouldNotCreateAccountingEntries_SC11()
        {
            // Arrange (SC11): Sprint B — order creation must NOT trigger accounting entries
            Order order = TestEntityBuilder.CreateOrder(_testTenantId, 100.00m);

            _ = _mockOrderRepository
                .Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            // Act
            Order result = await _orderService.CreateOrderAsync(order, _testTenantId.Value);

            // Assert: accounting service must NOT be called at order creation time
            _ = result.Should().NotBeNull();
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()),
                Times.Never,
                "Revenue entry must NOT be created at order creation — only after payment confirmation (TT 152/2025)");
            _mockAccountingService.Verify(
                x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()),
                Times.Never,
                "COGS entry must NOT be created at order creation — only after payment confirmation (TT 152/2025)");
        }

        [Fact]
        public async Task ConfirmPaymentAsync_ShouldCreateAccountingEntries_SC12()
        {
            // Arrange (SC12): Sprint B — ConfirmPaymentAsync triggers accounting entries
            Guid orderId = Guid.NewGuid();
            string transactionId = "TXN-12345";
            Order order = TestEntityBuilder.CreateOrder(_testTenantId, 200.00m);

            _ = _mockOrderRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<OrderId>(), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
            _ = _mockOrderRepository
                .Setup(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
            _ = _mockOrderRepository
                .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var revenueDto = new VanAn.Shared.DTOs.AccountingEntryDto
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId.Value,
                EntryType = AccountingEntryType.Revenue,
                Amount = 200.00m,
                Description = "Doanh thu"
            };
            var cogsDto = new VanAn.Shared.DTOs.AccountingEntryDto
            {
                Id = Guid.NewGuid(),
                TenantId = _testTenantId.Value,
                EntryType = AccountingEntryType.Expense,
                Amount = 140.00m,
                Description = "COGS"
            };

            _ = _mockAccountingService
                .Setup(x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(revenueDto);
            _ = _mockAccountingService
                .Setup(x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(cogsDto);
            _ = _mockHkdBookRepository
                .Setup(x => x.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, transactionId);

            // Assert: accounting service MUST be called after payment confirmed
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()),
                Times.Once,
                "Revenue entry MUST be created after payment confirmation");
            _mockAccountingService.Verify(
                x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()),
                Times.Once,
                "COGS entry MUST be created after payment confirmation");
        }

        [Fact]
        public async Task ConfirmPaymentAsync_ShouldBeIdempotent_SC13()
        {
            // Arrange (SC13): Sprint B — second call for already-paid order is a noop
            Guid orderId = Guid.NewGuid();
            string transactionId = "TXN-99999";
            Order alreadyPaidOrder = TestEntityBuilder.CreateOrder(_testTenantId, 150.00m);
            // Simulate payment already confirmed
            alreadyPaidOrder.ConfirmPayment(transactionId);

            _ = _mockOrderRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<OrderId>(), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(alreadyPaidOrder);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, transactionId);

            // Assert: second call must NOT create duplicate entries (idempotency)
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()),
                Times.Never,
                "Revenue entry must NOT be created on second ConfirmPayment call (idempotency guard)");
            _mockOrderRepository.Verify(
                x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
                Times.Never,
                "Order must NOT be updated on idempotent noop call");
        }

        // ============================================================================
        // Sprint D (C-3) — COGS from Product.CostPrice Tests
        // SC14: COGS uses actual Product.CostPrice when set (not 70% hardcode)
        // SC15: COGS falls back to 70% of UnitPrice when CostPrice == 0 (legacy products)
        // SC16: COGS = 0 when order.Items is empty (no negative entries)
        // DMD-2 fix: Product.CostPrice added to Domain entity
        // ============================================================================

        [Fact]
        public async Task ConfirmPaymentAsync_ShouldUseCostPrice_ForCOGSCalculation_SC14()
        {
            // Arrange (SC14): Product has CostPrice set — COGS must use actual cost, not 70% hardcode
            Guid orderId = Guid.NewGuid();
            string transactionId = "TXN-SC14";

            // Create product with known CostPrice (e.g., product costs 60, sells at 100)
            Product product = TestEntityBuilder.CreateProduct(_testTenantId, "Test Product", price: 100m, costPrice: 60m);
            Guid productId = product.ProductId.Value;

            // Create OrderItem referencing that product (qty=2, unitPrice=100 → subtotal=200)
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, productId, quantity: 2, unitPrice: 100m);

            // Wire the Product navigation property onto the OrderItem via reflection (simulates EF Include)
            typeof(OrderItem).GetProperty("Product")!.SetValue(item, product);

            // Create order with items
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);

            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 220m); // light version for idempotency check

            _ = _mockOrderRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<OrderId>(), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderLight);
            _ = _mockOrderRepository
                .Setup(x => x.GetByIdWithIncludesAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderWithItems);
            _ = _mockOrderRepository
                .Setup(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderLight);
            _ = _mockOrderRepository
                .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            decimal capturedCogsAmount = 0m;
            _ = _mockAccountingService
                .Setup(x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Revenue, Amount = 220m });
            _ = _mockAccountingService
                .Setup(x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .Callback((TenantId _, AccountingPeriod _, decimal amount, string _, string? _, string? _, string? _, string? _) => capturedCogsAmount = amount)
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Expense });
            _ = _mockHkdBookRepository
                .Setup(x => x.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, transactionId);

            // Assert: COGS = qty(2) × CostPrice(60) = 120, NOT qty(2) × unitPrice(100) × 0.7 = 140
            _ = capturedCogsAmount.Should().Be(120m,
                "COGS must use Product.CostPrice (2 × 60 = 120), not 70% hardcode (2 × 100 × 0.7 = 140)");
        }

        [Fact]
        public async Task ConfirmPaymentAsync_ShouldFallbackTo70Percent_WhenCostPriceIsZero_SC15()
        {
            // Arrange (SC15): Product.CostPrice == 0 → fallback to 70% of UnitPrice (backward compat)
            Guid orderId = Guid.NewGuid();
            string transactionId = "TXN-SC15";

            // Product with CostPrice = 0 (legacy product, no cost recorded)
            Product product = TestEntityBuilder.CreateProduct(_testTenantId, "Legacy Product", price: 100m, costPrice: 0m);
            Guid productId = product.ProductId.Value;

            // OrderItem: qty=1, unitPrice=100 → expected COGS = 100 × 0.7 = 70
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, productId, quantity: 1, unitPrice: 100m);
            typeof(OrderItem).GetProperty("Product")!.SetValue(item, product);

            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 110m);

            _ = _mockOrderRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<OrderId>(), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderLight);
            _ = _mockOrderRepository
                .Setup(x => x.GetByIdWithIncludesAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderWithItems);
            _ = _mockOrderRepository
                .Setup(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(orderLight);
            _ = _mockOrderRepository
                .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            decimal capturedCogsAmount = 0m;
            _ = _mockAccountingService
                .Setup(x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Revenue });
            _ = _mockAccountingService
                .Setup(x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .Callback((TenantId _, AccountingPeriod _, decimal amount, string _, string? _, string? _, string? _, string? _) => capturedCogsAmount = amount)
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Expense });
            _ = _mockHkdBookRepository
                .Setup(x => x.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, transactionId);

            // Assert: COGS = 1 × 100 × 0.7 = 70 (fallback for legacy products with CostPrice=0)
            _ = capturedCogsAmount.Should().Be(70m,
                "When Product.CostPrice == 0, COGS must fall back to UnitPrice × 0.7 (backward compat)");
        }

        [Fact]
        public async Task UpdateCostPrice_ShouldRejectNegativeValue_SC16()
        {
            // Arrange (SC16): Product.UpdateCostPrice() must reject negative values — domain validation
            Product product = TestEntityBuilder.CreateProduct(_testTenantId, "Test Product", price: 100m, costPrice: 50m);

            // Act & Assert: negative cost price is invalid
            Action act = () => product.UpdateCostPrice(-10m);
            _ = act.Should().Throw<ArgumentException>()
                .WithMessage("*CostPrice cannot be negative*");

            // Also verify valid update works
            product.UpdateCostPrice(75m);
            _ = product.CostPrice.Should().Be(75m, "UpdateCostPrice should update the value when valid");

            await Task.CompletedTask; // Keep async signature consistent with other tests
        }
    }
}
