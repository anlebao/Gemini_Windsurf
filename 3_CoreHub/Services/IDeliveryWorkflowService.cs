using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S2 (Sprint 2): Delivery workflow service — state machine transitions + GPS location recording.
/// UC-05 (Delivery status) + UC-06 (GPS tracking).
/// </summary>
public interface IDeliveryWorkflowService
{
    /// <summary>
    /// Transition DeliveryTask status for the active task on the given order.
    /// Calls domain method (MarkPickedUp/MarkOutForDelivery/MarkDelivered/MarkFailed).
    /// If Delivered → calls OrderWorkflowService.TransitionStatusAsync(orderId, "completed").
    /// </summary>
    Task<DeliveryTask?> TransitionStatusAsync(Guid orderId, DeliveryTaskStatus newStatus, string? failureReason = null);

    /// <summary>
    /// Record a GPS location ping for the given DeliveryTask (append-only DeliveryTracking).
    /// </summary>
    Task RecordLocationAsync(Guid deliveryTaskId, double lat, double lng);

    /// <summary>
    /// Get tracking history for a DeliveryTask, sorted by RecordedAt ascending.
    /// </summary>
    Task<List<DeliveryTracking>> GetTrackingHistoryAsync(Guid deliveryTaskId);
}
