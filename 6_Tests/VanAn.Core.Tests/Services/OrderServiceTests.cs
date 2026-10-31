using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Commands;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Common;
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
        private readonly Mock<IProductRepository> _mockProductRepository;
        private readonly OrderService _orderService;
        private readonly TenantId _testTenantId = new(Guid.NewGuid());

        public OrderServiceTests()
        {
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockAccountingService = new Mock<IAccountingService>();
            _mockHkdBookRepository = new Mock<IHKDBookRepository>();
            _mockAccountingEntryRepository = new Mock<IAccountingEntryRepository>();
            _mockProductRepository = new Mock<IProductRepository>();

            _orderService = new OrderService(
                _mockOrderRepository.Object,
                _mockAccountingService.Object,
                _mockHkdBookRepository.Object,
                _mockAccountingEntryRepository.Object,
                new NullLogger<OrderService>(),
                productRepository: _mockProductRepository.Object
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
            // Arrange: Gateway entry point — maps CreateOrderCommand to domain Order.
            // RC-7: ProductRepository must return products for snapshot (otherwise KeyNotFoundException).
            Guid productAId = Guid.NewGuid();
            Guid productBId = Guid.NewGuid();
            Product productA = new(_testTenantId, "Product A", 25.0m, "Cat", 0m);
            typeof(Product).GetProperty("Id")!.SetValue(productA, productAId);
            typeof(Product).GetProperty("ProductId")!.SetValue(productA, new ProductId(productAId));
            Product productB = new(_testTenantId, "Product B", 50.0m, "Cat", 0m);
            typeof(Product).GetProperty("Id")!.SetValue(productB, productBId);
            typeof(Product).GetProperty("ProductId")!.SetValue(productB, new ProductId(productBId));

            _ = _mockProductRepository
                .Setup(x => x.GetByIdAsync(new ProductId(productAId), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(productA);
            _ = _mockProductRepository
                .Setup(x => x.GetByIdAsync(new ProductId(productBId), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(productB);

            CreateOrderCommand command = new()
            {
                CustomerDeviceId = Guid.NewGuid(),
                Items =
                [
                    new() { ProductId = productAId, Quantity = 2, UnitPrice = 25.0m },
                    new() { ProductId = productBId, Quantity = 1, UnitPrice = 50.0m }
                ]
            };

            _ = _mockOrderRepository
                .Setup(x => x.AddAsyncNoSave(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order o, CancellationToken _) => o);

            // RC-1 fix: CreateOrderFromCommandAsync now uses BeginTransactionAsync + AddAsyncNoSave
            _mockOrderRepository
                .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);

            // Act
            Order result = await _orderService.CreateOrderFromCommandAsync(command, _testTenantId.Value);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.TenantId.Value.Should().Be(_testTenantId.Value);
            _ = result.Items.Should().HaveCount(2);
            _mockOrderRepository.Verify(
                x => x.AddAsyncNoSave(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // ============================================================================
        // RC-7 tests: OrderItem must snapshot ProductName + VatRate from Product entity.
        // TT 152/2025/TT-BTC: VAT must come from server-side Product, not client claim.
        // ============================================================================

        [Fact]
        public async Task CreateOrderFromCommandAsync_ShouldSnapshotProductNameAndVatRateFromProduct()
        {
            // Arrange: Product with non-default VAT (5%) and explicit name.
            Guid productId = Guid.NewGuid();
            Product product = new(_testTenantId, "Cà phê đen", 25.0m, "Cà phê", 0m);
            typeof(Product).GetProperty("Id")!.SetValue(product, productId);
            typeof(Product).GetProperty("ProductId")!.SetValue(product, new ProductId(productId));
            // Set VatRate via the full constructor path — use Update to set 5%.
            product.Update("Cà phê đen", "", 25.0m, "Cà phê", true, null, 0.05m);

            _ = _mockProductRepository
                .Setup(x => x.GetByIdAsync(new ProductId(productId), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            CreateOrderCommand command = new()
            {
                CustomerDeviceId = Guid.NewGuid(),
                Items =
                [
                    new() { ProductId = productId, Quantity = 2, UnitPrice = 25.0m }
                ]
            };

            _ = _mockOrderRepository
                .Setup(x => x.AddAsyncNoSave(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order o, CancellationToken _) => o);
            _mockOrderRepository
                .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);

            // Act
            Order result = await _orderService.CreateOrderFromCommandAsync(command, _testTenantId.Value);

            // Assert: snapshot fields reflect the Product entity, not factory defaults.
            _ = result.Items.Should().HaveCount(1);
            OrderItem item = result.Items.First();
            _ = item.ProductName.Should().Be("Cà phê đen");
            _ = item.VatRate.Should().Be(0.05m);
            // Per-item VatAmount = SubTotal * VatRate = (2 * 25) * 0.05 = 2.5
            _ = item.VatAmount.Should().Be(2.5m);
            // Order total VAT = 2.5 (not 5.0 which the old 0.10m default would produce)
            _ = result.TotalVatAmount.Should().Be(2.5m);
        }

        [Fact]
        public async Task CreateOrderFromCommandAsync_ShouldThrowWhenProductNotFound()
        {
            // Arrange: ProductId that doesn't exist in repository.
            Guid missingProductId = Guid.NewGuid();
            _ = _mockProductRepository
                .Setup(x => x.GetByIdAsync(new ProductId(missingProductId), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            CreateOrderCommand command = new()
            {
                CustomerDeviceId = Guid.NewGuid(),
                Items =
                [
                    new() { ProductId = missingProductId, Quantity = 1, UnitPrice = 10.0m }
                ]
            };

            _mockOrderRepository
                .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);

            // Act + Assert: fail fast — no ghost "Unknown" stubs.
            _ = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _orderService.CreateOrderFromCommandAsync(command, _testTenantId.Value));
        }

        [Fact]
        public async Task CreateOrderFromCommandAsync_ShouldHandleMixedVatRates()
        {
            // Arrange: two products with different VAT rates (5% and 0%).
            Guid productAId = Guid.NewGuid();
            Guid productBId = Guid.NewGuid();
            Product productA = new(_testTenantId, "Trà sữa", 50.0m, "Trà", 0m);
            typeof(Product).GetProperty("Id")!.SetValue(productA, productAId);
            typeof(Product).GetProperty("ProductId")!.SetValue(productA, new ProductId(productAId));
            productA.Update("Trà sữa", "", 50.0m, "Trà", true, null, 0.05m);

            Product productB = new(_testTenantId, "Nước lọc", 10.0m, "Nước", 0m);
            typeof(Product).GetProperty("Id")!.SetValue(productB, productBId);
            typeof(Product).GetProperty("ProductId")!.SetValue(productB, new ProductId(productBId));
            productB.Update("Nước lọc", "", 10.0m, "Nước", true, null, 0.00m);

            _ = _mockProductRepository
                .Setup(x => x.GetByIdAsync(new ProductId(productAId), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(productA);
            _ = _mockProductRepository
                .Setup(x => x.GetByIdAsync(new ProductId(productBId), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(productB);

            CreateOrderCommand command = new()
            {
                CustomerDeviceId = Guid.NewGuid(),
                Items =
                [
                    new() { ProductId = productAId, Quantity = 1, UnitPrice = 50.0m }, // VAT 5% → 2.5
                    new() { ProductId = productBId, Quantity = 2, UnitPrice = 10.0m }  // VAT 0% → 0
                ]
            };

            _ = _mockOrderRepository
                .Setup(x => x.AddAsyncNoSave(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order o, CancellationToken _) => o);
            _mockOrderRepository
                .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);

            // Act
            Order result = await _orderService.CreateOrderFromCommandAsync(command, _testTenantId.Value);

            // Assert: per-item VAT correctly snapshot from Product.
            _ = result.Items.Should().HaveCount(2);
            List<OrderItem> itemList = result.Items.ToList();
            _ = itemList[0].ProductName.Should().Be("Trà sữa");
            _ = itemList[0].VatRate.Should().Be(0.05m);
            _ = itemList[0].VatAmount.Should().Be(2.5m);
            _ = itemList[1].ProductName.Should().Be("Nước lọc");
            _ = itemList[1].VatRate.Should().Be(0.00m);
            _ = itemList[1].VatAmount.Should().Be(0m);
            // Total VAT = 2.5 + 0 = 2.5
            _ = result.TotalVatAmount.Should().Be(2.5m);
        }

        // ============================================================================
        // Issue #195 follow-up (2026-10-29): cọc booking đã thu — DepositPaidAmount snapshot.
        // Khi "Thanh toán" cuối, số còn lại = Total − DepositPaidAmount (UI hiển thị).
        // ============================================================================

        [Fact]
        public async Task CreateOrderFromCommandAsync_ShouldSetDepositPaidAmount_FromCommand()
        {
            // Arrange: booking cọc 10.000 đã thu → command.DepositPaidAmount = 10.000.
            Guid productId = Guid.NewGuid();
            Product product = new(_testTenantId, "Massage 60m", 300_000m, "Booking", 0m);
            typeof(Product).GetProperty("Id")!.SetValue(product, productId);
            typeof(Product).GetProperty("ProductId")!.SetValue(product, new ProductId(productId));

            _ = _mockProductRepository
                .Setup(x => x.GetByIdAsync(new ProductId(productId), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            CreateOrderCommand command = new()
            {
                CustomerDeviceId = Guid.NewGuid(),
                DepositPaidAmount = 10_000m, // cọc booking đã thu
                Items = [new() { ProductId = productId, Quantity = 1, UnitPrice = 300_000m }]
            };

            _ = _mockOrderRepository
                .Setup(x => x.AddAsyncNoSave(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order o, CancellationToken _) => o);
            _mockOrderRepository
                .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);

            // Act
            Order result = await _orderService.CreateOrderFromCommandAsync(command, _testTenantId.Value);

            // Assert — snapshot cọc vào Order (UI "Còn lại = Total − DepositPaidAmount")
            _ = result.DepositPaidAmount.Should().Be(10_000m);
        }

        [Fact]
        public async Task CreateOrderFromCommandAsync_DefaultDepositPaidAmount_IsZero()
        {
            // Arrange: additive — command không có cọc (đơn thường / booking không cọc).
            Guid productId = Guid.NewGuid();
            Product product = new(_testTenantId, "Cà phê đen", 25.0m, "Đồ uống", 0m);
            typeof(Product).GetProperty("Id")!.SetValue(product, productId);
            typeof(Product).GetProperty("ProductId")!.SetValue(product, new ProductId(productId));

            _ = _mockProductRepository
                .Setup(x => x.GetByIdAsync(new ProductId(productId), _testTenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

            CreateOrderCommand command = new()
            {
                CustomerDeviceId = Guid.NewGuid(),
                Items = [new() { ProductId = productId, Quantity = 1, UnitPrice = 25.0m }]
            };

            _ = _mockOrderRepository
                .Setup(x => x.AddAsyncNoSave(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order o, CancellationToken _) => o);
            _mockOrderRepository
                .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);

            // Act
            Order result = await _orderService.CreateOrderFromCommandAsync(command, _testTenantId.Value);

            // Assert — backward compatible 100%
            _ = result.DepositPaidAmount.Should().Be(0m);
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
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never,
                "Revenue entry must NOT be created at order creation — only after payment confirmation (TT 152/2025)");
            _mockAccountingService.Verify(
                x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never,
                "COGS entry must NOT be created at order creation — only after payment confirmation (TT 152/2025)");
        }

        [Fact]
        public async Task ConfirmPaymentAsync_ShouldCreateAccountingEntries_SC12()
        {
            // Arrange (SC12): Sprint B — ConfirmPaymentAsync triggers accounting entries.
            // VAS Wave 0 (W0-T3): VAT split — CreateRevenueEntryAsync called Twice (511 net + 3331 VAT).
            Guid orderId = Guid.NewGuid();
            string transactionId = "TXN-12345";

            // Build order with items so SubTotal/TotalVatAmount are calculated by CalculateTotals().
            // item: qty=2, unitPrice=100 → SubTotal=200, VatAmount=20 (10% default), TotalAmount=220.
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 2, unitPrice: 100m);
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
                .Setup(x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(revenueDto);
            _ = _mockAccountingService
                .Setup(x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(cogsDto);
            _ = _mockHkdBookRepository
                .Setup(x => x.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, transactionId);

            // Assert: accounting service MUST be called after payment confirmed.
            // W0-T3: VAT split → CreateRevenueEntryAsync called Twice (511 net revenue + 3331 VAT liability).
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Exactly(2),
                "Revenue entry MUST be called twice after payment confirmation (511 net + 3331 VAT) when VAT > 0");
            _mockAccountingService.Verify(
                x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()),
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
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()),
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
            Guid productId = product.Id;

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
                .Setup(x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Revenue, Amount = 220m });
            _ = _mockAccountingService
                .Setup(x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((TenantId _, AccountingPeriod _, decimal amount, string _, string? _, string? _, string? _, string? _, IndustrySector? _, DateTime? _, string? _, string? _) => capturedCogsAmount = amount)
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
            Guid productId = product.Id;

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
                .Setup(x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Revenue });
            _ = _mockAccountingService
                .Setup(x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((TenantId _, AccountingPeriod _, decimal amount, string _, string? _, string? _, string? _, string? _, IndustrySector? _, DateTime? _, string? _, string? _) => capturedCogsAmount = amount)
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

        // ============================================================================
        // VAS Wave 0 — Order→Accounting Writer Fix Tests
        // SC17: VAT split — JournalEntry has 3 lines when VAT > 0 (debit cash, credit 511, credit 3331)
        // SC18: PaymentMethod mapping — CASH→111, VIETQR→112 (cash account in JE lines)
        // SC19: COGS Path A (AccountingEntry) == Path B (JournalEntry) — shared CalculateCogsAmount
        // SC20: AccountCode 632 (not 621) — COGS expense entry uses correct account
        // SC21: OrderDate used for AccountingPeriod (not UtcNow)
        // SC22: Discount net revenue — credit 511 = SubTotal - DiscountAmount
        // SC23: No VAT call when TotalVatAmount = 0 — CreateRevenueEntryAsync called Once
        // ============================================================================

        /// <summary>Helper: set Order.PaymentMethod via reflection (protected setter).</summary>
        private static void SetOrderPaymentMethod(Order order, string paymentMethod)
        {
            typeof(Order).GetProperty("PaymentMethod")!.SetValue(order, paymentMethod);
        }

        /// <summary>Helper: set Order.DiscountAmount via reflection (protected setter).</summary>
        private static void SetOrderDiscount(Order order, decimal discount)
        {
            typeof(Order).GetProperty("DiscountAmount")!.SetValue(order, discount);
        }

        /// <summary>Helper: set Order.OrderDate via reflection (protected setter).</summary>
        private static void SetOrderDate(Order order, DateTime orderDate)
        {
            typeof(Order).GetProperty("OrderDate")!.SetValue(order, orderDate);
        }

        /// <summary>Helper: wire up common mocks for ConfirmPaymentAsync-driven tests.</summary>
        private void SetupConfirmPaymentMocks(Order orderLight, Order orderWithItems, Guid orderId)
        {
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
            _ = _mockAccountingService
                .Setup(x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Revenue });
            _ = _mockAccountingService
                .Setup(x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Expense });
            _ = _mockHkdBookRepository
                .Setup(x => x.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public async Task ConfirmPaymentAsync_ShouldNotCreateDirectJournal_DoubleRevenueFix_SC17()
        {
            // Issue #195 (2026-10-29): double revenue bug — OrderService TRƯỚC đây tạo JournalEntry
            // TRỰC TIẾP (CreateRevenueEntryAsync + AddToBookAsync) NỮA, trong khi AccountingEntryService
            // (ManualEntryJournalBridge, P2 #4) đã tạo journal cho mọi entry → mỗi order paid sinh
            // 2 journal revenue → sổ HKD nhân đôi (booking 100.000 → sổ ghi 200.000).
            // Fix: OrderService CHỈ tạo AccountingEntry — bridge là single source cho JournalEntry.
            // Assert: AddToBookAsync KHÔNG được gọi (0 journal trực tiếp) + entry 511/3331/COGS đủ 1 lần.
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            // item: SubTotal=100, VatAmount=10 (10% default) → netRevenue=100, VAT=10.
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 110m);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-SC17");

            // Assert: KHÔNG journal trực tiếp từ OrderService (single source = bridge)
            _mockHkdBookRepository.Verify(
                x => x.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()),
                Times.Never,
                "OrderService must NOT create JournalEntry directly — bridge (AccountingEntryService) is the single journal source (double-revenue fix)");

            // Assert: AccountingEntries vẫn được tạo đúng 1 lần mỗi loại (511 + 3331 khi VAT>0 + COGS)
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Exactly(2),
                "VAT > 0 → 2 revenue entries (511 net + 3331 VAT) — bridge tạo journal cho từng entry");
            _mockAccountingService.Verify(
                x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Once,
                "COGS expense entry được tạo (bridge tạo journal COGS)");
        }

        [Theory]
        [InlineData("CASH")]
        [InlineData("VIETQR")]
        [InlineData("CREDIT_CARD")]
        [InlineData(null)]
        public async Task ConfirmPaymentAsync_ShouldNotCreateDirectJournal_ForAnyPaymentMethod_SC18(string? paymentMethod)
        {
            // Issue #195: PaymentMethod → cash account (111/112) TRƯỚC đây nằm trong journal trực tiếp
            // của OrderService — đường đó đã bị bỏ (double revenue fix). Journal giờ do bridge tạo
            // (Nợ 111/Có TK — ManualEntryJournalBridge, P2 #4 user chốt). Assert: mọi payment method
            // đều KHÔNG tạo journal trực tiếp từ OrderService (chống double journal).
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            if (paymentMethod != null)
                SetOrderPaymentMethod(orderWithItems, paymentMethod);
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 110m);
            if (paymentMethod != null)
                SetOrderPaymentMethod(orderLight, paymentMethod);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-SC18");

            // Assert — single journal source (bridge), không journal trực tiếp
            _mockHkdBookRepository.Verify(
                x => x.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()),
                Times.Never,
                $"PaymentMethod '{paymentMethod ?? "null"}' must NOT trigger direct JournalEntry creation in OrderService (bridge is single source)");
        }

        [Fact]
        public async Task ConfirmPaymentAsync_CogsEntry_CreatedViaAccountingService_SC19()
        {
            // Issue #195: Path B (JournalEntry trực tiếp) đã bỏ — COGS journal do bridge tạo từ
            // AccountingEntry (CreateExpenseEntryAsync). Assert: entry COGS = CostPrice × qty,
            // không có journal trực tiếp (chống double).
            Guid orderId = Guid.NewGuid();
            Product product = TestEntityBuilder.CreateProduct(_testTenantId, "Test", price: 100m, costPrice: 60m);
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, product.Id, quantity: 2, unitPrice: 100m);
            typeof(OrderItem).GetProperty("Product")!.SetValue(item, product);
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 220m);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            decimal capturedCogs = 0m;
            _ = _mockAccountingService
                .Setup(x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((TenantId _, AccountingPeriod _, decimal amount, string _, string? _, string? _, string? _, string? _, IndustrySector? _, DateTime? _, string? _, string? _) => capturedCogs = amount)
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Expense });

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-SC19");

            // Assert: COGS entry = 2 × CostPrice(60) = 120 (single source — bridge tạo journal từ entry này)
            _ = capturedCogs.Should().Be(120m, "COGS = 2 × CostPrice(60) = 120");
            _mockHkdBookRepository.Verify(
                x => x.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()),
                Times.Never,
                "OrderService không tạo JournalEntry COGS trực tiếp — bridge là single source");
        }

        [Fact]
        public async Task ConfirmPaymentAsync_ShouldUseAccount632_Not621_SC20()
        {
            // Arrange (SC20): W0-T5 — COGS expense entry uses account 632 (not 621).
            Guid orderId = Guid.NewGuid();
            Product product = TestEntityBuilder.CreateProduct(_testTenantId, "Test", price: 100m, costPrice: 60m);
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, product.Id, quantity: 1, unitPrice: 100m);
            typeof(OrderItem).GetProperty("Product")!.SetValue(item, product);
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 110m);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            string? capturedAccountCode = null;
            _ = _mockAccountingService
                .Setup(x => x.CreateExpenseEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((TenantId _, AccountingPeriod _, decimal _, string _, string? accountCode, string? _, string? _, string? _, IndustrySector? _, DateTime? _, string? _, string? _) => capturedAccountCode = accountCode)
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Expense });

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-SC20");

            // Assert: accountCode = 632 (Giá vốn hàng bán), NOT 621.
            _ = capturedAccountCode.Should().Be("632", "COGS must use account 632 (Giá vốn hàng bán), not 621");
        }

        [Fact]
        public async Task ConfirmPaymentAsync_ShouldUseOrderDate_ForPeriod_SC21()
        {
            // Arrange (SC21): W0-T6 — AccountingPeriod derived from OrderDate, not UtcNow.
            Guid orderId = Guid.NewGuid();
            DateTime fixedOrderDate = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            SetOrderDate(orderWithItems, fixedOrderDate);
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 110m);
            SetOrderDate(orderLight, fixedOrderDate);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            AccountingPeriod? capturedPeriod = null;
            _ = _mockAccountingService
                .Setup(x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((TenantId _, AccountingPeriod period, decimal _, string _, string? _, string? _, IndustrySector? _, DateTime? _, string? _, string? _) => capturedPeriod = period)
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Revenue });

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-SC21");

            // Assert: period year/month = OrderDate year/month (2026/03), not UtcNow.
            _ = capturedPeriod.Should().NotBeNull();
            _ = capturedPeriod!.Year.Should().Be(2026);
            _ = capturedPeriod.Month.Should().Be(3);
        }

        [Fact]
        public async Task ConfirmPaymentAsync_ShouldApplyDiscountAsNetRevenue_SC22()
        {
            // Arrange (SC22): W0-T8 — Discount reduces revenue (net approach).
            // SubTotal=200, DiscountAmount=50 → netRevenue=150. VAT on discounted: 150×10%=15. TotalAmount=165.
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 2, unitPrice: 100m);
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            SetOrderDiscount(orderWithItems, 50m);
            orderWithItems.CalculateTotals(); // recalc with discount
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, orderWithItems.TotalAmount);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            List<decimal> capturedRevenueAmounts = [];
            _ = _mockAccountingService
                .Setup(x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((TenantId _, AccountingPeriod _, decimal amount, string _, string? _, string? _, IndustrySector? _, DateTime? _, string? _, string? _) => capturedRevenueAmounts.Add(amount))
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Revenue });

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-SC22");

            // Assert: 511 entry amount = netRevenue = SubTotal - Discount = 200 - 50 = 150.
            _ = capturedRevenueAmounts.Should().Contain(150m,
                "Net revenue (511) must be SubTotal(200) - DiscountAmount(50) = 150");
        }

        [Fact]
        public async Task ConfirmPaymentAsync_NoVatCall_WhenTotalVatZero_SC23()
        {
            // Arrange (SC23): W0-T3 — when TotalVatAmount = 0, only 1 CreateRevenueEntryAsync call (no 3331).
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            // Force VatRate = 0 via reflection.
            typeof(OrderItem).GetProperty("VatRate")!.SetValue(item, 0m);
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            // Order.Create calls CalculateTotals, but VatRate=0 → TotalVatAmount=0.
            // Recalculate to be safe (CalculateTotals reads item.VatRate which we just set).
            orderWithItems.CalculateTotals();
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, orderWithItems.TotalAmount);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-SC23");

            // Assert: CreateRevenueEntryAsync called Once (only 511, no 3331 since VAT=0).
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Once,
                "When TotalVatAmount = 0, only 1 revenue call (511 net) — no 3331 VAT liability call");
        }

        // ============================================================================
        // Issue #195 follow-up (2026-10-29, user approve — deposit offset 3387 làm luôn):
        // order booking có cọc đã thu (DepositPaidAmount>0) → thanh toán cuối:
        //   revenue cash = netRevenue − cọc (Nợ 111/Có 511 — tiền thực nhận)
        //   offset       = cọc (Nợ 3387/Có 511 — cọc chuyển thành doanh thu, liability cleared)
        // Tổng doanh thu vẫn = netRevenue; VAT full — không đổi.
        // ============================================================================

        [Fact]
        public async Task ConfirmPaymentAsync_DepositOrder_RevenueReducedAndOffsetEntryCreated()
        {
            // Arrange: booking 100.000, cọc 10.000 đã thu → DepositPaidAmount=10.000.
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100_000m);
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            orderWithItems.SetDepositPaidAmount(10_000m); // cọc đã thu (snapshot)
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 110_000m);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            // Setup CreateDepositOffsetEntryAsync (Moq mặc định trả null → NRE nếu không setup)
            _ = _mockAccountingService
                .Setup(x => x.CreateDepositOffsetEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.DepositOffset, Amount = 10_000m });

            List<decimal> capturedRevenueAmounts = [];
            _ = _mockAccountingService
                .Setup(x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((TenantId _, AccountingPeriod _, decimal amount, string _, string? _, string? _, IndustrySector? _, DateTime? _, string? _, string? _) => capturedRevenueAmounts.Add(amount))
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.Revenue });

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-DEP");

            // Assert: revenue cash = 100.000 − 10.000 = 90.000 (511) + VAT 10.000 (3331) — không còn full 100.000
            _ = capturedRevenueAmounts.Should().Contain(90_000m, "Revenue cash (511) = netRevenue(100.000) − DepositPaidAmount(10.000) = 90.000");
            _ = capturedRevenueAmounts.Should().NotContain(100_000m, "Không ghi full 100.000 khi có cọc — cọc 10.000 đã thu trước");

            // Assert: offset entry 10.000 (Nợ 3387/Có 511 — cọc chuyển thành doanh thu)
            _mockAccountingService.Verify(
                x => x.CreateDepositOffsetEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), 10_000m, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()),
                Times.Once,
                "Deposit offset entry = DepositPaidAmount (10.000) — Nợ 3387/Có 511");
        }

        [Fact]
        public async Task ConfirmPaymentAsync_NonDepositOrder_NoOffsetEntry()
        {
            // Arrange: đơn thường (không cọc) → KHÔNG tạo offset entry (backward compat 100%).
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 110m);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            _ = _mockAccountingService
                .Setup(x => x.CreateDepositOffsetEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new VanAn.Shared.DTOs.AccountingEntryDto { Id = Guid.NewGuid(), TenantId = _testTenantId.Value, EntryType = AccountingEntryType.DepositOffset });

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-NDEP");

            // Assert: KHÔNG offset entry cho đơn không cọc
            _mockAccountingService.Verify(
                x => x.CreateDepositOffsetEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>(), It.IsAny<DateTime?>()),
                Times.Never,
                "Đơn không có cọc → không tạo deposit offset entry");
        }

        // ============================================================================
        // Issue #195 (2026-10-29) — DOUBLE REVENUE journal regression guard.
        // Bug production: booking 100.000 (cọc 10.000) → sổ HKD ghi 200.000.
        // Root cause: GenerateAccountingEntriesAsync tạo journal 2 ĐƯỜNG — (1) journal TRỰC TIẾP
        // (CreateRevenueEntryAsync + AddToBookAsync + CreateCOGSEntryAsync, ref "Order"), (2) journal
        // của bridge (AccountingEntryService → ManualEntryJournalBridge, ref "ManualEntry") —
        // mỗi order paid sinh 2 journal revenue → sổ HKD nhân đôi.
        // Fix: bỏ journal trực tiếp — bridge là single source cho JournalEntry.
        // ============================================================================

        [Fact]
        public async Task ConfirmPaymentAsync_ShouldNotCallAddToBook_DoubleRevenueFix_W1()
        {
            // Arrange: order with 1 item — VAT=0 to keep the call structure minimal.
            // item: qty=1, unitPrice=100 → SubTotal=100, VatAmount=0, netRevenue=100, COGS=70 (70% fallback).
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 100m);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-W1");

            // Assert: AddToBookAsync NEVER — mọi journal đều do bridge tạo (1 bộ/order, không double).
            // BEFORE fix: 2 lần (revenue + COGS) cộng với bridge → 2 journal revenue + 2 COGS.
            // AFTER fix: 0 lần — OrderService không còn tạo journal trực tiếp.
            _mockHkdBookRepository.Verify(
                x => x.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()),
                Times.Never,
                "AddToBookAsync must NEVER be called by OrderService — bridge (AccountingEntryService) is the single journal source (double-revenue fix)");
        }

        [Fact]
        public async Task ConfirmPaymentAsync_NoJournalEntry_AddedToAnyBook_W2()
        {
            // Issue #195: W2 giữ mục đích chống trùng lặp journal — sau fix, OrderService không tạo
            // journal cho bất kỳ book type nào (S2b/S2c/S2d) — bridge xử lý hoàn toàn.
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            Order orderWithItems = Order.Create(orderId, _testTenantId, null, [item]);
            Order orderLight = TestEntityBuilder.CreateOrder(_testTenantId, 110m);
            SetupConfirmPaymentMocks(orderLight, orderWithItems, orderId);

            // Capture each JournalEntry instance passed to AddToBookAsync (expect none)
            List<JournalEntry> capturedEntries = [];
            _ = _mockHkdBookRepository
                .Setup(x => x.AddToBookAsync(It.IsAny<JournalEntry>(), It.IsAny<AccountingBookType>(), It.IsAny<CancellationToken>()))
                .Callback((JournalEntry je, AccountingBookType _, CancellationToken _) => capturedEntries.Add(je))
                .Returns(Task.CompletedTask);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, "TXN-W2");

            // Assert: KHÔNG journal trực tiếp nào được tạo — bridge là single source (0 double).
            // Before fix: 2 JournalEntry (revenue + COGS) được thêm thẳng — cộng với bridge = double.
            _ = capturedEntries.Should().BeEmpty(
                "OrderService phải tạo 0 JournalEntry trực tiếp — journal do bridge tạo từ AccountingEntry (double-revenue fix)");
        }
    }
}
