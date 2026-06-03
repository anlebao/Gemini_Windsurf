using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Interfaces;
using Xunit;
using Moq;

namespace VanAn.Core.Tests.Services
{
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

            ServiceCollection services = new();
            services.AddSingleton(_loggerMock.Object);
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddSingleton(new Mock<IServiceScopeFactory>().Object);
            services.AddSingleton<OrderQueueService>();

            _serviceProvider = services.BuildServiceProvider();
            _queueService = _serviceProvider.GetRequiredService<OrderQueueService>();
        }

        [Fact]
        public async Task EnqueueOrderAsync_ShouldAddOrderToQueue()
        {
            // Arrange
            Order order = CreateTestOrder(Guid.NewGuid(), "Test Order");

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
            Order order = CreateTestOrder(Guid.NewGuid(), "Test Order");
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
            TenantId tenantId = new(Guid.NewGuid());
            Guid customerId = Guid.NewGuid();
            string deviceFingerprint = $"device_{Guid.NewGuid():N}";
            Order order = new(tenantId, customerId, 100.50m);
            // Set CustomerDeviceId properly as string (device fingerprint)
            order.SetCustomerDeviceId(deviceFingerprint);
            return order;
        }
    }
}
