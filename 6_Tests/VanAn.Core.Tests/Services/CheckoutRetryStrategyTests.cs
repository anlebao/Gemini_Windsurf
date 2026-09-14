using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Commands;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using Xunit;
using AccountingService = VanAn.CoreHub.Services.IAccountingService;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// C1 regression (2026-09-14): Gateway PG runs EnableRetryOnFailure (Phase 1 Scaling,
    /// commit 8f1144f3, deployed 2026-08-22) — EF Core rejects user-initiated transactions
    /// outside CreateExecutionStrategy. This broke ALL Gateway checkouts from 2026-08-22
    /// (last successful order 2026-08-21 14:41; error: "NpgsqlRetryingExecutionStrategy
    /// does not support user-initiated transactions"). These tests configure a RETRYING
    /// execution strategy (mirroring the Gateway) and verify the checkout + order-status
    /// transactional units run inside CreateExecutionStrategy.
    /// </summary>
    [Trait("Category", "Unit")]
    public class CheckoutRetryStrategyTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly VanAnDbContext _context;
        private readonly Guid _tenantId = Guid.NewGuid();

        public CheckoutRetryStrategyTests()
        {
            _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
            _connection.Open();
            var options = new DbContextOptionsBuilder<VanAnDbContext>()
                .UseSqlite(_connection)
                .ReplaceService<IExecutionStrategyFactory, TestRetryingStrategyFactory>()
                .Options;
            _context = new VanAnDbContext(options, new StubTenantProvider(_tenantId));
            _context.Database.EnsureCreated();
        }

        [Fact(DisplayName = "C1: charity checkout (UnitPrice=0, IsFree) creates order under retrying execution strategy")]
        public async Task CreateOrderFromCommandAsync_UnderRetryingStrategy_CreatesCharityOrder()
        {
            // Arrange — real repository + outbox on the retry-configured context (mirrors Gateway wiring)
            var orderRepo = new OrderRepository(_context, NullLogger<OrderRepository>.Instance);
            var outboxRepo = new OutboxRepository(_context);
            var service = new OrderService(
                orderRepo,
                new Mock<AccountingService>().Object,
                new Mock<IHKDBookRepository>().Object,
                new Mock<IAccountingEntryRepository>().Object,
                NullLogger<OrderService>.Instance,
                dbContext: _context,
                outboxRepository: outboxRepo);

            // Seed a real Product (OrderItem.ProductId has an FK to Products)
            var product = new Product(new TenantId(_tenantId), "com chay thap cam", "Test Description", 0m, "Test Category");
            _ = await _context.Products.AddAsync(product);
            _ = await _context.SaveChangesAsync();

            var command = new CreateOrderCommand
            {
                CustomerDeviceId = Guid.NewGuid(),
                CustomerName = "Retry Test",
                Items =
                [
                    new OrderItemRequest
                    {
                        ProductId = product.Id,
                        TenantId = _tenantId,
                        ProductName = "com chay thap cam",
                        VatRate = 0.10m,
                        Quantity = 2,
                        UnitPrice = 0m,
                        IsFree = true
                    }
                ]
            };

            // Act — before fix: InvalidOperationException "does not support user-initiated transactions"
            Order order = await service.CreateOrderFromCommandAsync(command, _tenantId, routingKey: null);

            // Assert — charity order: TotalAmount=0 persisted + outbox event enqueued atomically
            order.TotalAmount.Should().Be(0m);
            _context.Orders.IgnoreQueryFilters().Count().Should().Be(1);
            _context.OutboxMessages.IgnoreQueryFilters().Count().Should().Be(1);
        }

        [Fact(DisplayName = "C1: order status transition commits under retrying execution strategy")]
        public async Task TransitionStatusAsync_UnderRetryingStrategy_CommitsTransition()
        {
            // Arrange — seed a Pending order (real Product for the OrderItem FK)
            var tenantIdValue = new TenantId(_tenantId);
            var product = new Product(tenantIdValue, "Test Product", "Test Description", 100000m, "Test Category");
            _ = await _context.Products.AddAsync(product);
            _ = await _context.SaveChangesAsync();

            var orderItem = new OrderItem(tenantIdValue, Guid.Empty, product.Id, 1, 100000m, "Test Product", 0.10m);
            Order order = Order.Create(Guid.NewGuid(), tenantIdValue, null, [orderItem]);
            _ = await _context.Orders.AddAsync(order);
            _ = await _context.SaveChangesAsync();

            var orderRepo = new OrderRepository(_context, NullLogger<OrderRepository>.Instance);
            var outboxRepo = new OutboxRepository(_context);
            var workflow = new OrderWorkflowService(
                orderRepo,
                NullLogger<OrderWorkflowService>.Instance,
                new Mock<ISocialCampaignService>().Object,
                new Mock<ILoyaltyRewardsService>().Object,
                new Mock<ICustomerRepository>().Object,
                natsEventPublisher: null,
                outboxRepository: outboxRepo,
                dbContext: _context);

            // Act — pending → preparing (kitchen ON default). Before fix: same strategy exception.
            Order? result = await workflow.TransitionStatusAsync(order.Id, new OrderStatusId("preparing"));

            // Assert
            _ = result.Should().NotBeNull();
            result!.Status.Value.Should().Be("preparing");
        }

        /// <summary>
        /// Mirrors Gateway PG NpgsqlRetryingExecutionStrategy (Phase 1 Scaling): a retrying
        /// execution strategy makes EF Core reject user-initiated transactions that are not
        /// wrapped in CreateExecutionStrategy().ExecuteAsync. Never actually retries
        /// (ShouldRetryOn=false) — it only needs RetriesOnFailure=true to reproduce the
        /// Gateway failure mode on the SQLite test provider.
        /// </summary>
        private sealed class TestRetryingStrategyFactory(ICurrentDbContext currentContext)
            : IExecutionStrategyFactory
        {
            public IExecutionStrategy Create() => new TestRetryingExecutionStrategy(currentContext.Context);
        }

        private sealed class TestRetryingExecutionStrategy(DbContext context)
            : ExecutionStrategy(context, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(1))
        {
            protected override bool ShouldRetryOn(Exception? exception) => false;
        }

        private sealed class StubTenantProvider : ITenantProvider
        {
            public StubTenantProvider(Guid tenantId) => TenantId = tenantId;
            public Guid TenantId { get; }
            public string? CurrentUser => "test";
            public bool HasTenant => true;
            public void SetTenant(Guid tenantId) { }
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }
}
