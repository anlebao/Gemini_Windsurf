using Microsoft.EntityFrameworkCore;
using VanAn.Voice.Domain;
using VanAn.Voice.Infra;

namespace VanAn.Voice.Api.Services;

public sealed record VoiceMetricsSummary(
    int TotalCalls,
    int TodayCalls,
    int ConnectedCalls,
    int RejectedCalls,
    int MissedCalls,
    int FailedCalls,
    int BusyCalls,
    int OfflineCalls,
    int MeteringDenied,
    int FeatureDisabled,
    int TurnRelayed,
    double AvgDurationSeconds,
    double TurnPercentage);

/// <summary>
/// Pilot/ops metrics (SRS v1.1 §14 observability): aggregate call statistics from
/// <c>voice_calls</c> + events — no per-call PII, no audio/SDP. Gate-1 baseline source.
/// </summary>
public sealed class VoiceMetricsService(VoiceDbContext db)
{
    public async Task<VoiceMetricsSummary> SummaryAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var today = nowUtc.Date;
        var calls = db.VoiceCalls;

        var total = await calls.CountAsync(ct);
        var todayCalls = await calls.CountAsync(c => c.StartedAt >= today, ct);
        var connected = await calls.CountAsync(c => c.ConnectionType != null, ct); // reached Connected
        var rejected = await calls.CountAsync(c => c.Status == CallStatus.Rejected, ct);
        var missed = await calls.CountAsync(c => c.Status == CallStatus.Missed, ct);
        var failed = await calls.CountAsync(c => c.Status == CallStatus.Failed, ct);
        var busy = await calls.CountAsync(c => c.Status == CallStatus.Busy, ct);
        var offline = await calls.CountAsync(c => c.Status == CallStatus.Offline, ct);
        var meteringDenied = await calls.CountAsync(c => c.EndReason == CallEndReason.MeteringDenied, ct);
        var featureDisabled = await calls.CountAsync(c => c.EndReason == CallEndReason.FeatureDisabled, ct);
        var turnRelayed = await calls.CountAsync(c => c.ConnectionType == "TURN", ct);

        var durations = await calls
            .Where(c => c.Status == CallStatus.Ended && c.DurationSeconds > 0)
            .Select(c => (int?)c.DurationSeconds)
            .ToListAsync(ct);
        var avgDuration = durations.Count > 0 ? durations.Average(d => d!.Value) : 0;

        var terminalWithType = await calls
            .Where(c => c.ConnectionType != null)
            .CountAsync(ct);

        return new VoiceMetricsSummary(
            total, todayCalls, connected, rejected, missed, failed, busy, offline,
            meteringDenied, featureDisabled, turnRelayed, avgDuration,
            terminalWithType > 0 ? turnRelayed * 100.0 / terminalWithType : 0);
    }
}