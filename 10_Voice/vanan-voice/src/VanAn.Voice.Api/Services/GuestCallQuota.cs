using Microsoft.EntityFrameworkCore;
using VanAn.Voice.Domain;
using VanAn.Voice.Infra;

namespace VanAn.Voice.Api.Services;

/// <summary>
/// Anti-spam quota for guest (Device) callers: a device identity gets ONE free call —
/// the next attempt is denied with <see cref="CallEndReason.GuestCallLimit"/> so the
/// client can offer inline login. A device GUID is self-issued, so this is a soft
/// friction gate (the per-IP rate limit is the hard guard).
/// </summary>
public interface IGuestCallQuota
{
    /// <summary>True while the device has no prior real call attempt. Only calls that
    /// actually reached the receiver side count — denied calls (Status=Failed, e.g.
    /// INVALID_CONTEXT / METERING_DENIED) do not consume the free call.</summary>
    Task<bool> HasFreeCallAsync(Guid deviceUserId, CancellationToken ct = default);
}

public sealed class GuestCallQuota(VoiceDbContext db) : IGuestCallQuota
{
    public async Task<bool> HasFreeCallAsync(Guid deviceUserId, CancellationToken ct = default)
        => !await db.VoiceCalls.AnyAsync(
            c => c.CallerUserId == deviceUserId && c.Status != CallStatus.Failed, ct);
}
