using VanAn.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Services;

public class OrderQueueService : IOrderQueueService, IHostedService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderQueueService> _logger;
    
    public OrderQueueService(
        IBackgroundTaskQueue taskQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderQueueService> logger)
    {
        _taskQueue = taskQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }
    
    public async Task QueueOrderAsync(Order order)
    {
        _taskQueue.QueueBackgroundWorkItem(async (scope, token) =>
        {
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            
            await orderService.CreateOrderAsync(order, order.TenantId.Value);
        });
        
        _logger.LogInformation("Order {OrderId} enqueued for background processing", order.Id);
    }
    
    public async Task<int> GetQueueCountAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        
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
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        
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
            var workItem = await _taskQueue.DequeueAsync(cancellationToken);
            
            try
            {
                using var scope = _scopeFactory.CreateScope();
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
