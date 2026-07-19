using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Common;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Core.Tests.TestInfrastructure;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// Phase 3.5: Unit tests for MarkPaidAsync vs ConfirmPaymentAsync split.
    /// - MarkPaidAsync: sets status=Paid + enqueues Outbox event (if flag set). Does NOT create accounting entries.
    /// - ConfirmPaymentAsync (wrapper): calls MarkPaidAsync (no event) + GenerateAccountingEntriesAsync.
    /// </summary>
    public class OrderServiceMarkPaidTests : IDisposable
    {
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IAccountingService> _mockAccountingService;
        private readonly Mock<IHKDBookRepository> _mockHkdBookRepository;
        private readonly Mock<IAccountingEntryRepository> _mockAccountingEntryRepository;
        private readonly Mock<IProductRepository> _mockProductRepository;
        private readonly Mock<IOutboxRepository> _mockOutboxRepository;
        private readonly OrderService _orderService;
        private readonly TenantId _testTenantId = new(Guid.NewGuid());

        public OrderServiceMarkPaidTests()
        {
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockAccountingService = new Mock<IAccountingService>();
            _mockHkdBookRepository = new Mock<IHKDBookRepository>();
            _mockAccountingEntryRepository = new Mock<IAccountingEntryRepository>();
            _mockProductRepository = new Mock<IProductRepository>();
            _mockOutboxRepository = new Mock<IOutboxRepository>();

            _orderService = new OrderService(
                _mockOrderRepository.Object,
                _mockAccountingService.Object,
                _mockHkdBookRepository.Object,
                _mockAccountingEntryRepository.Object,
                new NullLogger<OrderService>(),
                productRepository: _mockProductRepository.Object,
                outboxRepository: _mockOutboxRepository.Object
            );
        }

        public void Dispose() { }

        [Fact]
        public async Task MarkPaidAsync_ShouldSetOrderStatusPaid_AndNotCreateAccountingEntries()
        {
            // Arrange
            Guid orderId = Guid.NewGuid();
            string transactionId = "TXN-MARK-001";
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            Order order = Order.Create(orderId, _testTenantId, null, [item]);

            _ = _mockOrderRepository.Setup(x => x.GetByIdAsync(It.IsAny<OrderId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
            _ = _mockOrderRepository.Setup(x => x.GetByIdWithIncludesAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            // Act
            await _orderService.MarkPaidAsync(orderId, _testTenantId.Value, transactionId, enqueuePaymentConfirmedEvent: false);

            // Assert: order status should be Paid
            _ = order.PaymentStatus.Should().Be("Paid");

            // Assert: NO accounting entries created (MarkPaidAsync does not generate entries)
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>()),
                Times.Never,
                "MarkPaidAsync must NOT create accounting entries — those are created by ShopERP PaymentConfirmedSubscriber");

            // Assert: NO Outbox event enqueued (flag was false)
            _mockOutboxRepository.Verify(x => x.EnqueueAsync(It.IsAny<OutboxEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MarkPaidAsync_WithEnqueueEvent_ShouldEnqueueOutboxEvent()
        {
            // Arrange
            Guid orderId = Guid.NewGuid();
            string transactionId = "TXN-MARK-002";
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            Order order = Order.Create(orderId, _testTenantId, null, [item]);

            _ = _mockOrderRepository.Setup(x => x.GetByIdAsync(It.IsAny<OrderId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            // Act
            await _orderService.MarkPaidAsync(orderId, _testTenantId.Value, transactionId, enqueuePaymentConfirmedEvent: true);

            // Assert: Outbox event enqueued
            _mockOutboxRepository.Verify(x => x.EnqueueAsync(It.Is<OutboxEvent>(e => e.EventType == "OrderPaymentConfirmed"), It.IsAny<CancellationToken>()), Times.Once);

            // Assert: still NO accounting entries
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>()),
                Times.Never);
        }

        [Fact]
        public async Task MarkPaidAsync_ShouldBeIdempotent_WhenOrderAlreadyPaid()
        {
            // Arrange
            Guid orderId = Guid.NewGuid();
            string transactionId = "TXN-MARK-003";
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            Order order = Order.Create(orderId, _testTenantId, null, [item]);
            order.ConfirmPayment("previous-txn", "Cash"); // already paid

            _ = _mockOrderRepository.Setup(x => x.GetByIdAsync(It.IsAny<OrderId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            // Act
            await _orderService.MarkPaidAsync(orderId, _testTenantId.Value, transactionId, enqueuePaymentConfirmedEvent: true);

            // Assert: NO Outbox event enqueued (idempotent — already paid)
            _mockOutboxRepository.Verify(x => x.EnqueueAsync(It.IsAny<OutboxEvent>(), It.IsAny<CancellationToken>()), Times.Never);

            // Assert: NO accounting entries
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>()),
                Times.Never);
        }

        [Fact]
        public async Task MarkPaidAsync_ShouldThrowKeyNotFound_WhenOrderNotFound()
        {
            // Arrange
            Guid orderId = Guid.NewGuid();
            _ = _mockOrderRepository.Setup(x => x.GetByIdAsync(It.IsAny<OrderId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order?)null);

            // Act + Assert
            _ = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _orderService.MarkPaidAsync(orderId, _testTenantId.Value, "TXN-404"));
        }

        [Fact]
        public async Task ConfirmPaymentAsync_Wrapper_ShouldCreateAccountingEntries()
        {
            // Arrange
            Guid orderId = Guid.NewGuid();
            string transactionId = "TXN-CONFIRM-001";
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            Order order = Order.Create(orderId, _testTenantId, null, [item]);

            _ = _mockOrderRepository.Setup(x => x.GetByIdAsync(It.IsAny<OrderId>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
            _ = _mockOrderRepository.Setup(x => x.GetByIdWithIncludesAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            // Act
            await _orderService.ConfirmPaymentAsync(orderId, _testTenantId.Value, transactionId);

            // Assert: order status should be Paid
            _ = order.PaymentStatus.Should().Be("Paid");

            // Assert: accounting entries SHOULD be created (ConfirmPaymentAsync wrapper)
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>()),
                Times.AtLeastOnce,
                "ConfirmPaymentAsync wrapper MUST create accounting entries (backward compat for POS)");

            // Assert: NO Outbox event enqueued (wrapper uses enqueuePaymentConfirmedEvent: false)
            _mockOutboxRepository.Verify(x => x.EnqueueAsync(It.IsAny<OutboxEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GenerateAccountingEntriesAsync_PublicMethod_ShouldCreateEntries()
        {
            // Arrange
            Guid orderId = Guid.NewGuid();
            OrderItem item = OrderItem.Create(Guid.NewGuid(), _testTenantId, orderId, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
            Order order = Order.Create(orderId, _testTenantId, null, [item]);

            // Act — call public method directly (Phase 3.5: made public for PaymentConfirmedSubscriber)
            await _orderService.GenerateAccountingEntriesAsync(order, _testTenantId);

            // Assert: accounting entries created
            _mockAccountingService.Verify(
                x => x.CreateRevenueEntryAsync(It.IsAny<TenantId>(), It.IsAny<AccountingPeriod>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IndustrySector?>()),
                Times.AtLeastOnce,
                "GenerateAccountingEntriesAsync (now public) must create revenue entries");
        }
    }
}
