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

    /// <summary>Host opts in (tenant feature flag Voice_Call_Enabled).</summary>
    [Parameter] public bool Enabled { get; set; }

    /// <summary>Chat subject (server RealtimeSubjectType names: "Shop", "Order", ...).</summary>
    [Parameter] public string SubjectType { get; set; } = "Shop";

    /// <summary>Chat subject id — the voice context reference is "{SubjectType}:{SubjectId}".</summary>
    [Parameter] public Guid SubjectId { get; set; }

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
    private bool _muted;
    private bool _missed;
    private string? _error;
    private bool _errorIsMeteringDenied;
    private string _incomingCaller = "";

    private bool _idleLike => _state is "idle" or "ended" or "rejected" or "cancelled" or "missed"
        or "busy" or "offline" or "collision" or "failed";

    private bool _canCall => !string.IsNullOrEmpty(Token) && CurrentUserId != Guid.Empty
        && _state == "idle" && _hubState == "connected";

    // S5 (2026-11-12): guests have no customer token — button stays visible but disabled
    // with a login hint instead of failing on click.
    private bool HubConnecting => _hubState == "connecting";

    private string CallButtonTitle =>
        string.IsNullOrEmpty(Token) || CurrentUserId == Guid.Empty ? "Đăng nhập để gọi thoại"
        : _hubState == "connecting" ? "Đang kết nối máy chủ cuộc gọi..."
        : _hubState != "connected" ? "Không kết nối được máy chủ cuộc gọi"
        : "Gọi thoại";

    private bool _active => _state is "accepted" or "negotiating" or "connected" or "reconnecting";

    private string _displayState => _state switch
    {
        "accepted" => "Đang kết nối...",
        "negotiating" => "Đang kết nối...",
        "reconnecting" => "Đang nối lại...",
        "connected" => "Đang gọi",
        _ => _state
    };

    protected override async Task OnInitializedAsync()
    {
        if (!Enabled) return;
        _hubState = "connecting";
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
    public async Task StartCallAsync()
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
        if (!_canCall) return;
        _error = null;
        try
        {
            await JSRuntime.InvokeVoidAsync("vananVoiceChat.startCall", CurrentUserId, SubjectType, SubjectId);
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

    // ---- JSInvokable callbacks from the voice-chat.js bridge ----

    [JSInvokable]
    public void OnVoiceStateChange(string state, string infoJson)
    {
        _state = state;
        _errorIsMeteringDenied = false;

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
            _error = state switch
            {
                "offline" => "Đối phương hiện không trực tuyến.",
                "busy" => "Đối phương đang bận.",
                _ => "Đang có cuộc gọi khác, vui lòng thử lại."
            };
        }

        // Missed-call hint: ring timeout của callee (state 'missed') hoặc caller nhận
        // RING_TIMEOUT — KHÔNG cho các lần start thất bại (denied/disabled/busy/offline).
        if (state == "missed" || (state == "ended" && reason == "RING_TIMEOUT"))
            _missed = true;
        _ = CallStateChanged.InvokeAsync(state);
        StateHasChanged();
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
        _ = HubStateChanged.InvokeAsync(_hubState);
        StateHasChanged();
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
    public void OnVoiceIncoming(string callJson) => StateHasChanged();

    [JSInvokable]
    public void OnVoiceLocalStream() => StateHasChanged();

    [JSInvokable]
    public void OnVoiceRemoteStream() => StateHasChanged();

    [JSInvokable]
    public void OnVoiceError(string message)
    {
        _error = message;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_ref is not null)
        {
            try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.dispose"); }
            catch { /* best effort */ }
            _ref.Dispose();
            _ref = null;
        }
    }
}