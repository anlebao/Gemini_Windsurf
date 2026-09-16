using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S2 (Sprint 2): Delivery workflow service — state machine transitions + GPS location recording.
/// Uses IVanAnDbContext directly (cross-tenant via IgnoreQueryFilters), same pattern as CommunityOrderService.
/// On Delivered → delegates Order status transition to IOrderWorkflowService.TransitionStatusAsync(orderId, "completed")
/// for unified state-machine validation + loyalty points + NATS Outbox sync + accounting entries.
/// Previously bypassed OrderWorkflowService (direct order.UpdateOrderStatus + SaveChanges) which skipped
/// loyalty points awarding, NATS sync to ShopERP SQLite, accounting entries, and referral commission.
/// </summary>
public class DeliveryWorkflowService(
    IVanAnDbContext dbContext,
    IOrderWorkflowService orderWorkflowService,
    ITenantProvider tenantProvider,
    ILogger<DeliveryWorkflowService> logger) : IDeliveryWorkflowService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IOrderWorkflowService _orderWorkflowService = orderWorkflowService;
    private readonly ITenantProvider _tenantProvider = tenantProvider;
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

        // Persist DeliveryTask status change first (PickedUp/OutForDelivery/Delivered/Failed).
        await _dbContext.SaveChangesAsync();

        // If Delivered → delegate Order status transition to OrderWorkflowService for unified flow:
        //   - State-machine validation (delivering → completed is valid)
        //   - ProcessLoyaltyPointsAsync (award loyalty points to customer)
        //   - HandleOrderCompletedAsync (accounting entries, referral commission, customer stats)
        //   - EnqueueOrderStatusChangedEventAsync (Outbox → NATS sync to ShopERP SQLite)
        //   - PublishOrderStatusChangedEventAsync (NATS push notification)
        // OrderWorkflowService uses its own transaction + repository, separate from DeliveryTask save above.
        // Set tenant context from DeliveryTask so OrderWorkflowService's multi-tenancy query filter
        // can find the order (customer-token auth doesn't set tenant context like JWT auth does).
        if (newStatus == DeliveryTaskStatus.Delivered)
        {
            _tenantProvider.SetTenant(task.TenantId.Value);
            Order? result = await _orderWorkflowService.TransitionStatusAsync(
                orderId,
                new OrderStatusId("completed"));
            if (result == null)
            {
                _logger.LogWarning(
                    "TransitionStatus: OrderWorkflowService.TransitionStatusAsync returned null for order {OrderId} (completed) — " +
                    "order may already be completed, transition invalid, or order not found. DeliveryTask {TaskId} was marked Delivered.",
                    orderId, task.Id);
            }
            else
            {
                _logger.LogInformation(
                    "TransitionStatus: Order {OrderId} → completed via OrderWorkflowService (DeliveryTask {TaskId} delivered)",
                    orderId, task.Id);
            }
        }

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
