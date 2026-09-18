using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): resolves the keyed <see cref="IRealtimeParticipantAuthorizer"/>
/// for a subject type, defaulting to DENY when the module has not registered one.
///
/// Default-deny is the security property that makes the platform safe to extend: a new subject type
/// is inaccessible until its module explicitly ships an authorizer, so adding an enum value can
/// never silently expose data.
/// </summary>
public static class RealtimeAuthorizerLookup
{
    public static async Task<bool> CanAccessAsync(
        IServiceProvider services,
        RealtimeSubjectType subjectType,
        Guid subjectId,
        Guid userId,
        CancellationToken ct = default)
        => await CanAccessAsync(services, subjectType, subjectId, userId, tenantId: null, ct);

    /// <summary>
    /// Realtime Platform P5 (2026-09-18): tenant-aware overload — hubs/endpoints pass the caller's
    /// <see cref="RealtimeIdentity.TenantId"/> so the Shop authorizer can match staff to their shop.
    /// </summary>
    public static async Task<bool> CanAccessAsync(
        IServiceProvider services,
        RealtimeSubjectType subjectType,
        Guid subjectId,
        Guid userId,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        var authorizer = services.GetKeyedService<IRealtimeParticipantAuthorizer>(subjectType);
        if (authorizer == null)
            return false;

        return await authorizer.CanAccessAsync(subjectType, subjectId, userId, tenantId, ct);
    }
}
