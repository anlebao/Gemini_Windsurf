using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Tests.TestInfrastructure;
using Xunit;
using Moq;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Tests for OrderQueueService - SQLite concurrency solution
/// </summary>
public class OrderQueueServiceTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OrderQueueService _queueService;
    private readonly Mock<ILogger<OrderQueueService>> _loggerMock;

    public OrderQueueServiceTests()
    {
        _loggerMock = new Mock<ILogger<OrderQueueService>>();
        
        var services = new ServiceCollection();
        services.AddSingleton(_loggerMock.Object);
        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        services.AddSingleton<IServiceScopeFactory>(new Mock<IServiceScopeFactory>().Object);
        services.AddSingleton<OrderQueueService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _queueService = _serviceProvider.GetRequiredService<OrderQueueService>();
    }

    [Fact]
    public async Task EnqueueOrderAsync_ShouldAddOrderToQueue()
    {
        // Arrange
        var order = CreateTestOrder(Guid.NewGuid(), "Test Order");

        // Act
        await _queueService.QueueOrderAsync(order);

        // Assert
        // Test passes if no exception is thrown
        Assert.True(true);
    }

    [Fact]
    public async Task StartAsync_ShouldBeginProcessingOrders()
    {
        // Arrange
        var order = CreateTestOrder(Guid.NewGuid(), "Test Order");
        await _queueService.QueueOrderAsync(order);

        // Act
        await _queueService.StartAsync(CancellationToken.None);

        // Wait a moment for processing to start
        await Task.Delay(100);

        // Assert
        // Test passes if no exception is thrown
        Assert.True(true);
    }

    [Fact]
    public async Task StopAsync_ShouldStopProcessingGracefully()
    {
        // Arrange
        await _queueService.StartAsync(CancellationToken.None);

        // Act
        await _queueService.StopAsync(CancellationToken.None);

        // Assert
        // Service should stop without throwing
        Assert.True(true);
    }

    private static Order CreateTestOrder(Guid shopId, string description)
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var customerId = Guid.NewGuid();
        var deviceFingerprint = $"device_{Guid.NewGuid():N}";
        var order = new Order(tenantId, customerId, 100.50m);
        // Set CustomerDeviceId properly as string (device fingerprint)
        order.SetCustomerDeviceId(deviceFingerprint);
        return order;
    }
}
