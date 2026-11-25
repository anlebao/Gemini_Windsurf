namespace VanAn.Voice.Domain;

/// <summary>
/// Identity of a caller connected to the VoiceHub — mirrors the Gateway Realtime pattern
/// (RealtimeIdentity) so the Voice Service stays attachable by contract. A customer has no
/// tenant claim; the call tenant is resolved from the verified context (SRS v1.1 O6).
/// </summary>
public enum VoiceIdentityKind
{
    Staff = 0,
    Customer = 1,
    /// <summary>Unauthenticated guest — a self-asserted device GUID (same trust model as
    /// guest chat). May only start calls against a verified <c>shop</c>/<c>chat</c> context,
    /// and only while the tenant's <c>VoiceGuestCalling</c> flag is on.</summary>
    Device = 2
}

public sealed record VoiceIdentity(Guid UserId, VoiceIdentityKind Kind, Guid? TenantId = null, string? DisplayName = null)
{
    public bool IsStaff => Kind == VoiceIdentityKind.Staff;
}