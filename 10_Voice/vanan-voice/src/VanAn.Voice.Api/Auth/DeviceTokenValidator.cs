using VanAn.Voice.Domain;

namespace VanAn.Voice.Api.Auth;

/// <summary>
/// Guest (device-id) identity for zero-barrier calling: <c>customerDeviceId</c> — a
/// self-asserted device GUID, the SAME trust model as guest chat (Realtime accepts the
/// device GUID as the sender identity). Last in the validator chain — real credentials
/// (staff JWT / customer token) always win. A Device identity may only start calls
/// against a verified <c>shop</c>/<c>chat</c> context, and only while the tenant's
/// VoiceGuestCalling flag is on (sysadmin kill-switch per tenant, default ON).
/// </summary>
public sealed class DeviceTokenValidator : IVoiceTokenValidator
{
    public const string QueryKey = "customerDeviceId";
    public const string HeaderKey = "X-Customer-Device-Id";

    public string Name => "CustomerDevice";

    public Task<VoiceIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var raw = VoiceRequestReader.ReadCredential(httpContext, QueryKey, HeaderKey);
        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var deviceId) || deviceId == Guid.Empty)
            return Task.FromResult<VoiceIdentity?>(null);

        return Task.FromResult<VoiceIdentity?>(
            new VoiceIdentity(deviceId, VoiceIdentityKind.Device, DisplayName: "Khách"));
    }
}
