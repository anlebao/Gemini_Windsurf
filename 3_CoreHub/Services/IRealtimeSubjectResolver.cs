using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Realtime Platform P3 (2026-09-17): maps an opaque <c>(subjectType, subjectId)</c> pair back to the
/// tenant that owns it.
///
/// <see cref="ILiveLocationService.RecordPingAsync"/> and
/// <see cref="IRealtimeMessagingService.EnsureConversationAsync"/> both need an explicit
/// <see cref="TenantId"/> — Gateway has no ambient tenant scope for a customer or a guest device,
/// so the tenant has to come from the subject itself. Without this, a generic ping could only be
/// stamped with a caller-supplied tenant, which would be a multi-tenancy hole.
///
/// Returns null for an unknown subject (caller turns that into 404/400), never a guessed tenant.
/// </summary>
public interface IRealtimeSubjectResolver
{
    Task<TenantId?> ResolveTenantAsync(
        RealtimeSubjectType subjectType,
        Guid subjectId,
        CancellationToken ct = default);
}
