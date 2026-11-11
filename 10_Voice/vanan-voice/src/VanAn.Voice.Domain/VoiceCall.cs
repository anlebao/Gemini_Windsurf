namespace VanAn.Voice.Domain;

/// <summary>
/// A call record (SRS v1.1 §7 <c>voice_calls</c>). Single identity: <see cref="Id"/> is the PK
/// and equals the client-supplied <c>callId</c> (UUIDv7) — idempotency key of StartCall.
/// Immutable after creation except for lifecycle transitions applied through
/// <see cref="Transition"/> / <see cref="ForceTerminal"/> (append-only semantics; history
/// lives in <see cref="VoiceCallEvent"/>).
/// </summary>
public sealed class VoiceCall
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CallerUserId { get; private set; }
    public Guid ReceiverUserId { get; private set; }
    public string SourceApplication { get; private set; } = string.Empty;
    public string? ContextType { get; private set; }
    public string? ContextReferenceId { get; private set; }
    public CallStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public int DurationSeconds { get; private set; }
    public string? ConnectionType { get; private set; } // "P2P" | "TURN" | null
    public string? EndReason { get; private set; }
    public string? CallerConnectionId { get; private set; }
    public string? ReceiverConnectionId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    protected VoiceCall() { } // EF

    public static VoiceCall Create(
        Guid callId, Guid tenantId, Guid callerUserId, Guid receiverUserId,
        string sourceApplication, string? contextType, string? contextReferenceId, DateTime now)
    {
        if (callId == Guid.Empty) throw new ArgumentException("callId cannot be empty.", nameof(callId));
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId cannot be empty.", nameof(tenantId));
        if (callerUserId == Guid.Empty) throw new ArgumentException("callerUserId cannot be empty.", nameof(callerUserId));
        if (receiverUserId == Guid.Empty) throw new ArgumentException("receiverUserId cannot be empty.", nameof(receiverUserId));
        if (callerUserId == receiverUserId) throw new ArgumentException("caller and receiver must differ.");

        return new VoiceCall
        {
            Id = callId,
            TenantId = tenantId,
            CallerUserId = callerUserId,
            ReceiverUserId = receiverUserId,
            SourceApplication = sourceApplication,
            ContextType = contextType,
            ContextReferenceId = contextReferenceId,
            Status = CallStatus.Created,
            StartedAt = now,
            CreatedAt = now
        };
    }

    /// <summary>
    /// Applies a state-machine transition. Idempotent: transitioning to the current status is a
    /// no-op returning true. Terminal timestamps/duration are stamped when the call ends.
    /// </summary>
    public bool Transition(CallStatus to, string? reason, DateTime now)
    {
        if (to == Status) return true; // idempotent replay
        if (!CallStateMachine.CanTransition(Status, to)) return false;

        Status = to;
        if (to == CallStatus.Accepted) AcceptedAt ??= now;
        EndCallBookkeeping(to, reason, now);
        return true;
    }

    /// <summary>
    /// Forces a terminal state outside the happy-path transition table: startup reconciliation
    /// (any non-terminal → FAILED/SERVER_RESTART) and validation-time terminal outcomes
    /// (Created → Offline/Busy/CallCollision/Failed).
    /// </summary>
    public void ForceTerminal(CallStatus to, string reason, DateTime now)
    {
        if (IsTerminal()) return; // already terminal — never rewrite history
        Status = to;
        EndCallBookkeeping(to, reason, now);
    }

    public void StampConnectionIds(string? callerConnectionId, string? receiverConnectionId)
    {
        CallerConnectionId = callerConnectionId;
        ReceiverConnectionId = receiverConnectionId;
    }

    /// <summary>Records whether the media path was direct P2P or TURN-relayed (set once).</summary>
    public void SetConnectionType(string connectionType)
        => ConnectionType ??= connectionType;

    public bool IsTerminal() => CallStateMachine.IsTerminal(Status);

    private void EndCallBookkeeping(CallStatus to, string? reason, DateTime now)
    {
        if (CallStateMachine.IsTerminal(to))
        {
            EndedAt = now;
            EndReason = reason ?? EndReason ?? to.ToString().ToUpperInvariant();
            DurationSeconds = (int)(now - StartedAt).TotalSeconds;
        }
    }
}