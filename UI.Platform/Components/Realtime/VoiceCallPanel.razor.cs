using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace VanAn.UI.Platform.Components.Realtime;

/// <summary>
/// VOICE SERVICE P4 (2026-11-11): one-to-one voice call inside a chat conversation
/// (SRS v1.1 §9.2 — chat-first integration). Renders a call button, incoming overlay and
/// active-call controls, driven by <c>window.vananVoice</c> through the voice-chat.js bridge.
/// Gated by <see cref="Enabled"/> (host resolves the tenant flag) — disabled = zero UI impact.
/// </summary>
public partial class VoiceCallPanel : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    /// <summary>
    /// VOICE PRESENCE (2026-11-21): state bus với VoicePresencePanel/global call UI.
    /// Null khi host chưa đăng ký (test/legacy) → panel chạy owner mode như cũ.
    /// </summary>
    [Inject] private VoiceCallCoordinator? Coordinator { get; set; }

    /// <summary>Host opts in (tenant feature flag Voice_Call_Enabled).</summary>
    [Parameter] public bool Enabled { get; set; }

    /// <summary>Chat subject (server RealtimeSubjectType names: "Shop", "Order", ...).</summary>
    [Parameter] public string SubjectType { get; set; } = "Shop";

    /// <summary>Chat subject id — the voice context reference is "{SubjectType}:{SubjectId}".</summary>
    [Parameter] public Guid SubjectId { get; set; }

    /// <summary>
    /// 2026-11-13: shop threads are per-customer — staff pass the customer id of the selected
    /// thread so the voice context reference scopes verification + counterpart resolution to
    /// THAT conversation ("{type}:{id}:{peerId}"). Customer/guest callers leave it null — the
    /// verifier scopes to the caller's own thread automatically.
    /// </summary>
    [Parameter] public Guid? PeerId { get; set; }

    /// <summary>The caller's own participant id (staff userId or customerId).</summary>
    [Parameter] public Guid CurrentUserId { get; set; }

    /// <summary>Auth token for the voice service: staff JWT (StaffToken) or customer token.</summary>
    [Parameter] public string? Token { get; set; }

    /// <summary>Voice service SignalR hub URL (default: same-origin /hubs/voice).</summary>
    [Parameter] public string VoiceHubUrl { get; set; } = "/hubs/voice";

    /// <summary>Voice service HTTP base URL (TURN credentials + missed calls).</summary>
    [Parameter] public string VoiceServiceUrl { get; set; } = "";

    /// <summary>
    /// VOICE SERVICE C4 (2026-11-11): hiện link nạp phí khi bị METERING_DENIED.
    /// ShopERP (staff) = true → link /wallet/phi-nen-tang; KhachLink (customer) = false →
    /// message trung tính, không lộ ví (SRS §14.6 UX contract).
    /// </summary>
    [Parameter] public bool ShowWalletLink { get; set; }

    /// <summary>
    /// S3 (2026-11-12): notifies the host of the voice hub transport state
    /// ('connecting' | 'connected' | 'disconnected' | 'reconnecting') so a header call
    /// button can mirror the same readiness.
    /// </summary>
    [Parameter] public EventCallback<string> HubStateChanged { get; set; }

    /// <summary>S3: notifies the host of call-state changes (for header button busy state).</summary>
    [Parameter] public EventCallback<string> CallStateChanged { get; set; }

    private DotNetObjectReference<VoiceCallPanel>? _ref;
    private string _state = "idle";
    private string _hubState = "connecting";

    // VOICE PRESENCE: delegated mode — JS singleton đã được component khác init
    // (layout presence mount trước). Panel KHÔNG init/dispose JS, mirror state
    // từ VoiceCallCoordinator; card UI vẫn render bình thường trong chat.
    private bool _delegated;
    private Action? _coordinatorHandler;
    private bool _muted;
    private bool _missed;
    private string? _error;
    private bool _errorIsMeteringDenied;
    private string _incomingCaller = "";

    // VIDEO MVP (2026-11-22): callType từ infoJson (SDK merge vào mọi payload);
    // camera/remote-video flags từ OnVoiceVideoChange (owner) hoặc coordinator (delegated).
    private string _callType = "audio";
    private bool _cameraOn;
    private bool _selfViewHidden;
    private bool _remoteVideoOn;
    private bool _pendingVideoAttach;

    // P1 (2026-11-21): call duration timer — hiển thị mm:ss khi connected
    // (metering relevance: user thấy phút gọi = thấy phí nền tảng).
    private System.Threading.PeriodicTimer? _callTimer;
    private DateTime _callStartedAt;
    private TimeSpan _callDuration = TimeSpan.Zero;

    private string CallDurationText =>
        _callDuration > TimeSpan.Zero ? _callDuration.ToString(@"mm\:ss") : "";

    private bool _idleLike => _state is "idle" or "ended" or "rejected" or "cancelled" or "missed"
        or "busy" or "offline" or "collision" or "failed";

    // Terminal states (offline/busy/failed/...) are retryable — the SDK resets to idle
    // on the next startCall; gating on exactly "idle" left the button dead forever (S6).
    private bool _canCall => !string.IsNullOrEmpty(Token) && CurrentUserId != Guid.Empty
        && _idleLike && _hubState == "connected";

    // S5 (2026-11-12): guests have no customer token — button stays visible but disabled
    // with a login hint instead of failing on click.
    private bool HubConnecting => _hubState == "connecting";

    private string CallButtonTitle =>
        string.IsNullOrEmpty(Token) || CurrentUserId == Guid.Empty ? "Đăng nhập để gọi thoại"
        : _hubState == "connecting" ? "Đang kết nối máy chủ cuộc gọi..."
        : _hubState != "connected" ? "Không kết nối được máy chủ cuộc gọi"
        : "Gọi thoại";

    private string MuteButtonClass =>
        "voice-call-btn voice-call-btn--mute" + (_muted ? " is-muted" : "");

    private string CameraButtonClass =>
        "voice-call-btn voice-call-btn--mute" + (_cameraOn ? "" : " is-muted");

    // VIDEO MVP: hiện video area cho video call, hoặc audio call vừa upgrade camera
    // (camera toggle mid-call — callType local vẫn 'audio' nhưng track đã live).
    private bool _showVideo => !_idleLike
        && (_callType == "video" || _remoteVideoOn || _cameraOn);

    private bool _active => _state is "accepted" or "negotiating" or "connected" or "reconnecting";

    private string _displayState => _state switch
    {
        "ringing" => "Đang đổ chuông...",
        "calling" => "Đang gọi đi...",
        "accepted" => "Đang kết nối...",
        "negotiating" => "Đang kết nối...",
        "reconnecting" => "Đang nối lại...",
        "connected" => string.IsNullOrEmpty(CallDurationText) ? "Đang gọi" : $"Đang gọi · {CallDurationText}",
        _ => _state
    };

    protected override async Task OnInitializedAsync()
    {
        if (!Enabled) return;
        _hubState = "connecting";

        // VOICE PRESENCE: JS singleton đã có owner (presence panel ở layout) →
        // delegated: subscribe coordinator + replay state hiện tại, không init JS.
        if (Coordinator is not null)
        {
            var alreadyInit = false;
            try { alreadyInit = await JSRuntime.InvokeAsync<bool>("vananVoiceChat.isInitialized"); }
            catch { /* bridge cũ chưa có isInitialized → owner path như cũ */ }
            if (alreadyInit)
            {
                _delegated = true;
                _coordinatorHandler = OnCoordinatorChanged;
                Coordinator.Changed += _coordinatorHandler;
                ReplayCoordinatorState();
                // Missed-call hint vẫn cần fetch kể cả delegated — JS singleton
                // thuộc presence owner nhưng fetchMissedCalls chỉ là HTTP GET.
                await LoadMissedCallsAsync();
                return;
            }
        }

        try
        {
            _ref = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("vananVoiceChat.initialize", _ref, new
            {
                hubUrl = VoiceHubUrl,
                serviceUrl = VoiceServiceUrl,
                token = Token
            });
            await LoadMissedCallsAsync();
        }
        catch (Exception ex)
        {
            // Race với presence owner: initialize throw "đang khởi tạo" nhưng JS đã
            // có owner thật → chuyển delegated thay vì báo lỗi giả.
            if (Coordinator is not null)
            {
                var owned = false;
                try { owned = await JSRuntime.InvokeAsync<bool>("vananVoiceChat.isInitialized"); }
                catch { /* giữ nguyên lỗi gốc */ }
                if (owned)
                {
                    _delegated = true;
                    _coordinatorHandler = OnCoordinatorChanged;
                    Coordinator.Changed += _coordinatorHandler;
                    ReplayCoordinatorState();
                    await LoadMissedCallsAsync();
                    return;
                }
            }
            _hubState = "disconnected";
            _error = "Không khởi tạo được gọi thoại: " + ex.Message;
        }
    }

    private async Task LoadMissedCallsAsync()
    {
        if (string.IsNullOrEmpty(VoiceServiceUrl) || CurrentUserId == Guid.Empty) return;
        var missed = await JSRuntime.InvokeAsync<object[]>("vananVoiceChat.fetchMissedCalls",
            VoiceServiceUrl, CurrentUserId, Token);
        _missed = missed is { Length: > 0 };
        StateHasChanged();
    }

    // Public so the chat header button (S4) can trigger the same single SDK instance.
    // callType 'audio'|'video' (VIDEO MVP) — nút video header truyền "video".
    public async Task StartCallAsync(string callType = "audio")
    {
        // S3: never invoke StartCall while the hub is not Connected — the server round-trip
        // would throw the raw SignalR error and mask the real connection problem.
        if (_hubState != "connected")
        {
            _error = _hubState == "connecting"
                ? "Đang kết nối máy chủ cuộc gọi, vui lòng thử lại trong giây lát."
                : "Không kết nối được máy chủ cuộc gọi.";
            StateHasChanged();
            return;
        }
        if (!_canCall)
        {
            // Identity failures must be visible — a silent return left the header call
            // button looking dead (ShopERP tenant login lacked a GUID sub claim, S9).
            if (string.IsNullOrEmpty(Token) || CurrentUserId == Guid.Empty)
                _error = "Phiên đăng nhập chưa sẵn sàng — vui lòng đăng nhập lại.";
            StateHasChanged();
            return;
        }
        _error = null;
        try
        {
            // Chat-first (P4): pass NULL target — the server resolves the conversation
            // counterpart. Passing CurrentUserId made every call SELF_CALL-denied (S7).
            await JSRuntime.InvokeVoidAsync("vananVoiceChat.startCall", null, SubjectType, SubjectId, PeerId, callType);
        }
        catch (Exception ex)
        {
            _error = "Không gọi được: " + ex.Message;
        }
    }

    private async Task AcceptCallAsync() => await JSRuntime.InvokeVoidAsync("vananVoiceChat.acceptCall");
    private async Task RejectCallAsync() => await JSRuntime.InvokeVoidAsync("vananVoiceChat.rejectCall");
    private async Task CancelCallAsync() => await JSRuntime.InvokeVoidAsync("vananVoiceChat.cancelCall");
    private async Task EndCallAsync() => await JSRuntime.InvokeVoidAsync("vananVoiceChat.endCall");

    private async Task ToggleMuteAsync()
    {
        if (_muted) await JSRuntime.InvokeVoidAsync("vananVoiceChat.unmute");
        else await JSRuntime.InvokeVoidAsync("vananVoiceChat.mute");
        _muted = !_muted;
    }

    // VIDEO MVP: bật/tắt camera — SDK mutate track + re-offer qua SendOffer sẵn có.
    private async Task ToggleCameraAsync()
    {
        try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.toggleCamera"); }
        catch (Exception ex) { _error = "Không đổi được camera: " + ex.Message; }
    }

    // Đổi camera trước/sau — replaceTrack phía media, không renegotiation.
    private async Task SwitchCameraAsync()
    {
        try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.switchCamera"); }
        catch (Exception ex) { _error = "Không đổi được camera: " + ex.Message; }
    }

    // Ẩn/hiện self-view PiP — pure UI pref qua coordinator (camera track không đổi).
    private void ToggleSelfView() => Coordinator?.SetVideoSelfHidden(!Coordinator.VideoSelfHidden);

    // ---- JSInvokable callbacks from the voice-chat.js bridge ----

    [JSInvokable]
    public void OnVoiceStateChange(string state, string infoJson)
    {
        _state = state;
        _errorIsMeteringDenied = false;

        // VIDEO MVP: callType rides every info payload (SDK merge). Về idle → reset
        // video flags (teardown cũng emit video-off nhưng delegated replay cần sạch).
        var parsedCallType = VoiceCallCoordinator.ParseCallType(infoJson);
        if (parsedCallType is not null) _callType = parsedCallType;
        if (_idleLike)
        {
            _callType = "audio";
            _cameraOn = false;
            _remoteVideoOn = false;
        }

        // SRS §14.6 UX contract (C4, 2026-11-11): deny/kill-switch → message rõ ràng.
        var reason = ParseReason(infoJson);
        if (state == "failed" && !string.IsNullOrEmpty(reason))
        {
            switch (reason)
            {
                case "METERING_DENIED":
                    _errorIsMeteringDenied = true;
                    _error = ShowWalletLink
                        ? "Hết hạn mức phí nền tảng — vui lòng nạp phí để gọi thoại."
                        : "Tính năng gọi thoại đang tạm ngừng.";
                    break;
                case "FEATURE_DISABLED":
                    _error = "Tính năng gọi thoại đang tạm ngừng.";
                    break;
                case "INVALID_CONTEXT":
                case "SELF_CALL":
                case "CONTEXT_REQUIRED":
                    _error = "Cuộc gọi không khả dụng trong cuộc trò chuyện này.";
                    break;
                default:
                    _error = "Không gọi được, vui lòng thử lại.";
                    break;
            }
        }

        // Terminal no-call outcomes must be VISIBLE — previously OFFLINE/BUSY/COLLISION
        // silently returned to idle and looked like "button does nothing" (2026-11-12 RV).
        if (state is "offline" or "busy" or "collision")
        {
            // R10 (voice_presence Phase C): terminal no-call phải rõ ràng + gợi ý
            // để lại tin nhắn — caller đang ở trong chat thread sẵn.
            _error = state switch
            {
                "offline" => "Đối phương hiện không trực tuyến — bạn có thể để lại tin nhắn, chúng tôi sẽ chuyển ngay khi họ online.",
                "busy" => "Đối phương đang bận — bạn có thể để lại tin nhắn.",
                _ => "Đang có cuộc gọi khác — vui lòng thử lại sau."
            };
        }

        // Missed-call hint: ring timeout của callee (state 'missed') hoặc caller nhận
        // RING_TIMEOUT — KHÔNG cho các lần start thất bại (denied/disabled/busy/offline).
        if (state == "missed" || (state == "ended" && reason == "RING_TIMEOUT"))
            _missed = true;

        // P1: call timer — start khi connected, chạy tiếp qua reconnecting,
        // dừng ở mọi state khác (terminal/ringing/new call).
        if (state == "connected" && _callTimer is null)
            StartCallTimer();
        else if (state is not ("connected" or "reconnecting" or "negotiating"))
            StopCallTimer();

        // VOICE PRESENCE: owner publish state lên bus; chat surface flag điều
        // khiển global overlay/mini-bar (ẩn khi chat đang hiển thị call UI).
        if (!_delegated) Coordinator?.PublishCallState(state, infoJson);
        Coordinator?.SetChatSurfaceActive(CallUiRelevant(state));

        _ = CallStateChanged.InvokeAsync(state);
        StateHasChanged();
    }

    private static bool CallUiRelevant(string state) =>
        state is "ringing" or "calling" or "accepted" or "negotiating" or "connected" or "reconnecting";

    // ── VOICE PRESENCE: delegated-mode coordinator plumbing ──────────────

    private void ReplayCoordinatorState()
    {
        var c = Coordinator;
        if (c is null) return;
        OnVoiceConnectionState(c.HubState);
        OnVoiceStateChange(c.CallState, c.CallInfoJson);
        SyncVideoFromCoordinator(c);
    }

    private void OnCoordinatorChanged()
    {
        var c = Coordinator;
        if (c is null) return;
        _ = InvokeAsync(() =>
        {
            OnVoiceConnectionState(c.HubState);
            OnVoiceStateChange(c.CallState, c.CallInfoJson);
            SyncVideoFromCoordinator(c);
        });
    }

    private void SyncVideoFromCoordinator(VoiceCallCoordinator c)
    {
        if (_cameraOn == c.VideoLocalOn && _remoteVideoOn == c.VideoRemoteOn
            && _selfViewHidden == c.VideoSelfHidden) return;
        _cameraOn = c.VideoLocalOn;
        _selfViewHidden = c.VideoSelfHidden;
        _remoteVideoOn = c.VideoRemoteOn;
        _pendingVideoAttach = true;
    }

    /// <summary>S3: hub transport state from voice-sdk.js via voice-chat.js.</summary>
    [JSInvokable]
    public void OnVoiceConnectionState(string hubState)
    {
        _hubState = string.IsNullOrWhiteSpace(hubState) ? "disconnected" : hubState;
        // Keep the root-cause init error if present (??=) — only add a hint when the
        // connection drops mid-session while idle.
        if (_hubState == "disconnected" && _state == "idle")
            _error ??= "Mất kết nối máy chủ cuộc gọi — tải lại trang để thử lại.";
        if (!_delegated) Coordinator?.PublishHubState(_hubState);
        _ = HubStateChanged.InvokeAsync(_hubState);
        StateHasChanged();
    }

    private void StartCallTimer()
    {
        _callStartedAt = DateTime.UtcNow;
        _callDuration = TimeSpan.Zero;
        _callTimer?.Dispose();
        _callTimer = new System.Threading.PeriodicTimer(TimeSpan.FromSeconds(1));
        _ = Task.Run(async () =>
        {
            try
            {
                while (await _callTimer.WaitForNextTickAsync())
                {
                    if (_state is not ("connected" or "reconnecting" or "negotiating")) break;
                    _callDuration = DateTime.UtcNow - _callStartedAt;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch { /* timer disposed / circuit closed */ }
        });
    }

    private void StopCallTimer()
    {
        _callTimer?.Dispose();
        _callTimer = null;
        _callDuration = TimeSpan.Zero;
    }

    private static string ParseReason(string infoJson)
    {
        if (string.IsNullOrWhiteSpace(infoJson)) return "";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(infoJson);
            return doc.RootElement.TryGetProperty("reason", out var r) && r.ValueKind == System.Text.Json.JsonValueKind.String
                ? r.GetString() ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    [JSInvokable]
    public void OnVoiceIncoming(string callJson)
    {
        // VOICE PRESENCE: đẩy thông tin người gọi lên bus cho global overlay.
        if (!_delegated && Coordinator is not null)
        {
            var (name, label) = VoiceCallCoordinator.ParseIncomingInfo(callJson);
            Coordinator.PublishIncomingInfo(name, label);
        }
        StateHasChanged();
    }

    [JSInvokable]
    public void OnVoiceLocalStream() => StateHasChanged();

    [JSInvokable]
    public void OnVoiceRemoteStream() => StateHasChanged();

    /// <summary>
    /// VIDEO MVP: SDK onVideoChange(localOn, remoteOn) — chỉ fire trên JS owner.
    /// Owner publish lên coordinator để delegated/global UI render cùng trạng thái;
    /// host divs mount sau state change → flag attach trong OnAfterRenderAsync.
    /// </summary>
    [JSInvokable]
    public void OnVoiceVideoChange(bool localOn, bool remoteOn)
    {
        _cameraOn = localOn;
        _remoteVideoOn = remoteOn;
        _pendingVideoAttach = true;
        if (!_delegated) Coordinator?.PublishVideoState(localOn, remoteOn);
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Video host divs render SAU state change — re-attach SDK-owned <video>
        // elements vào host mới (idempotent; bridge optional-chained cho SDK cũ).
        if (!_pendingVideoAttach) return;
        _pendingVideoAttach = false;
        try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.attachVideoElements"); }
        catch { /* bridge cũ / JS chưa sẵn sàng */ }
    }

    [JSInvokable]
    public void OnVoiceError(string message)
    {
        _error = message;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        StopCallTimer();
        Coordinator?.SetChatSurfaceActive(false);
        if (_coordinatorHandler is not null && Coordinator is not null)
        {
            Coordinator.Changed -= _coordinatorHandler;
            _coordinatorHandler = null;
        }
        // Delegated mode: KHÔNG dispose JS — singleton thuộc owner (presence).
        if (!_delegated && _ref is not null)
        {
            try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.dispose"); }
            catch { /* best effort */ }
            _ref.Dispose();
            _ref = null;
        }
    }
}