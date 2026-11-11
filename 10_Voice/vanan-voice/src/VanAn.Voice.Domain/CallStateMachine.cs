namespace VanAn.Voice.Domain;

/// <summary>
/// Pure transition table for the call state machine (SRS v1.1 §5).
/// Server state is authoritative; client state is provisional until confirmed.
/// </summary>
public static class CallStateMachine
{
    private static readonly HashSet<(CallStatus From, CallStatus To)> Allowed = new()
    {
        // Validation-time terminal outcomes (row created already terminal).
        (CallStatus.Created, CallStatus.Offline),
        (CallStatus.Created, CallStatus.Busy),
        (CallStatus.Created, CallStatus.CallCollision),
        (CallStatus.Created, CallStatus.Failed),        // METERING_DENIED / FEATURE_DISABLED / INVALID_CONTEXT / CALLER_BUSY

        // Invitation lifecycle.
        (CallStatus.Created, CallStatus.Ringing),
        (CallStatus.Ringing, CallStatus.Accepted),
        (CallStatus.Ringing, CallStatus.Rejected),
        (CallStatus.Ringing, CallStatus.Cancelled),
        (CallStatus.Ringing, CallStatus.Missed),        // ring timeout 30 s
        (CallStatus.Ringing, CallStatus.Busy),          // receiver became busy (race)
        (CallStatus.Ringing, CallStatus.Failed),        // participant vanished / server-side error

        // Negotiation.
        (CallStatus.Accepted, CallStatus.Negotiating),
        (CallStatus.Accepted, CallStatus.Failed),       // negotiation never completed (20 s)
        (CallStatus.Negotiating, CallStatus.Connected),
        (CallStatus.Negotiating, CallStatus.Failed),    // SDP/ICE timeout

        // Media.
        (CallStatus.Connected, CallStatus.Reconnecting),
        (CallStatus.Connected, CallStatus.Ended),       // either party, or MAX_DURATION
        (CallStatus.Connected, CallStatus.Failed),      // transport fatal error
        (CallStatus.Reconnecting, CallStatus.Connected),
        (CallStatus.Reconnecting, CallStatus.Failed)    // 10 s / 2 ICE restarts exceeded
    };

    public static bool CanTransition(CallStatus from, CallStatus to) => Allowed.Contains((from, to));

    public static bool IsTerminal(CallStatus status) => status switch
    {
        CallStatus.Ended or CallStatus.Rejected or CallStatus.Cancelled or CallStatus.Missed
            or CallStatus.Busy or CallStatus.Offline or CallStatus.CallCollision or CallStatus.Failed => true,
        _ => false
    };

    /// <summary>Statuses that count as an active call for FR-007 (one active call per user).</summary>
    public static readonly IReadOnlyList<int> ActiveStatusValues =
        new[] { 0, 1, 2, 3, 4, 5 }; // Created, Ringing, Accepted, Negotiating, Connected, Reconnecting
}