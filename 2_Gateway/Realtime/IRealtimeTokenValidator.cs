namespace VanAn.Gateway.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): one authentication strategy for the realtime layer.
///
/// Three implementations cover the three identities the platform must serve —
/// <see cref="CustomerTokenValidator"/> (logged-in customer), <see cref="DeviceTokenValidator"/>
/// (guest) and <see cref="StaffJwtValidator"/> (staff). <see cref="RealtimeIdentityResolver"/>
/// runs them in order and returns the first identity that resolves.
///
/// Each validator returns null (never throws) when it does not apply — a missing header is a
/// normal outcome, not an error. Transport is deliberately not fixed here: browser WebSocket
/// handshakes cannot carry custom headers, so every validator reads the query string first and
/// falls back to the header (F3).
/// </summary>
public interface IRealtimeTokenValidator
{
    /// <summary>Diagnostic name used in logs.</summary>
    string Name { get; }

    Task<RealtimeIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default);
}
