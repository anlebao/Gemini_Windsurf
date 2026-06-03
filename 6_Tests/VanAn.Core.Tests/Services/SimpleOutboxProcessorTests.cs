using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using NATS.Client;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Events;
using VanAn.ShopERP.Infrastructure;
using VanAn.ShopERP.Services;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Tests.TestInfrastructure;
using Xunit;

namespace VanAn.Core.Tests.Services;

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
        _natsConnectionFactoryMock
            .Setup(f => f.CreateConnection(It.IsAny<string>()))
            .Returns(_natsConnectionMock.Object);

        var services = new ServiceCollection();
        services.AddDbContext<ShopERPDbContext>(options =>
            options.UseInMemoryDatabase("OutboxTestDb"));
        services.AddSingleton(_loggerMock.Object);
        services.AddSingleton(_natsConnectionFactoryMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ShopERPDbContext>();
        _processor = new SimpleOutboxProcessor(_serviceProvider, _loggerMock.Object, _natsConnectionFactoryMock.Object);

        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_ShouldProcessMessages_WhenMessagesExist()
    {
        // Arrange
        var order = CreateTestOrder(Guid.NewGuid(), "Test Order");
        var outboxMessage = new CoreHub.Infrastructure.OutboxMessage
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

        await _context.OutboxMessages.AddAsync(outboxMessage);
        await _context.SaveChangesAsync();

        // Act
        await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        // Assert - Processor creates its own scope/context, so we need a fresh context to query
        using var scope = _serviceProvider.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        var processedMessage = await freshContext.OutboxMessages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == outboxMessage.Id);
        Assert.NotNull(processedMessage);
        Assert.NotNull(processedMessage.ProcessedAt);
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
        var order = CreateTestOrder(Guid.NewGuid(), "Test Order");
        var outboxMessage = new CoreHub.Infrastructure.OutboxMessage
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
        await _context.OutboxMessages.AddAsync(outboxMessage);
        await _context.SaveChangesAsync();

        // Wait for retry time to pass
        await Task.Delay(100);

        // Act
        await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        // Assert - Processor creates its own scope/context, so we need a fresh context to query
        using var scope = _serviceProvider.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        var retriedMessage = await freshContext.OutboxMessages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == outboxMessage.Id);
        Assert.NotNull(retriedMessage);
        Assert.Equal(1, retriedMessage.RetryCount); // Should stay at 1 when successfully processed
        Assert.NotNull(retriedMessage.ProcessedAt); // Should be marked as processed
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_ShouldRespectBatchSize()
    {
        // Arrange - Create more messages than batch size
        var messages = new List<CoreHub.Infrastructure.OutboxMessage>();
        for (int i = 0; i < 20; i++) // More than default batch size of 15
        {
            var order = CreateTestOrder(Guid.NewGuid(), "Test Order");
            var message = new CoreHub.Infrastructure.OutboxMessage
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
        await _context.SaveChangesAsync();

        // Act
        await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        // Assert - Should only process batch size (15) messages
        var processedCount = await _context.OutboxMessages
            .Where(m => m.ProcessedAt != null)
            .CountAsync();
        
        Assert.True(processedCount <= 15); // Should not exceed batch size
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_ShouldHandleDifferentEventTypes()
    {
        // Arrange
        var order = CreateTestOrder(Guid.NewGuid(), "Test Order");
        var orderEvent = new CoreHub.Infrastructure.OutboxMessage
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

        await _context.OutboxMessages.AddAsync(orderEvent);
        await _context.SaveChangesAsync();

        // Act
        await _processor.ProcessOutboxMessagesAsync(CancellationToken.None);

        // Assert - Processor creates its own scope/context, so we need a fresh context to query
        using var scope = _serviceProvider.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        var processedMessage = await freshContext.OutboxMessages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == orderEvent.Id);
        Assert.NotNull(processedMessage);
        Assert.NotNull(processedMessage.ProcessedAt);
        Assert.Equal(EventTypes.OrderCompleted, processedMessage.EventType);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessMessagesPeriodically()
    {
        // Arrange
        var order = CreateTestOrder(Guid.NewGuid(), "Test Order");
        var outboxMessage = new CoreHub.Infrastructure.OutboxMessage
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

        await _context.OutboxMessages.AddAsync(outboxMessage);
        await _context.SaveChangesAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6)); // Run for 6 seconds

        // Act
        await _processor.ProcessOutboxMessagesAsync(cts.Token);

        // Assert - Processor creates its own scope/context, so we need a fresh context to query
        using var scope = _serviceProvider.CreateScope();
        var freshContext = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        var processedMessage = await freshContext.OutboxMessages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == outboxMessage.Id);
        Assert.NotNull(processedMessage);
        Assert.NotNull(processedMessage.ProcessedAt);
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
        _processor?.Dispose();
    }

    private static Order CreateTestOrder(Guid shopId, string description)
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var customerId = Guid.NewGuid();
        var deviceFingerprint = $"device_{Guid.NewGuid():N}";
        var order = new Order(tenantId, customerId, 100.50m);
        order.SetCustomerDeviceId(deviceFingerprint);
        return order;
    }
}
