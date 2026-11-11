namespace VanAn.Voice.Domain;

/// <summary>
/// Call lifecycle states (SRS v1.1 §5). Persisted as their numeric value.
/// Terminal states never transition back to active states.
/// </summary>
public enum CallStatus
{
    Created = 0,
    Ringing = 1,
    Accepted = 2,
    Negotiating = 3,
    Connected = 4,
    Reconnecting = 5,
    Ended = 6,
    Rejected = 7,
    Cancelled = 8,
    Missed = 9,
    Busy = 10,
    Offline = 11,
    CallCollision = 12,
    Failed = 13
}