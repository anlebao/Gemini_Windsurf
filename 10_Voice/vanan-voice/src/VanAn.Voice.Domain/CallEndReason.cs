namespace VanAn.Voice.Domain;

/// <summary>Well-known terminal reasons stored in <c>voice_calls.EndReason</c> (SRS v1.1).</summary>
public static class CallEndReason
{
    public const string MeteringDenied = "METERING_DENIED";
    public const string FeatureDisabled = "FEATURE_DISABLED";
    public const string InvalidContext = "INVALID_CONTEXT";
    public const string ContextRequired = "CONTEXT_REQUIRED";
    public const string SelfCall = "SELF_CALL";
    public const string CallerBusy = "CALLER_BUSY";
    public const string ReceiverBusy = "RECEIVER_BUSY";
    public const string Collision = "CALL_COLLISION";
    public const string Offline = "OFFLINE";
    public const string NegotiationTimeout = "NEGOTIATION_TIMEOUT";
    public const string IceTimeout = "ICE_TIMEOUT";
    public const string ReconnectFailed = "RECONNECT_FAILED";
    public const string MaxDuration = "MAX_DURATION";
    public const string ServerRestart = "SERVER_RESTART";
    public const string UserEnded = "USER_ENDED";
    public const string NetworkLost = "NETWORK_LOST";
    public const string RingTimeout = "RING_TIMEOUT";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
}