using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Realtime Platform P2 (2026-09-17): subject-agnostic live-location ping service.
/// Generic counterpart to the DeliveryTask-keyed path in
/// <see cref="IDeliveryWorkflowService.RecordLocationAsync"/> (kept for existing callers).
///
/// Pings are append-only (<see cref="DeliveryTracking"/> has no update methods).
/// For <see cref="RealtimeSubjectType.Delivery"/> the SubjectId is the DeliveryTask id and the
/// legacy DeliveryTaskId column is populated, so existing delivery queries keep working.
/// </summary>
public interface ILiveLocationService
{
    /// <summary>Append a GPS ping for a subject. Returns null when the subject/tenant is invalid.</summary>
    Task<DeliveryTracking?> RecordPingAsync(
        TenantId tenantId,
        RealtimeSubjectType subjectType,
        Guid subjectId,
        Guid? trackerId,
        double lat,
        double lng,
        CancellationToken ct = default);

    /// <summary>Most recent ping for a subject (across tenants), or null when none recorded.</summary>
    Task<DeliveryTracking?> GetLatestAsync(RealtimeSubjectType subjectType, Guid subjectId, CancellationToken ct = default);

    /// <summary>Ping history for a subject, oldest first, optionally bounded by a start time.</summary>
    Task<List<DeliveryTracking>> GetHistoryAsync(
        RealtimeSubjectType subjectType,
        Guid subjectId,
        DateTime? since = null,
        int take = 200,
        CancellationToken ct = default);
}
