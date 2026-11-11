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

    private DotNetObjectReference<VoiceCallPanel>? _ref;
    private string _state = "idle";
    private bool _muted;
    private bool _missed;
    private string? _error;
    private bool _errorIsMeteringDenied;
    private string _incomingCaller = "";

    private bool _idleLike => _state is "idle" or "ended" or "rejected" or "cancelled" or "missed"
        or "busy" or "offline" or "collision" or "failed";

    private bool _canCall => !string.IsNullOrEmpty(Token) && CurrentUserId != Guid.Empty && _state == "idle";

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

    private async Task StartCallAsync()
    {
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
                default:
                    _error = null; // timeout/reconnect/etc — không cần thông báo đáng sợ
                    break;
            }
        }

        // Missed-call hint: ring timeout của callee (state 'missed') hoặc caller nhận
        // RING_TIMEOUT — KHÔNG cho các lần start thất bại (denied/disabled/busy/offline).
        if (state == "missed" || (state == "ended" && reason == "RING_TIMEOUT"))
            _missed = true;
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