using Microsoft.EntityFrameworkCore;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Infra;

/// <summary>
/// Startup reconciliation (SRS v1.1 §3.4/§7): any non-terminal row older than the stale
/// threshold is a call orphaned by a service restart — mark it FAILED (SERVER_RESTART) and
/// emit one audit event per row. Bound: no call may remain non-terminal longer than the
/// max timeout (120 min).
/// </summary>
public static class VoiceReconciliation
{
    public static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    public static async Task<int> MarkStaleAsync(VoiceDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        var cutoff = nowUtc - StaleThreshold;
        var stale = await db.VoiceCalls
            .Where(c => CallStateMachine.ActiveStatusValues.Contains((int)c.Status) && c.StartedAt < cutoff)
            .ToListAsync(ct);

        foreach (var call in stale)
        {
            call.ForceTerminal(CallStatus.Failed, CallEndReason.ServerRestart, nowUtc);
            db.VoiceCallEvents.Add(new VoiceCallEvent(
                call.Id, "call.failed", actorUserId: null, nowUtc,
                $"{{\"reason\":\"{CallEndReason.ServerRestart}\"}}"));
        }

        if (stale.Count > 0) await db.SaveChangesAsync(ct);
        return stale.Count;
    }
}