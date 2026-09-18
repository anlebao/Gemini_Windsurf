using VanAn.UI.Platform.Realtime;

namespace VanAn.UI.Platform.Core.Interfaces;

/// <summary>
/// Realtime Platform P4 (UI-2): subject-agnostic live-location client for UI Platform components.
///
/// Implementations talk to the Gateway generic surface (<c>/api/realtime/location/*</c>). The
/// tracker id is NOT a client parameter: the server stamps every ping with the caller's resolved
/// identity (customer id / staff id / guest device id), so a client can never spoof another user's
/// ping (see RealtimeController.RecordPing).
/// </summary>
public interface ILiveLocationClient
{
    /// <summary>GET /api/realtime/location/{subjectType}/{subjectId}/latest — latest ping (nulls when none).</summary>
    Task<RealtimeLocationResult> GetLatestAsync(
        string subjectType,
        Guid subjectId,
        string? customerToken,
        Guid? customerDeviceId,
        string? staffToken = null,
        CancellationToken ct = default);

    /// <summary>POST /api/realtime/location/ping — record the caller's position for a subject.</summary>
    Task<RealtimePingResult> RecordPingAsync(
        string subjectType,
        Guid subjectId,
        double lat,
        double lng,
        string? customerToken,
        Guid? customerDeviceId,
        string? staffToken = null,
        CancellationToken ct = default);
}
