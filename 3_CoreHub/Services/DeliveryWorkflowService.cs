using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S2 (Sprint 2): Delivery workflow service — state machine transitions + GPS location recording.
/// Uses IVanAnDbContext directly (cross-tenant via IgnoreQueryFilters), same pattern as CommunityOrderService.
/// On Delivered → updates Order status to "completed" inline (OrderWorkflowService not registered in Gateway DI;
/// full loyalty/NATS/outbox flow handled by ShopERP sync).
/// </summary>
public class DeliveryWorkflowService(
    IVanAnDbContext dbContext,
    ILogger<DeliveryWorkflowService> logger) : IDeliveryWorkflowService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<DeliveryWorkflowService> _logger = logger;

    public async Task<DeliveryTask?> TransitionStatusAsync(Guid orderId, DeliveryTaskStatus newStatus, string? failureReason = null)
    {
        // Load the active DeliveryTask for this order (cross-tenant)
        var activeStatuses = new[] { DeliveryTaskStatus.Assigned, DeliveryTaskStatus.PickedUp, DeliveryTaskStatus.OutForDelivery };
        var task = await _dbContext.DeliveryTasks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(dt => dt.OrderId == orderId && activeStatuses.Contains(dt.Status));

        if (task == null)
        {
            _logger.LogWarning("TransitionStatus: No active DeliveryTask for order {OrderId}", orderId);
            return null;
        }

        // Call domain method (validates transition rules)
        switch (newStatus)
        {
            case DeliveryTaskStatus.PickedUp:
                task.MarkPickedUp();
                break;
            case DeliveryTaskStatus.OutForDelivery:
                task.MarkOutForDelivery();
                break;
            case DeliveryTaskStatus.Delivered:
                task.MarkDelivered();
                break;
            case DeliveryTaskStatus.Failed:
                task.MarkFailed(failureReason ?? "Unknown");
                break;
            default:
                _logger.LogWarning("TransitionStatus: Unsupported target status {Status} for task {TaskId}", newStatus, task.Id);
                return null;
        }

        // If Delivered → update Order status to "completed"
        if (newStatus == DeliveryTaskStatus.Delivered)
        {
            var order = await _dbContext.Orders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null)
            {
                order.UpdateOrderStatus(new OrderStatusId("completed"));
                _logger.LogInformation("TransitionStatus: Order {OrderId} → completed (DeliveryTask {TaskId} delivered)", orderId, task.Id);
            }
            else
            {
                _logger.LogWarning("TransitionStatus: DeliveryTask {TaskId} delivered but Order {OrderId} not found", task.Id, orderId);
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("TransitionStatus: DeliveryTask {TaskId} → {Status} (Order {OrderId})",
            task.Id, newStatus, orderId);

        return task;
    }

    public async Task RecordLocationAsync(Guid deliveryTaskId, double lat, double lng)
    {
        // Load task to get TenantId for the tracking record
        var task = await _dbContext.DeliveryTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(dt => dt.Id == deliveryTaskId);

        if (task == null)
        {
            _logger.LogWarning("RecordLocation: DeliveryTask {TaskId} not found", deliveryTaskId);
            return;
        }

        var tracking = new DeliveryTracking(task.TenantId, deliveryTaskId, lat, lng);
        _dbContext.DeliveryTrackings.Add(tracking);
        await _dbContext.SaveChangesAsync();

        _logger.LogDebug("RecordLocation: DeliveryTask {TaskId} → ({Lat}, {Lng}) at {RecordedAt}",
            deliveryTaskId, lat, lng, tracking.RecordedAt);
    }

    public async Task<List<DeliveryTracking>> GetTrackingHistoryAsync(Guid deliveryTaskId)
    {
        return await _dbContext.DeliveryTrackings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(dt => dt.DeliveryTaskId == deliveryTaskId)
            .OrderBy(dt => dt.RecordedAt)
            .ToListAsync();
    }
}
