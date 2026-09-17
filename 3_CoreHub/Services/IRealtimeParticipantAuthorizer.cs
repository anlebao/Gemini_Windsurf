using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Realtime Platform P2 (2026-09-17): decides whether an identity may join a realtime subject
/// (chat group or location group).
///
/// Each module implements this for its own subject and registers it with .NET keyed DI:
/// <code>
/// services.AddKeyedScoped&lt;IRealtimeParticipantAuthorizer, ShopRealtimeAuthorizer&gt;(RealtimeSubjectType.Shop);
/// services.AddKeyedScoped&lt;IRealtimeParticipantAuthorizer, OrderRealtimeAuthorizer&gt;(RealtimeSubjectType.Order);
/// </code>
/// Hubs/endpoints resolve the keyed service for the subject type and fall back to a deny when no
/// authorizer is registered — an unregistered subject is never accessible by default.
///
/// <paramref name="userId"/> is whatever identity the caller authenticated as: a customer id, a
/// staff user id, or — for guests — the <c>CustomerDeviceId</c> guid (see P1 D6).
/// </summary>
public interface IRealtimeParticipantAuthorizer
{
    Task<bool> CanAccessAsync(RealtimeSubjectType subjectType, Guid subjectId, Guid userId, CancellationToken ct = default);
}
