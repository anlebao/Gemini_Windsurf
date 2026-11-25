namespace VanAn.Voice.Domain;

/// <summary>Well-known terminal reasons stored in <c>voice_calls.EndReason</c> (SRS v1.1).</summary>
public static class CallEndReason
{
    public const string MeteringDenied = "METERING_DENIED";
    public const string FeatureDisabled = "FEATURE_DISABLED";
    public const string InvalidContext = "INVALID_CONTEXT";
    /// <summary>Counterpart resolution found only guest (device-id) participants — guests
    /// cannot call per SRS O2; the client shows "khách chưa đăng nhập" instead of a
    /// generic context error.</summary>
    public const string GuestPeer = "GUEST_PEER";
    /// <summary>A Device (guest) caller tried to call a tenant whose VoiceGuestCalling flag
    /// is off — the client shows "đăng nhập nhanh để gọi" so the guest can upgrade inline.</summary>
    public const string GuestCallDisabled = "GUEST_CALL_DISABLED";
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