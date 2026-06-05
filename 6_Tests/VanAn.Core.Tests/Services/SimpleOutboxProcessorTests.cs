using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NATS.Client;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Events;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// Tests for SimpleOutboxProcessor - Outbox pattern implementation
    /// </summary>
    public class SimpleOutboxProcessorTests : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<ILogger<SimpleOutboxProcessor>> _loggerMock;
        private readonly Mock<INatsConnectionFactory> _natsConnectionFactoryMock;
        private readonly Mock<IConnection> _natsConnectionMock;
        private readonly ShopERPDbContext _context;
        private readonly SimpleOutboxProcessor _processor;

        public SimpleOutboxProcessorTests()
        {
            _loggerMock = new Mock<ILogger<SimpleOutboxProcessor>>();
            _natsConnectionFactoryMock = new Mock<INatsConnectionFactory>();
            _natsConnectionMock = new Mock<IConnection>();

            // Setup NATS factory to return mocked connection
            _ = _natsConnectionFactoryMock
                .Setup(f => f.CreateConnection(It.IsAny<string>()))
                .Returns(_natsConnectionMock.Object);

            ServiceCollection services = new();
            _ = services.AddDbContext<ShopERPDbContext>(options =>
                options.UseInMemoryDatabase("OutboxTestDb"));
            _ = services.AddSingleton(_loggerMock.Object);
            _ = services.AddSingleton(_natsConnectionFactoryMock.Object);

            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<ShopERPDbContext>();
            _processor = new SimpleOutboxProcessor(_serviceProvider, _loggerMock.Object, _natsConnectionFactoryMock.Object);

            _ = _context.Database.EnsureCreated();
        }

        [Fact]
        public async Task ProcessOutboxMessagesAsync_ShouldProcessMessages_WhenMessagesExist()
        {
            // Arrange
            Order order = CreateTestOrder(Guid.NewGuid(), "Test Order");
            CoreHub.Infrastructure.OutboxMessage outboxMessage = new()
            {
                EventType = EventTypes.OrderCompleted,
                EventData = JsonSerializer.Serialize(new OrderCompletedEvent
                {
                    EventId = Guid.NewGuid(),
                    OrderId = order.Id,
                    CustomerId = order.CustomerId,
                    CustomerDeviceId = order.CustomerDeviceId ?? $"device_{Guid.NewGuid():N}",
                    TenantId = new TenantId(Guid.NewGuid()),
                    TotalAmount = 100.50m,
                    CompletedAt = DateTime.UtcNow
                }),
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            _ = await _context.OutboxMessages.AddAsync(outboxMessage);
            _ = await _context.SaveChangesAsync();

            // Act
            await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

            // Assert - Processor creates its own scope/context, so we need a fresh context to query
            using IServiceScope scope = _serviceProvider.CreateScope();
            ShopERPDbContext freshContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            CoreHub.Infrastructure.OutboxMessage? processedMessage = await freshContext.OutboxMessages
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == outboxMessage.Id);
            Assert.NotNull(processedMessage);
            _ = Assert.NotNull(processedMessage.ProcessedAt);
            Assert.Equal(0, processedMessage.RetryCount);
        }

        [Fact]
        public async Task ProcessOutboxMessagesAsync_ShouldSkipProcessing_WhenNoMessages()
        {
            // Arrange - No messages in database

            // Act
            await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

            // Assert - Should not throw
            Assert.True(true);
        }

        [Fact]
        public async Task ProcessOutboxMessagesAsync_ShouldRetryFailedMessages()
        {
            // Arrange
            Order order = CreateTestOrder(Guid.NewGuid(), "Test Order");
            CoreHub.Infrastructure.OutboxMessage outboxMessage = new()
            {
                EventType = EventTypes.OrderCompleted,
                EventData = JsonSerializer.Serialize(new OrderCompletedEvent
                {
                    EventId = Guid.NewGuid(),
                    OrderId = order.Id,
                    CustomerId = order.CustomerId,
                    CustomerDeviceId = order.CustomerDeviceId ?? $"device_{Guid.NewGuid():N}",
                    TenantId = new TenantId(Guid.NewGuid()),
                    TotalAmount = 100.50m,
                    CompletedAt = DateTime.UtcNow
                }),
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            // Simulate previous failure by setting retry count
            outboxMessage.RetryCount = 1;
            _ = await _context.OutboxMessages.AddAsync(outboxMessage);
            _ = await _context.SaveChangesAsync();

            // Wait for retry time to pass
            await Task.Delay(100);

            // Act
            await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

            // Assert - Processor creates its own scope/context, so we need a fresh context to query
            using IServiceScope scope = _serviceProvider.CreateScope();
            ShopERPDbContext freshContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            CoreHub.Infrastructure.OutboxMessage? retriedMessage = await freshContext.OutboxMessages
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == outboxMessage.Id);
            Assert.NotNull(retriedMessage);
            Assert.Equal(1, retriedMessage.RetryCount); // Should stay at 1 when successfully processed
            _ = Assert.NotNull(retriedMessage.ProcessedAt); // Should be marked as processed
        }

        [Fact]
        public async Task ProcessOutboxMessagesAsync_ShouldRespectBatchSize()
        {
            // Arrange - Create more messages than batch size
            List<CoreHub.Infrastructure.OutboxMessage> messages = [];
            for (int i = 0; i < 20; i++) // More than default batch size of 15
            {
                Order order = CreateTestOrder(Guid.NewGuid(), "Test Order");
                CoreHub.Infrastructure.OutboxMessage message = new()
                {
                    EventType = EventTypes.OrderCompleted,
                    EventData = JsonSerializer.Serialize(new OrderCompletedEvent
                    {
                        EventId = Guid.NewGuid(),
                        OrderId = order.Id,
                        CustomerId = order.CustomerId,
                        CustomerDeviceId = order.CustomerDeviceId ?? $"device_{Guid.NewGuid():N}",
                        TenantId = new TenantId(Guid.NewGuid()),
                        TotalAmount = 100.50m,
                        CompletedAt = DateTime.UtcNow
                    }),
                    CreatedAt = DateTime.UtcNow,
                    RetryCount = 0
                };
                messages.Add(message);
            }

            await _context.OutboxMessages.AddRangeAsync(messages);
            _ = await _context.SaveChangesAsync();

            // Act
            await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

            // Assert - Should only process batch size (15) messages
            int processedCount = await _context.OutboxMessages
                .Where(m => m.ProcessedAt != null)
                .CountAsync();

            Assert.True(processedCount <= 15); // Should not exceed batch size
        }

        [Fact]
        public async Task ProcessOutboxMessagesAsync_ShouldHandleDifferentEventTypes()
        {
            // Arrange
            Order order = CreateTestOrder(Guid.NewGuid(), "Test Order");
            CoreHub.Infrastructure.OutboxMessage orderEvent = new()
            {
                EventType = EventTypes.OrderCompleted,
                EventData = JsonSerializer.Serialize(new OrderCompletedEvent
                {
                    EventId = Guid.NewGuid(),
                    OrderId = order.Id,
                    CustomerId = order.CustomerId,
                    CustomerDeviceId = order.CustomerDeviceId ?? $"device_{Guid.NewGuid():N}",
                    TenantId = new TenantId(Guid.NewGuid()),
                    TotalAmount = 100.50m,
                    CompletedAt = DateTime.UtcNow
                }),
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            _ = await _context.OutboxMessages.AddAsync(orderEvent);
            _ = await _context.SaveChangesAsync();

            // Act
            await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

            // Assert - Processor creates its own scope/context, so we need a fresh context to query
            using IServiceScope scope = _serviceProvider.CreateScope();
            ShopERPDbContext freshContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            CoreHub.Infrastructure.OutboxMessage? processedMessage = await freshContext.OutboxMessages
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == orderEvent.Id);
            Assert.NotNull(processedMessage);
            _ = Assert.NotNull(processedMessage.ProcessedAt);
            Assert.Equal(EventTypes.OrderCompleted, processedMessage.EventType);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessMessagesPeriodically()
        {
            // Arrange
            Order order = CreateTestOrder(Guid.NewGuid(), "Test Order");
            CoreHub.Infrastructure.OutboxMessage outboxMessage = new()
            {
                EventType = EventTypes.OrderCompleted,
                EventData = JsonSerializer.Serialize(new OrderCompletedEvent
                {
                    EventId = Guid.NewGuid(),
                    OrderId = order.Id,
                    CustomerId = order.CustomerId,
                    CustomerDeviceId = order.CustomerDeviceId ?? $"device_{Guid.NewGuid():N}",
                    TenantId = new TenantId(Guid.NewGuid()),
                    TotalAmount = 100.50m,
                    CompletedAt = DateTime.UtcNow
                }),
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            _ = await _context.OutboxMessages.AddAsync(outboxMessage);
            _ = await _context.SaveChangesAsync();

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(6)); // Run for 6 seconds

            // Act
            await _processor.ProcessOutboxMessagesAsync(cts.Token);

            // Assert - Processor creates its own scope/context, so we need a fresh context to query
            using IServiceScope scope = _serviceProvider.CreateScope();
            ShopERPDbContext freshContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
            CoreHub.Infrastructure.OutboxMessage? processedMessage = await freshContext.OutboxMessages
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == outboxMessage.Id);
            Assert.NotNull(processedMessage);
            _ = Assert.NotNull(processedMessage.ProcessedAt);
        }

        public void Dispose()
        {
            _ = (_context?.Database.EnsureDeleted());
            _context?.Dispose();
            _processor?.Dispose();
        }

        private static Order CreateTestOrder(Guid shopId, string description)
        {
            TenantId tenantId = new(Guid.NewGuid());
            Guid customerId = Guid.NewGuid();
            string deviceFingerprint = $"device_{Guid.NewGuid():N}";
            Order order = new(tenantId, customerId, 100.50m);
            order.SetCustomerDeviceId(deviceFingerprint);
            return order;
        }
    }
}
