namespace VanAn.UI.Platform.Components.Realtime;

/// <summary>
/// VOICE PRESENCE (2026-11-21, task voice_presence): scoped state bus giữa
/// voice JS owner (VoicePresencePanel layout-level hoặc VoiceCallPanel trong chat)
/// và các observer (chat panel delegated mode + global call UI).
///
/// Luồng: component sở hữu `window.vananVoiceChat` (ai initialize trước) nhận
/// JSInvokable events → Publish* vào đây → mọi subscriber (chat panel mirror,
/// global overlay/mini-bar) render theo. ChatSurfaceActive = chat panel đang
/// mounted VÀ call liên quan → global UI ẩn (tránh double UI).
/// </summary>
public sealed class VoiceCallCoordinator
{
    /// <summary>Voice hub transport: connecting | connected | disconnected | reconnecting.</summary>
    public string HubState { get; private set; } = "connecting";

    /// <summary>Call UI state: idle | ringing | calling | accepted | negotiating | connected | reconnecting | terminal.</summary>
    public string CallState { get; private set; } = "idle";

    /// <summary>infoJson từ OnVoiceStateChange (chứa reason khi failed) — replay nguyên cho delegated panel.</summary>
    public string CallInfoJson { get; private set; } = "{}";

    /// <summary>Tên hiển thị người gọi đến (từ IncomingCall payload).</summary>
    public string? IncomingCallerName { get; private set; }

    /// <summary>Context label đã verify (vd tên khách hàng / shop).</summary>
    public string? IncomingContextLabel { get; private set; }

    /// <summary>Đang đổ chuông phía callee.</summary>
    public bool IncomingActive => CallState == "ringing";

    /// <summary>
    /// Chat surface (VoiceCallPanel trong RealtimeChatPanel) đang hiển thị call UI —
    /// global overlay/mini-bar ẩn để tránh double UI. Set bởi chat panel khi mounted
    /// và call đang relevant; clear khi dispose/idle.
    /// </summary>
    public bool ChatSurfaceActive { get; private set; }

    public event Action? Changed;

    private void Notify() => Changed?.Invoke();

    public void PublishHubState(string state)
    {
        HubState = string.IsNullOrWhiteSpace(state) ? "disconnected" : state;
        Notify();
    }

    public void PublishCallState(string state, string infoJson)
    {
        CallState = state;
        CallInfoJson = string.IsNullOrEmpty(infoJson) ? "{}" : infoJson;
        Notify();
    }

    public void PublishIncomingInfo(string? callerName, string? contextLabel)
    {
        IncomingCallerName = callerName;
        IncomingContextLabel = contextLabel;
        Notify();
    }

    public void SetChatSurfaceActive(bool active)
    {
        if (ChatSurfaceActive == active) return;
        ChatSurfaceActive = active;
        Notify();
    }

    /// <summary>Owner dispose / toggle OFF → reset về trạng thái sạch cho session sau.</summary>
    public void Reset()
    {
        HubState = "connecting";
        CallState = "idle";
        CallInfoJson = "{}";
        IncomingCallerName = null;
        IncomingContextLabel = null;
        ChatSurfaceActive = false;
        Notify();
    }

    /// <summary>Parse IncomingCall payload (camelCase JSON từ voice-sdk) → tên + context label.</summary>
    public static (string? Name, string? Label) ParseIncomingInfo(string callJson)
    {
        if (string.IsNullOrWhiteSpace(callJson)) return (null, null);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(callJson);
            var root = doc.RootElement;
            var name = root.TryGetProperty("callerDisplayName", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String
                ? n.GetString() : null;
            var label = root.TryGetProperty("verifiedContextLabel", out var l) && l.ValueKind == System.Text.Json.JsonValueKind.String
                ? l.GetString() : null;
            return (name, label);
        }
        catch { return (null, null); }
    }
}
