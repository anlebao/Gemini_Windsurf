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

    /// <summary>
    /// VIDEO MVP (2026-11-22): 'audio' | 'video' — SDK merge callType vào mọi
    /// state-info payload; delegated panels đọc từ đây thay vì parse infoJson riêng.
    /// </summary>
    public string CallType { get; private set; } = "audio";

    /// <summary>VIDEO MVP: local camera đang bật (onVideoChange của SDK owner publish lên).</summary>
    public bool VideoLocalOn { get; private set; }

    /// <summary>VIDEO MVP: remote đang gửi video track.</summary>
    public bool VideoRemoteOn { get; private set; }

    /// <summary>Tên hiển thị người gọi đến (từ IncomingCall payload).</summary>
    public string? IncomingCallerName { get; private set; }

    /// <summary>Context label đã verify (vd tên khách hàng / shop).</summary>
    public string? IncomingContextLabel { get; private set; }

    /// <summary>Đang đổ chuông phía callee.</summary>
    public bool IncomingActive => CallState == "ringing";

    /// <summary>R12 (voice_presence Phase C): call đang sống — dùng cho presence badge đỏ.</summary>
    public static bool IsCallActive(string state) =>
        state is "calling" or "accepted" or "negotiating" or "connected" or "reconnecting";

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
        CallType = ParseCallType(CallInfoJson) ?? "audio";
        Notify();
    }

    /// <summary>VIDEO MVP: SDK owner nhận onVideoChange → publish cho delegated observers.</summary>
    public void PublishVideoState(bool localOn, bool remoteOn)
    {
        VideoLocalOn = localOn;
        VideoRemoteOn = remoteOn;
        Notify();
    }

    /// <summary>Parse callType ('audio'|'video') từ infoJson — SDK merge vào mọi payload.</summary>
    public static string? ParseCallType(string infoJson)
    {
        if (string.IsNullOrWhiteSpace(infoJson)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(infoJson);
            return doc.RootElement.TryGetProperty("callType", out var t)
                && t.ValueKind == System.Text.Json.JsonValueKind.String
                ? t.GetString() : null;
        }
        catch { return null; }
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
        CallType = "audio";
        VideoLocalOn = false;
        VideoRemoteOn = false;
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
