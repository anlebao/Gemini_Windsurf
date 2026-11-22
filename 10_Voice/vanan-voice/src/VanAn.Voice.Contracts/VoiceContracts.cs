namespace VanAn.Voice.Contracts;

// ---- Client → Server (invoked on /hubs/voice) ----

public sealed record StartCallRequest(Guid CallId, Guid TargetUserId, string? ContextType, string? ContextReferenceId, string? CallType = null);

public sealed record SdpDto(Guid CallId, string Sdp);

public sealed record IceCandidateDto(Guid CallId, string Candidate, string? SdpMid = null, int? SdpMLineIndex = null);

// ---- Server → Client (events) ----

public sealed record CallTimeoutConfig(
    int RingTimeoutSeconds,
    int NegotiationTimeoutSeconds,
    int IceTimeoutSeconds,
    int MaxDurationMinutes);

public sealed record CallCreatedDto(Guid CallId, string Status, CallTimeoutConfig TimeoutConfig);

public sealed record IncomingCallDto(
    Guid CallId,
    Guid CallerUserId,
    string CallerDisplayName,
    string? VerifiedContextLabel,
    CallTimeoutConfig TimeoutConfig,
    string? CallType = null);

public sealed record CallIdDto(Guid CallId);

public sealed record CallIdWithReasonDto(Guid CallId, string Reason);

public sealed record CallOfflineDto(Guid CallId, bool MissedCallAllowed);

public sealed record ReceiveSdpDto(Guid CallId, string Sdp);

public sealed record ReceiveIceCandidateDto(Guid CallId, string Candidate, string? SdpMid = null, int? SdpMLineIndex = null);

// Generic call-scoped UI event relay (e.g. name="camera", value="true"/"false").
// Opaque to the server — same philosophy as SDP/ICE: validate bounds + relay to
// the peer's connection, never inspect semantics.
public sealed record CallEventDto(Guid CallId, string Name, string? Value);

public sealed record ReceiveCallEventDto(Guid CallId, string Name, string? Value);