using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Realtime Platform P3 (2026-09-17): implementation of <see cref="IRealtimeSubjectResolver"/>.
///
/// Multi-tenancy: IgnoreQueryFilters() is deliberate — the subject id is the only input, and the
/// tenant is what we are trying to discover; there is no ambient tenant to filter on at the Gateway.
/// Access is gated separately by IRealtimeParticipantAuthorizer before any of this is reached.
/// </summary>
public class RealtimeSubjectResolver(
    IVanAnDbContext dbContext,
    ILogger<RealtimeSubjectResolver> logger) : IRealtimeSubjectResolver
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<RealtimeSubjectResolver> _logger = logger;

    public async Task<TenantId?> ResolveTenantAsync(
        RealtimeSubjectType subjectType,
        Guid subjectId,
        CancellationToken ct = default)
    {
        if (subjectId == Guid.Empty)
            return null;

        switch (subjectType)
        {
            case RealtimeSubjectType.Order:
                return await _dbContext.Orders
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(o => o.Id == subjectId)
                    .Select(o => (TenantId?)o.TenantId)
                    .FirstOrDefaultAsync(ct);

            case RealtimeSubjectType.Delivery:
                // SubjectId for a delivery ping is the DeliveryTask id (see LiveLocationService).
                return await _dbContext.DeliveryTasks
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(t => t.Id == subjectId)
                    .Select(t => (TenantId?)t.TenantId)
                    .FirstOrDefaultAsync(ct);

            case RealtimeSubjectType.Shop:
                // A shop chat is scoped to the tenant itself, so SubjectId IS the tenant id.
                // Confirm the tenant exists rather than trusting the caller-supplied guid.
                var exists = await _dbContext.Tenants
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(t => t.Id == new TenantId(subjectId), ct);

                return exists ? new TenantId(subjectId) : null;

            default:
                // Shipment/JobApplication/Ticket/Custom have no entity yet (R3) — a module that
                // introduces one must also extend this resolver, otherwise its pings have no tenant.
                _logger.LogDebug("RealtimeSubjectResolver: no tenant mapping for {SubjectType}", subjectType);
                return null;
        }
    }
}
