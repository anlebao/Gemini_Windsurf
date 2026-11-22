using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace VanAn.UI.Platform.Components.Realtime;

/// <summary>
/// VOICE PRESENCE (2026-11-21, task voice_presence): giữ 1 voice SignalR connection
/// sống suốt session (layout-level) → "login = sẵn sàng nhận cuộc gọi".
///
/// - Nếu `window.vananVoiceChat` chưa init → component này làm OWNER (init JS,
///   nhận JSInvokable, publish lên <see cref="VoiceCallCoordinator"/>).
/// - Nếu đã có owner (chat VoiceCallPanel init trước) → OBSERVER: chỉ subscribe
///   coordinator, global UI vẫn hoạt động.
/// - KHÔNG BAO GIỜ init/dispose JS ở observer mode — tránh steal callbacks.
/// </summary>
public partial class VoicePresencePanel : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    [Inject] private VoiceCallCoordinator? Coordinator { get; set; }
    [Inject] private ILogger<VoicePresencePanel> Logger { get; set; } = null!;

    /// <summary>Host opts in (tenant flag Voice_Call_Enabled).</summary>
    [Parameter] public bool Enabled { get; set; }

    /// <summary>Auth token: staff JWT hoặc customer token.</summary>
    [Parameter] public string? Token { get; set; }

    /// <summary>Caller/owner participant id.</summary>
    [Parameter] public Guid CurrentUserId { get; set; }

    [Parameter] public string VoiceHubUrl { get; set; } = "/hubs/voice";
    [Parameter] public string VoiceServiceUrl { get; set; } = "";

    private DotNetObjectReference<VoicePresencePanel>? _ref;
    private bool _owner;
    private bool _muted;
    private string? _initError;
    private Action? _coordinatorHandler;

    // VIDEO MVP (2026-11-22): host divs mount sau state change → attach lại sau render.
    private bool _pendingVideoAttach;

    // Call timer cho mini bar (mirror VoiceCallPanel P1).
    private System.Threading.PeriodicTimer? _callTimer;
    private DateTime _callStartedAt;
    private TimeSpan _callDuration = TimeSpan.Zero;

    private string CallDurationText =>
        _callDuration > TimeSpan.Zero ? _callDuration.ToString(@"mm\:ss") : "";

    /// <summary>Global UI chỉ hiện khi chat surface KHÔNG đang handle call.</summary>
    private bool ShowGlobalCallUi => Coordinator is { ChatSurfaceActive: false };

    internal static bool CallActive(string state) => VoiceCallCoordinator.IsCallActive(state);

    private string CallBarLabel(string state) => state switch
    {
        "calling" => "Đang gọi...",
        "accepted" or "negotiating" => "Đang kết nối...",
        "connected" => string.IsNullOrEmpty(CallDurationText) ? "Đang gọi" : $"Đang gọi · {CallDurationText}",
        "reconnecting" => "Đang nối lại...",
        _ => "Cuộc gọi"
    };

    protected override async Task OnInitializedAsync()
    {
        if (!Enabled || Coordinator is null
            || string.IsNullOrEmpty(Token) || CurrentUserId == Guid.Empty) return;

        _coordinatorHandler = OnCoordinatorChanged;
        Coordinator.Changed += _coordinatorHandler;

        var alreadyInit = false;
        try { alreadyInit = await JSRuntime.InvokeAsync<bool>("vananVoiceChat.isInitialized"); }
        catch { /* bridge cũ → coi như chưa init, thử owner */ }

        if (!alreadyInit)
        {
            try
            {
                _ref = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("vananVoiceChat.initialize", _ref, new
                {
                    hubUrl = VoiceHubUrl,
                    serviceUrl = VoiceServiceUrl,
                    token = Token
                });
                _owner = true;
            }
            catch (Exception ex)
            {
                _initError = "Không kết nối được máy chủ cuộc gọi.";
            }
        }

        SyncTimerFromState(Coordinator.CallState);
    }

    private void OnCoordinatorChanged() => _ = InvokeAsync(() =>
    {
        SyncTimerFromState(Coordinator?.CallState ?? "idle");
        // Video flags thay đổi → overlay mới render → cần re-attach SDK <video> elements.
        _pendingVideoAttach = true;
        StateHasChanged();
    });

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_pendingVideoAttach) return;
        _pendingVideoAttach = false;
        try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.attachVideoElements"); }
        catch { /* bridge cũ / JS chưa sẵn sàng */ }
    }

    private void SyncTimerFromState(string state)
    {
        if (state == "connected" && _callTimer is null) StartCallTimer();
        else if (state is not ("connected" or "reconnecting" or "negotiating")) StopCallTimer();
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
                    var s = Coordinator?.CallState ?? "idle";
                    if (s is not ("connected" or "reconnecting" or "negotiating")) break;
                    _callDuration = DateTime.UtcNow - _callStartedAt;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch { /* timer disposed */ }
        });
    }

    private void StopCallTimer()
    {
        _callTimer?.Dispose();
        _callTimer = null;
        _callDuration = TimeSpan.Zero;
    }

    // ---- Call controls (JS singleton — owner-agnostic) ----

    private async Task AcceptCallAsync()
    {
        try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.acceptCall"); }
        catch (Exception ex) { Logger.LogWarning(ex, "VoicePresencePanel acceptCall failed"); }
    }
    private async Task RejectCallAsync()
    {
        try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.rejectCall"); }
        catch (Exception ex) { Logger.LogWarning(ex, "VoicePresencePanel rejectCall failed"); }
    }
    private async Task EndCallAsync()
    {
        try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.endCall"); }
        catch (Exception ex) { Logger.LogWarning(ex, "VoicePresencePanel endCall failed"); }
    }
    private async Task ToggleMuteAsync()
    {
        try
        {
            if (_muted) await JSRuntime.InvokeVoidAsync("vananVoiceChat.unmute");
            else await JSRuntime.InvokeVoidAsync("vananVoiceChat.mute");
            _muted = !_muted;
        }
        catch (Exception ex) { Logger.LogWarning(ex, "VoicePresencePanel mute toggle failed"); }
    }

    // VIDEO MVP: camera toggle — re-offer nằm trong SDK (glare-safe); UI mirror
    // trạng thái qua Coordinator.VideoLocalOn (SDK emit onVideoChange).
    private async Task ToggleCameraAsync()
    {
        try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.toggleCamera"); }
        catch (Exception ex) { Logger.LogWarning(ex, "VoicePresencePanel camera toggle failed"); }
    }

    // ---- JSInvokable callbacks (chỉ fire khi component là JS owner) ----

    [JSInvokable]
    public void OnVoiceStateChange(string state, string infoJson)
        => Coordinator?.PublishCallState(state, infoJson);

    [JSInvokable]
    public void OnVoiceConnectionState(string hubState)
        => Coordinator?.PublishHubState(hubState);

    [JSInvokable]
    public void OnVoiceIncoming(string callJson)
    {
        if (Coordinator is null) return;
        var (name, label) = VoiceCallCoordinator.ParseIncomingInfo(callJson);
        Coordinator.PublishIncomingInfo(name, label);
    }

    [JSInvokable] public void OnVoiceLocalStream() { }
    [JSInvokable] public void OnVoiceRemoteStream() { }

    /// <summary>VIDEO MVP: SDK onVideoChange — owner publish lên bus cho global/delegated UI.</summary>
    [JSInvokable]
    public void OnVoiceVideoChange(bool localOn, bool remoteOn)
        => Coordinator?.PublishVideoState(localOn, remoteOn);

    [JSInvokable]
    public void OnVoiceError(string message) => _initError = message;

    public async ValueTask DisposeAsync()
    {
        StopCallTimer();
        if (_coordinatorHandler is not null && Coordinator is not null)
        {
            Coordinator.Changed -= _coordinatorHandler;
            _coordinatorHandler = null;
        }
        // Chỉ owner mới dispose JS — observer để nguyên connection của owner.
        if (_owner && _ref is not null)
        {
            try { await JSRuntime.InvokeVoidAsync("vananVoiceChat.dispose"); }
            catch { /* best effort */ }
            _ref.Dispose();
            _ref = null;
        }
    }
}
