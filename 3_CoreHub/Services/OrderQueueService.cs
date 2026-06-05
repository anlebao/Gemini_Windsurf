using VanAn.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Services
{
    public class OrderQueueService(
        IBackgroundTaskQueue taskQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderQueueService> logger) : IOrderQueueService, IHostedService
    {
        private readonly IBackgroundTaskQueue _taskQueue = taskQueue;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<OrderQueueService> _logger = logger;

        public async Task QueueOrderAsync(Order order)
        {
            _taskQueue.QueueBackgroundWorkItem(async (scope, token) =>
            {
                IOrderService orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

                _ = await orderService.CreateOrderAsync(order, order.TenantId.Value);
            });

            _logger.LogInformation("Order {OrderId} enqueued for background processing", order.Id);
        }

        public async Task<int> GetQueueCountAsync()
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            VanAnDbContext context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();

            return await context.Orders
                .CountAsync(o => o.Status == OrderStatusId.Pending);
        }

        public async Task EnqueueOrderAsync(Order order)
        {
            await QueueOrderAsync(order);
        }

        public async Task ProcessQueuedOrdersAsync(CancellationToken cancellationToken)
        {
            await ProcessQueueAsync(cancellationToken);
        }

        public async Task<List<Order>> GetQueuedOrdersAsync(Guid tenantId)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            VanAnDbContext context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();

            return await context.Orders
                .Where(o => EF.Property<Guid>(o, "TenantId") == tenantId &&
                           o.Status == OrderStatusId.Pending)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task ProcessQueueAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Func<IServiceScope, CancellationToken, Task> workItem = await _taskQueue.DequeueAsync(cancellationToken);

                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    await workItem(scope, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing background work item");
                }
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(() => ProcessQueueAsync(cancellationToken), cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Implementation for stopping the queue processing
            await Task.CompletedTask;
        }
    }
}
