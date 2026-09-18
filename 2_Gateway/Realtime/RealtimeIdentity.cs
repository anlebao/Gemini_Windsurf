using VanAn.Shared.Domain;

namespace VanAn.Gateway.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): how the caller authenticated against the realtime layer.
/// </summary>
public enum RealtimeIdentityKind
{
    /// <summary>Logged-in customer (X-Customer-Token / customerToken query, validated via ShopERP /me).</summary>
    Customer = 0,

    /// <summary>Guest (X-Customer-Device-Id / customerDeviceId query — the localStorage device guid).</summary>
    Device = 1,

    /// <summary>Staff (Bearer JWT issued by ShopERP).</summary>
    Staff = 2
}

/// <summary>
/// Realtime Platform P3 (2026-09-17): the identity a realtime hub/endpoint acts as.
///
/// For guests <see cref="UserId"/> IS the device guid — the same value stored in
/// <c>Order.CustomerDeviceId</c> and used as <c>Message.SenderId</c>/<c>DeliveryTracking.TrackerId</c>
/// (see P1 D6). Keeping one guid across all three means an authorizer never has to translate
/// between "who sent this" and "who may read this".
///
/// Realtime Platform P5 (2026-09-18): <see cref="TenantId"/> carries the staff JWT's
/// <c>tenant_id</c> claim — the shop side of a Shop conversation is a tenant, not a user, and the
/// authorizer needs this to answer "is this staff member the shop?". Customer/guest identities
/// have no tenant (null).
/// </summary>
public sealed record RealtimeIdentity(Guid UserId, RealtimeIdentityKind Kind, Guid? TenantId = null)
{
    public bool IsGuest => Kind == RealtimeIdentityKind.Device;

    /// <summary>Role code stamped on <see cref="ConversationParticipant"/> rows.</summary>
    public string RoleCode => Kind switch
    {
        RealtimeIdentityKind.Device => RealtimeParticipantRole.Guest,
        RealtimeIdentityKind.Staff => RealtimeParticipantRole.Shop,
        _ => RealtimeParticipantRole.Customer
    };
}
