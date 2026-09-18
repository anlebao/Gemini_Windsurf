namespace VanAn.Gateway.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): guest identity — the <c>customer_device_id</c> guid KhachLink
/// keeps in localStorage and sends with every checkout (stored as <c>Order.CustomerDeviceId</c>).
///
/// F3: transport is query string <c>customerDeviceId</c> first, header <c>X-Customer-Device-Id</c>
/// second. A browser WebSocket handshake cannot set custom headers, so query is the only way a
/// guest can authenticate a SignalR connection at all — the reason guests were HTTP-polling before.
///
/// This validator only proves the caller HOLDS a device guid; it does not prove the device owns any
/// particular subject. That is the authorizer's job (<c>IRealtimeParticipantAuthorizer</c> matches
/// the guid against <c>Order.CustomerDeviceId</c>). Splitting it this way keeps the device guid
/// unguessable (128-bit random) without giving the validator knowledge of every module's schema.
/// </summary>
public class DeviceTokenValidator : IRealtimeTokenValidator
{
    private const string QueryKey = "customerDeviceId";
    private const string HeaderKey = "X-Customer-Device-Id";

    public string Name => "DeviceToken";

    public Task<RealtimeIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var raw = RealtimeRequestReader.ReadCredential(httpContext, QueryKey, HeaderKey);

        if (Guid.TryParse(raw, out var deviceId) && deviceId != Guid.Empty)
            return Task.FromResult<RealtimeIdentity?>(new RealtimeIdentity(deviceId, RealtimeIdentityKind.Device));

        return Task.FromResult<RealtimeIdentity?>(null);
    }
}
