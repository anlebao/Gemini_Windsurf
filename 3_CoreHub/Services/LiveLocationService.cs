using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Realtime Platform P2 (2026-09-17): generic implementation of <see cref="ILiveLocationService"/>.
/// Multi-tenancy: IgnoreQueryFilters() is deliberate — Gateway has no ambient tenant scope for
/// customer/staff pings; the caller states the TenantId and access is gated by
/// IRealtimeParticipantAuthorizer at the API/hub layer.
/// </summary>
public class LiveLocationService(
    IVanAnDbContext dbContext,
    ILogger<LiveLocationService> logger) : ILiveLocationService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<LiveLocationService> _logger = logger;

    public async Task<DeliveryTracking?> RecordPingAsync(
        TenantId tenantId,
        RealtimeSubjectType subjectType,
        Guid subjectId,
        Guid? trackerId,
        double lat,
        double lng,
        CancellationToken ct = default)
    {
        if (subjectId == Guid.Empty)
        {
            _logger.LogWarning("RecordPing: empty subjectId for {SubjectType}", subjectType);
            return null;
        }

        // Legacy delivery pings keep DeliveryTaskId populated (SubjectId == DeliveryTaskId) so
        // DeliveryWorkflowService.GetTrackingHistoryAsync and existing queries still match.
        var tracking = subjectType == RealtimeSubjectType.Delivery
            ? new DeliveryTracking(tenantId, subjectId, lat, lng)
            : new DeliveryTracking(tenantId, subjectType, subjectId, trackerId, lat, lng);

        _dbContext.DeliveryTrackings.Add(tracking);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("RecordPing: {SubjectType}/{SubjectId} → ({Lat}, {Lng}) by {TrackerId}",
            subjectType, subjectId, lat, lng, trackerId);

        return tracking;
    }

    public async Task<DeliveryTracking?> GetLatestAsync(RealtimeSubjectType subjectType, Guid subjectId, CancellationToken ct = default)
    {
        var subjectCode = subjectType.ToString();

        return await _dbContext.DeliveryTrackings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.SubjectType == subjectCode && t.SubjectId == subjectId)
            .OrderByDescending(t => t.RecordedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<DeliveryTracking>> GetHistoryAsync(
        RealtimeSubjectType subjectType,
        Guid subjectId,
        DateTime? since = null,
        int take = 200,
        CancellationToken ct = default)
    {
        if (take <= 0)
            return new List<DeliveryTracking>();

        var subjectCode = subjectType.ToString();

        var query = _dbContext.DeliveryTrackings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.SubjectType == subjectCode && t.SubjectId == subjectId);

        if (since.HasValue)
            query = query.Where(t => t.RecordedAt >= since.Value);

        return await query
            .OrderBy(t => t.RecordedAt)
            .Take(take)
            .ToListAsync(ct);
    }
}
